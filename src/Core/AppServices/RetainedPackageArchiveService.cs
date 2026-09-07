using DivinityModManager.Models.NexusMods;
using DivinityModManager.Util;

using Newtonsoft.Json;

using System.Collections.ObjectModel;
using System.Security.Cryptography;

namespace DivinityModManager.AppServices;

public interface IRetainedPackageArchiveService
{
	ObservableCollection<RetainedPackageArchiveEntry> Entries { get; }
	string RootDirectory { get; }
	long UsageBytes { get; }
	Task InitializeAsync(CancellationToken cancellationToken = default);
	Task<RetainedPackageArchiveEntry> RetainAsync(string sourcePath, NxmDownloadItem item,
		string destination, long quotaBytes, bool removeSourceOnSuccess,
		CancellationToken cancellationToken = default);
	string GetPackagePath(string sha256);
	Task EnforceQuotaAsync(long quotaBytes, CancellationToken cancellationToken = default);
	Task ClearAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Long-term, opt-in package storage. Files are addressed by verified SHA-256 and the
/// index deliberately stores no local source paths, credentials, signed URLs, or headers.
/// </summary>
public sealed class RetainedPackageArchiveService : IRetainedPackageArchiveService
{
	private const int CurrentVersion = 1;
	private readonly string _manifestPath;
	private readonly string _backupPath;
	private readonly SemaphoreSlim _gate = new(1, 1);

	public ObservableCollection<RetainedPackageArchiveEntry> Entries { get; } = [];
	public string RootDirectory { get; }
	public long UsageBytes => Entries.Sum(entry => Math.Max(0, entry.SizeBytes));

	public RetainedPackageArchiveService(string rootDirectory)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(rootDirectory);
		RootDirectory = Path.GetFullPath(rootDirectory);
		_manifestPath = Path.Combine(RootDirectory, "archives.json");
		_backupPath = Path.Combine(RootDirectory, "archives.backup.json");
	}

	public async Task InitializeAsync(CancellationToken cancellationToken = default)
	{
		// Opting out must not create an empty library as a startup side effect.
		if (!Directory.Exists(RootDirectory)) return;
		await _gate.WaitAsync(cancellationToken);
		try
		{
			var manifest = await LoadManifestAsync(cancellationToken);
			var valid = manifest.Entries
				.Where(entry => TryGetStoredPath(entry, out var path) && File.Exists(path))
				.GroupBy(entry => entry.Sha256, StringComparer.OrdinalIgnoreCase)
				.Select(group => group.OrderByDescending(entry => entry.LastUsedAtUtc).First())
				.OrderByDescending(entry => entry.LastUsedAtUtc)
				.ToArray();
			Entries.Clear();
			foreach (var entry in valid) Entries.Add(entry);
			if (manifest.Entries.Count != valid.Length) await SaveManifestAsync(cancellationToken);
		}
		finally { _gate.Release(); }
	}

	public async Task<RetainedPackageArchiveEntry> RetainAsync(string sourcePath, NxmDownloadItem item,
		string destination, long quotaBytes, bool removeSourceOnSuccess,
		CancellationToken cancellationToken = default)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(sourcePath);
		ArgumentNullException.ThrowIfNull(item);
		if (!File.Exists(sourcePath)) throw new FileNotFoundException("The installed package is no longer available to retain.", sourcePath);
		if (!IsHash(item.ArchiveSha256)) throw new InvalidDataException("The installed package does not have a verified SHA-256 identity.");
		if (quotaBytes <= 0) throw new ArgumentOutOfRangeException(nameof(quotaBytes));

		await _gate.WaitAsync(cancellationToken);
		try
		{
			var sourceInfo = new FileInfo(sourcePath);
			if (sourceInfo.Length > quotaBytes)
				throw new IOException("This package is larger than the configured archive-library quota.");
			var actualHash = await ComputeSha256Async(sourcePath, cancellationToken);
			if (!actualHash.Equals(item.ArchiveSha256, StringComparison.OrdinalIgnoreCase))
				throw new InvalidDataException("The package changed after Redux verified it, so it was not retained.");

			var normalizedHash = actualHash.ToLowerInvariant();
			var existing = Entries.FirstOrDefault(entry => entry.Sha256.Equals(normalizedHash, StringComparison.OrdinalIgnoreCase));
			var extension = SafeExtension(item.SourceFileName, item.CompletedFileName, sourcePath);
			var storedFileName = existing?.StoredFileName ?? Path.Combine(normalizedHash[..2], normalizedHash + extension);
			var destinationPath = Path.Combine(RootDirectory, "packages", storedFileName);
			if (existing == null || !File.Exists(destinationPath)
				|| !normalizedHash.Equals(await ComputeSha256Async(destinationPath, cancellationToken), StringComparison.OrdinalIgnoreCase))
			{
				await AtomicFileWriter.CopyFileAsync(sourcePath, destinationPath, cancellationToken: cancellationToken);
				if (!normalizedHash.Equals(await ComputeSha256Async(destinationPath, cancellationToken), StringComparison.OrdinalIgnoreCase))
				{
					File.Delete(destinationPath);
					throw new InvalidDataException("Redux could not verify the retained package copy.");
				}
			}

			var now = DateTimeOffset.UtcNow;
			var entry = existing ?? new RetainedPackageArchiveEntry { Sha256 = normalizedHash, StoredFileName = storedFileName };
			entry.OriginalFileName = SafeDisplayFileName(item.SourceFileName, item.FileName, sourcePath);
			entry.SizeBytes = sourceInfo.Length;
			entry.SourceKind = item.SourceKind;
			entry.NexusModId = item.SourceKind == AcquiredPackageSourceKind.NexusMods ? item.ModId : 0;
			entry.NexusFileId = item.SourceKind == AcquiredPackageSourceKind.NexusMods ? item.FileId : 0;
			entry.ProjectName = item.ProjectName ?? String.Empty;
			entry.Version = item.Version ?? String.Empty;
			entry.PackageKind = item.DetectedContentKind ?? String.Empty;
			entry.InstallDestination = destination ?? String.Empty;
			entry.InstalledAtUtc = now;
			entry.LastUsedAtUtc = now;
			entry.ThumbnailUrl = item.ThumbnailUrl ?? String.Empty;
			if (existing == null) Entries.Add(entry);

			await PruneToQuotaAsync(quotaBytes, normalizedHash, cancellationToken);
			await SaveManifestAsync(cancellationToken);
			if (removeSourceOnSuccess && !PathsEqual(sourcePath, destinationPath)) File.Delete(sourcePath);
			return entry;
		}
		finally { _gate.Release(); }
	}

	public string GetPackagePath(string sha256)
	{
		if (!IsHash(sha256)) return null;
		var entry = Entries.FirstOrDefault(candidate => candidate.Sha256.Equals(sha256, StringComparison.OrdinalIgnoreCase));
		if (!TryGetStoredPath(entry, out var path)) return null;
		return File.Exists(path) ? path : null;
	}

	public async Task EnforceQuotaAsync(long quotaBytes, CancellationToken cancellationToken = default)
	{
		if (quotaBytes <= 0) throw new ArgumentOutOfRangeException(nameof(quotaBytes));
		if (!Directory.Exists(RootDirectory)) return;
		await _gate.WaitAsync(cancellationToken);
		try
		{
			await PruneToQuotaAsync(quotaBytes, null, cancellationToken);
			await SaveManifestAsync(cancellationToken);
		}
		finally { _gate.Release(); }
	}

	public async Task ClearAsync(CancellationToken cancellationToken = default)
	{
		await _gate.WaitAsync(cancellationToken);
		try
		{
			foreach (var entry in Entries.ToArray())
			{
				var path = TryGetStoredPath(entry, out var storedPath) ? storedPath : null;
				if (path != null && File.Exists(path)) File.Delete(path);
			}
			Entries.Clear();
			if (Directory.Exists(RootDirectory)) await SaveManifestAsync(cancellationToken);
		}
		finally { _gate.Release(); }
	}

	private async Task PruneToQuotaAsync(long quotaBytes, string protectedHash, CancellationToken cancellationToken)
	{
		foreach (var entry in Entries.Where(entry => protectedHash == null
				|| !entry.Sha256.Equals(protectedHash, StringComparison.OrdinalIgnoreCase))
			.OrderBy(entry => entry.LastUsedAtUtc).ToArray())
		{
			if (UsageBytes <= quotaBytes) break;
			var path = GetPackagePath(entry.Sha256);
			if (path != null) File.Delete(path);
			Entries.Remove(entry);
			cancellationToken.ThrowIfCancellationRequested();
		}
		await Task.CompletedTask;
	}

	private async Task<ArchiveManifest> LoadManifestAsync(CancellationToken cancellationToken)
	{
		foreach (var path in new[] { _manifestPath, _backupPath })
		{
			if (!File.Exists(path)) continue;
			try
			{
				var json = await File.ReadAllTextAsync(path, cancellationToken);
				var manifest = JsonConvert.DeserializeObject<ArchiveManifest>(json);
				if (manifest?.Version == CurrentVersion && manifest.Entries != null) return manifest;
			}
			catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or Newtonsoft.Json.JsonException) { }
		}
		return new ArchiveManifest();
	}

	private async Task SaveManifestAsync(CancellationToken cancellationToken)
	{
		var manifest = new ArchiveManifest { Entries = Entries.OrderByDescending(entry => entry.LastUsedAtUtc).ToList() };
		var bytes = System.Text.Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(manifest, Formatting.Indented));
		await AtomicFileWriter.WriteAllBytesAsync(_manifestPath, bytes, _backupPath,
			validateTemporaryFile: path =>
			{
				var candidate = JsonConvert.DeserializeObject<ArchiveManifest>(File.ReadAllText(path));
				return candidate?.Version == CurrentVersion && candidate.Entries != null;
			}, cancellationToken: cancellationToken);
	}

	private static async Task<string> ComputeSha256Async(string path, CancellationToken cancellationToken)
	{
		await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read,
			65536, FileOptions.Asynchronous | FileOptions.SequentialScan);
		return Convert.ToHexString(await SHA256.HashDataAsync(stream, cancellationToken)).ToLowerInvariant();
	}

	private static string SafeExtension(params string[] candidates)
	{
		foreach (var candidate in candidates)
		{
			var extension = Path.GetExtension(candidate ?? String.Empty).ToLowerInvariant();
			if (extension.Length is > 1 and <= 10 && extension.Skip(1).All(Char.IsLetterOrDigit)) return extension;
		}
		return ".package";
	}

	private static string SafeDisplayFileName(string sourceFileName, string fileName, string sourcePath)
	{
		foreach (var candidate in new[] { sourceFileName, fileName, Path.GetFileName(sourcePath) })
			if (!String.IsNullOrWhiteSpace(candidate) && candidate == Path.GetFileName(candidate)) return candidate;
		return "retained-package";
	}

	private static bool IsHash(string value) => value?.Length == 64 && value.All(Uri.IsHexDigit);
	private bool TryGetStoredPath(RetainedPackageArchiveEntry entry, out string path)
	{
		path = null;
		if (entry == null || !IsHash(entry.Sha256) || String.IsNullOrWhiteSpace(entry.StoredFileName)
			|| Path.IsPathRooted(entry.StoredFileName)) return false;
		var parts = entry.StoredFileName.Split([Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar],
			StringSplitOptions.RemoveEmptyEntries);
		if (parts.Length != 2 || !parts[0].Equals(entry.Sha256[..2], StringComparison.OrdinalIgnoreCase)
			|| !Path.GetFileNameWithoutExtension(parts[1]).Equals(entry.Sha256, StringComparison.OrdinalIgnoreCase)) return false;
		var extension = Path.GetExtension(parts[1]);
		if (extension.Length is < 2 or > 10 || !extension.Skip(1).All(Char.IsLetterOrDigit)) return false;
		var packagesRoot = Path.GetFullPath(Path.Combine(RootDirectory, "packages")) + Path.DirectorySeparatorChar;
		var candidate = Path.GetFullPath(Path.Combine(packagesRoot, entry.StoredFileName));
		if (!candidate.StartsWith(packagesRoot, StringComparison.OrdinalIgnoreCase)) return false;
		path = candidate;
		return true;
	}
	private static bool PathsEqual(string first, string second) => Path.GetFullPath(first)
		.Equals(Path.GetFullPath(second), StringComparison.OrdinalIgnoreCase);

	private sealed class ArchiveManifest
	{
		public int Version { get; set; } = CurrentVersion;
		public List<RetainedPackageArchiveEntry> Entries { get; set; } = [];
	}
}
