using SharpCompress.Archives;
using SharpCompress.Common;
using SharpCompress.Readers;

using System.Security.Cryptography;

namespace DivinityModManager.AppServices;

/// <summary>Verified local provenance retained for a completed Nexus download.</summary>
public sealed record NexusRetainedDownload(long ModId, long FileId, string Path, string ArchiveSha256, string Version);

/// <summary>
/// Re-establishes an installed PAK's exact Nexus file identity using only local bytes.
/// Package names, UUIDs, and declared versions are deliberately never used as proof.
/// </summary>
public sealed class NexusInstalledIdentityResolver
{
	private const int MaximumArchiveEntries = 4096;
	private const long MaximumPakBytes = 4L * 1024 * 1024 * 1024;
	private const long MaximumArchivePakBytes = 8L * 1024 * 1024 * 1024;
	private const long MaximumRetainedPackageBytes = 8L * 1024 * 1024 * 1024;
	private const int MaximumEvidenceCandidates = 256;
	private const int BufferSize = 64 * 1024;
	private static readonly HashSet<string> ArchiveExtensions = new(StringComparer.OrdinalIgnoreCase)
	{
		".zip", ".7z", ".rar"
	};
	private readonly Func<string, CancellationToken, Task<ReduxModDatabaseMatch>> _resolveBundledPak;

	public NexusInstalledIdentityResolver(Func<string, CancellationToken, Task<ReduxModDatabaseMatch>> resolveBundledPak = null)
	{
		_resolveBundledPak = resolveBundledPak ?? ReduxModDatabaseService.TryResolvePakAsync;
	}

	public async Task<NexusUpdateInstalledFile> ResolveAsync(NexusUpdateInstalledFile installed,
		IReadOnlyList<NexusRetainedDownload> evidence, CancellationToken token = default)
	{
		ArgumentNullException.ThrowIfNull(installed);
		token.ThrowIfCancellationRequested();
		if (String.IsNullOrWhiteSpace(installed.FilePath)) return Unreadable(installed);
		try
		{
			// Keep this handle open through every identity comparison so the PAK cannot be replaced after hashing.
			await using var installedPak = new FileStream(installed.FilePath, FileMode.Open, FileAccess.Read, FileShare.Read,
				BufferSize, FileOptions.Asynchronous | FileOptions.SequentialScan);
			var packageSha256 = await ComputeSha256Async(installedPak, token).ConfigureAwait(false);
			var retainedCandidates = installed.ModId > 0
				? (evidence ?? Array.Empty<NexusRetainedDownload>()).Where(candidate => IsCandidateForProject(candidate, installed.ModId))
					.Take(MaximumEvidenceCandidates + 1).ToArray()
				: Array.Empty<NexusRetainedDownload>();
			if (retainedCandidates.Length > MaximumEvidenceCandidates) return TooManyCandidates(installed, packageSha256);

			var matches = new List<VerifiedFile>();
			await AddBundledMatchAsync(installed, matches, token).ConfigureAwait(false);
			foreach (var retained in retainedCandidates)
			{
				token.ThrowIfCancellationRequested();
				if (await RetainedDownloadContainsPakAsync(retained, packageSha256, token).ConfigureAwait(false))
					matches.Add(new(retained.FileId, retained.Version, true));
			}

			var distinct = matches.GroupBy(match => match.FileId).ToArray();
			if (distinct.Length > 1)
			{
				return installed with
				{
					FileId = 0,
					HasExactFileIdentity = false,
					PackageSha256 = packageSha256,
					DownloadVersion = String.Empty,
					IdentityDescription = "Verified local sources refer to different Nexus downloads, so the installed download was not identified."
				};
			}
			if (distinct.Length == 1)
			{
				var versions = distinct[0].Select(match => match.Version).Where(version => !String.IsNullOrWhiteSpace(version))
					.Distinct(StringComparer.Ordinal).ToArray();
				var hasRetainedMatch = distinct[0].Any(match => match.IsRetained);
				var version = versions.Length == 1 ? versions[0] : String.Empty;
				var description = hasRetainedMatch
					? $"Installed download matches Nexus file #{distinct[0].Key} based on retained package bytes."
					: $"Installed download matches Nexus file #{distinct[0].Key} based on the bundled package fingerprint.";
				if (versions.Length > 1) description += " Retained release-version metadata differs.";
				return installed with
				{
					FileId = distinct[0].Key,
					HasExactFileIdentity = true,
					PackageSha256 = packageSha256,
					DownloadVersion = version,
					IdentityDescription = description
				};
			}

			return Unverified(installed, packageSha256);
		}
		catch (OperationCanceledException) when (token.IsCancellationRequested) { throw; }
		catch (Exception ex) when (IsLocalReadFailure(ex)) { return Unreadable(installed); }
	}

	private async Task AddBundledMatchAsync(NexusUpdateInstalledFile installed, List<VerifiedFile> matches,
		CancellationToken token)
	{
		if (installed.ModId <= 0 || String.IsNullOrWhiteSpace(installed.FilePath)) return;
		try
		{
			var bundled = await _resolveBundledPak(installed.FilePath, token).ConfigureAwait(false);
			if (bundled?.Kind == ReduxOfflineMatchKind.ExactPak && bundled.ModId == installed.ModId && bundled.FileId > 0)
				matches.Add(new(bundled.FileId, String.Empty, false));
		}
		catch (OperationCanceledException) when (token.IsCancellationRequested) { throw; }
		catch (Exception ex) when (IsLocalReadFailure(ex)) { }
	}

	private static async Task<bool> RetainedDownloadContainsPakAsync(NexusRetainedDownload retained,
		string installedPakSha256, CancellationToken token)
	{
		try
		{
			var extension = Path.GetExtension(retained.Path);
			if (!IsRetainedPackageExtension(extension)) return false;
			await using var input = new FileStream(retained.Path, FileMode.Open, FileAccess.Read, FileShare.Read,
				BufferSize, FileOptions.Asynchronous | FileOptions.SequentialScan);
			if (input.Length <= 0 || input.Length > MaximumRetainedPackageBytes) return false;
			var packageSha256 = await ComputeSha256Async(input, token).ConfigureAwait(false);
			if (packageSha256 == null || !packageSha256.Equals(retained.ArchiveSha256, StringComparison.OrdinalIgnoreCase)) return false;

			if (extension.Equals(".pak", StringComparison.OrdinalIgnoreCase))
				return packageSha256.Equals(installedPakSha256, StringComparison.OrdinalIgnoreCase);
			input.Position = 0;
			return await ArchiveContainsPakAsync(input, installedPakSha256, token).ConfigureAwait(false);
		}
		catch (OperationCanceledException) when (token.IsCancellationRequested) { throw; }
		catch (Exception ex) when (IsLocalReadFailure(ex)) { return false; }
	}

	private static async Task<bool> ArchiveContainsPakAsync(Stream input, string installedPakSha256,
		CancellationToken token)
	{
		token.ThrowIfCancellationRequested();
		using var archive = ArchiveFactory.OpenArchive(input, new ReaderOptions());
		var entries = archive.Entries.Take(MaximumArchiveEntries + 1).ToArray();
		token.ThrowIfCancellationRequested();
		if (entries.Length == 0 || entries.Length > MaximumArchiveEntries || entries.Any(entry => entry.IsEncrypted || entry.Size < 0))
			return false;

		var pakEntries = entries.Where(entry => !entry.IsDirectory && entry.Key?.EndsWith(".pak", StringComparison.OrdinalIgnoreCase) == true).ToArray();
		if (pakEntries.Length == 0) return false;
		long declaredBytes = 0;
		foreach (var entry in pakEntries)
		{
			if (entry.Size <= 0 || entry.Size > MaximumPakBytes || declaredBytes > MaximumArchivePakBytes - entry.Size)
				return false;
			declaredBytes += entry.Size;
		}

		foreach (var entry in pakEntries)
		{
			token.ThrowIfCancellationRequested();
			var entrySha256 = await ComputeArchiveEntrySha256Async(entry, token).ConfigureAwait(false);
			if (entrySha256.Equals(installedPakSha256, StringComparison.OrdinalIgnoreCase)) return true;
		}
		return false;
	}

	private static async Task<string> ComputeArchiveEntrySha256Async(IArchiveEntry entry, CancellationToken token)
	{
		using var input = entry.OpenEntryStream();
		using var hasher = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
		var buffer = new byte[BufferSize];
		long bytesRead = 0;
		while (true)
		{
			var read = await input.ReadAsync(buffer.AsMemory(0, buffer.Length), token).ConfigureAwait(false);
			if (read == 0) break;
			if (bytesRead > entry.Size - read) throw new InvalidDataException("The retained package entry exceeds its declared size.");
			bytesRead += read;
			hasher.AppendData(buffer, 0, read);
		}
		if (bytesRead != entry.Size) throw new InvalidDataException("The retained package entry is truncated.");
		return Convert.ToHexString(hasher.GetHashAndReset()).ToLowerInvariant();
	}

	private static async Task<string> ComputeSha256Async(Stream input, CancellationToken token) =>
		Convert.ToHexString(await SHA256.HashDataAsync(input, token).ConfigureAwait(false)).ToLowerInvariant();

	private static NexusUpdateInstalledFile Unreadable(NexusUpdateInstalledFile installed) => installed with
	{
		HasExactFileIdentity = false,
		PackageSha256 = String.Empty,
		DownloadVersion = String.Empty,
		IdentityDescription = "The installed download was not identified because its package could not be read."
	};

	private static NexusUpdateInstalledFile TooManyCandidates(NexusUpdateInstalledFile installed, string packageSha256) => installed with
	{
		FileId = 0,
		HasExactFileIdentity = false,
		PackageSha256 = packageSha256,
		DownloadVersion = String.Empty,
		IdentityDescription = "Too many retained Nexus download candidates were found to identify the installed download safely."
	};

	private static NexusUpdateInstalledFile Unverified(NexusUpdateInstalledFile installed, string packageSha256)
	{
		var hasRecordedIdentity = installed.FileId > 0 && (String.IsNullOrEmpty(installed.PackageSha256)
			|| String.Equals(installed.PackageSha256, packageSha256, StringComparison.OrdinalIgnoreCase));
		return installed with
		{
			FileId = hasRecordedIdentity ? installed.FileId : 0,
			HasExactFileIdentity = false,
			PackageSha256 = packageSha256,
			DownloadVersion = String.Empty,
			IdentityDescription = hasRecordedIdentity
				? $"Recorded Nexus file #{installed.FileId} was not reverified from local package evidence."
				: "The installed download was not identified from local package evidence."
		};
	}

	private static bool IsUsable(NexusRetainedDownload retained) => retained != null && retained.ModId > 0
		&& retained.FileId > 0 && !String.IsNullOrWhiteSpace(retained.Path) && IsSha256(retained.ArchiveSha256);
	private static bool IsCandidateForProject(NexusRetainedDownload retained, long projectId) => IsUsable(retained)
		&& retained.ModId == projectId && IsRetainedPackageExtension(Path.GetExtension(retained.Path));
	private static bool IsRetainedPackageExtension(string extension) => extension.Equals(".pak", StringComparison.OrdinalIgnoreCase)
		|| ArchiveExtensions.Contains(extension);
	private static bool IsSha256(string value) => value?.Length == 64 && value.All(Uri.IsHexDigit);
	private static bool IsLocalReadFailure(Exception ex) => ex is IOException or UnauthorizedAccessException
		or InvalidDataException or SharpCompressException or InvalidFormatException or ArchiveException
		or ArchiveOperationException or InvalidOperationException or OverflowException
		or System.Security.Cryptography.CryptographicException or ArgumentException or NotSupportedException;

	private sealed record VerifiedFile(long FileId, string Version, bool IsRetained);
}
