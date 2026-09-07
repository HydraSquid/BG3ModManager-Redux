using DivinityModManager.Models.NexusMods;
using DivinityModManager.Util;

using Newtonsoft.Json;

using System.Runtime.Serialization;

namespace DivinityModManager.AppServices;

public interface INxmDownloadStore
{
	Task<IReadOnlyList<NxmDownloadItem>> LoadAsync(CancellationToken cancellationToken = default);
	Task SaveAsync(IEnumerable<NxmDownloadItem> items, CancellationToken cancellationToken = default);
	Task<IReadOnlyList<NxmDownloadItem>> ReconcileAsync(CancellationToken cancellationToken = default);
}

public sealed class NxmDownloadStore : INxmDownloadStore
{
	private const int CurrentVersion = 1;
	private readonly string _directory;
	private readonly string _manifestPath;
	private readonly string _backupPath;
	private readonly SemaphoreSlim _writeLock = new(1, 1);

	public NxmDownloadStore(string directory)
	{
		if (String.IsNullOrWhiteSpace(directory)) throw new ArgumentException("A downloads directory is required.", nameof(directory));
		_directory = Path.GetFullPath(directory);
		_manifestPath = Path.Combine(_directory, "downloads.json");
		_backupPath = Path.Combine(_directory, "downloads.backup.json");
	}

	public async Task<IReadOnlyList<NxmDownloadItem>> LoadAsync(CancellationToken cancellationToken = default)
	{
		if (File.Exists(_manifestPath))
		{
			try { return await ReadManifestAsync(_manifestPath, cancellationToken); }
			catch (Exception ex) when (IsInvalidManifest(ex)) { Quarantine(_manifestPath, "downloads"); }
		}
		if (!File.Exists(_backupPath)) return Array.Empty<NxmDownloadItem>();
		try
		{
			var restored = await ReadManifestAsync(_backupPath, cancellationToken);
			await AtomicFileWriter.WriteAllBytesAsync(_manifestPath, await File.ReadAllBytesAsync(_backupPath, cancellationToken),
				cancellationToken: cancellationToken);
			return restored;
		}
		catch (Exception ex) when (IsInvalidManifest(ex))
		{
			Quarantine(_backupPath, "downloads-backup");
			return Array.Empty<NxmDownloadItem>();
		}
	}

	public async Task SaveAsync(IEnumerable<NxmDownloadItem> items, CancellationToken cancellationToken = default)
	{
		var manifest = new NxmDownloadManifest
		{
			Version = CurrentVersion,
			Items = Normalize(items).ToList()
		};
		var bytes = System.Text.Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(manifest, Formatting.Indented));
		await _writeLock.WaitAsync(cancellationToken);
		try
		{
			await AtomicFileWriter.WriteAllBytesAsync(_manifestPath, bytes, _backupPath,
				validateTemporaryFile: path =>
				{
					var candidate = JsonConvert.DeserializeObject<NxmDownloadManifest>(File.ReadAllText(path));
					return candidate?.Version == CurrentVersion && candidate.Items != null;
				}, cancellationToken: cancellationToken);
		}
		finally
		{
			_writeLock.Release();
		}
	}

	public async Task<IReadOnlyList<NxmDownloadItem>> ReconcileAsync(CancellationToken cancellationToken = default)
	{
		var items = (await LoadAsync(cancellationToken)).ToList();
		var stalePartialPaths = new List<string>();
		foreach (var item in items)
		{
			var completedPath = SafePath(item.CompletedFileName);
			var partialPath = SafePath(item.PartialFileName);
			if (item.State is NxmDownloadState.Downloaded or NxmDownloadState.NeedsReview
				or NxmDownloadState.InstallFailed)
			{
				if (completedPath == null || !File.Exists(completedPath))
				{
					item.State = NxmDownloadState.Failed;
					item.ErrorCode = "missing-file";
					item.ErrorDetails = String.Empty;
				}
				else if (item.SizeBytes > 0 && new FileInfo(completedPath).Length != item.SizeBytes)
				{
					item.State = NxmDownloadState.Failed;
					item.ErrorCode = "file-size-mismatch";
					item.ErrorDetails = String.Empty;
				}
				else if (!String.IsNullOrWhiteSpace(item.ArchiveSha256)
					&& !String.Equals(item.ArchiveSha256, await ComputeSha256Async(completedPath, cancellationToken), StringComparison.OrdinalIgnoreCase))
				{
					item.State = NxmDownloadState.Failed;
					item.ErrorCode = "archive-changed";
					item.ErrorDetails = "The completed archive no longer matches the file Redux downloaded. Download it again before review.";
				}
			}
			else if (item.State is NxmDownloadState.Downloading or NxmDownloadState.Queued or NxmDownloadState.RetryWaiting or NxmDownloadState.Resolving)
			{
				if (partialPath != null && File.Exists(partialPath)) item.BytesReceived = new FileInfo(partialPath).Length;
				item.State = item.RequiresAuthorization ? NxmDownloadState.NeedsFreshLink : NxmDownloadState.Queued;
			}
			else if (item.State == NxmDownloadState.Installing)
			{
				item.State = completedPath != null && File.Exists(completedPath)
					? NxmDownloadState.Downloaded : NxmDownloadState.Failed;
				item.ErrorCode = item.State == NxmDownloadState.Failed ? "missing-file" : "";
				item.ErrorDetails = String.Empty;
			}
			else if (item.State == NxmDownloadState.Failed && completedPath != null
				&& File.Exists(completedPath) && (item.SizeBytes <= 0 || new FileInfo(completedPath).Length == item.SizeBytes))
			{
				if (String.Equals(item.ErrorCode, "install-failed", StringComparison.Ordinal))
				{
					item.State = NxmDownloadState.InstallFailed;
				}
				else if (String.Equals(item.ErrorCode, "transfer-failed", StringComparison.Ordinal)
					&& partialPath != null && File.Exists(partialPath)
					&& new FileInfo(partialPath).Length == new FileInfo(completedPath).Length)
				{
					item.State = NxmDownloadState.Downloaded;
					item.ErrorCode = String.Empty;
					item.ErrorDetails = String.Empty;
					stalePartialPaths.Add(partialPath);
				}
			}
			item.Progress = item.SizeBytes > 0 ? Math.Clamp((double)item.BytesReceived / item.SizeBytes, 0, 1) : 0;
		}
		await SaveAsync(items, cancellationToken);
		foreach (var partialPath in stalePartialPaths)
		{
			try
			{
				if (File.Exists(partialPath)) File.Delete(partialPath);
				if (File.Exists(partialPath + ".meta")) File.Delete(partialPath + ".meta");
			}
			catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
		}
		return items;
	}

	private string SafePath(string fileName)
	{
		if (String.IsNullOrWhiteSpace(fileName) || fileName != Path.GetFileName(fileName)) return null;
		return Path.Combine(_directory, fileName);
	}

	private static async Task<IReadOnlyList<NxmDownloadItem>> ReadManifestAsync(string path, CancellationToken cancellationToken)
	{
		var json = await File.ReadAllTextAsync(path, cancellationToken);
		var manifest = JsonConvert.DeserializeObject<NxmDownloadManifest>(json)
			?? throw new SerializationException("The downloads manifest is empty.");
		if (manifest.Version != CurrentVersion) throw new SerializationException("The downloads manifest version is unsupported.");
		return Normalize(manifest.Items);
	}

	private static bool IsInvalidManifest(Exception exception) => exception is Newtonsoft.Json.JsonException or SerializationException;

	private static async Task<string> ComputeSha256Async(string path, CancellationToken cancellationToken)
	{
		await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read,
			65536, FileOptions.Asynchronous | FileOptions.SequentialScan);
		return Convert.ToHexString(await System.Security.Cryptography.SHA256.HashDataAsync(stream, cancellationToken))
			.ToLowerInvariant();
	}

	private static void Quarantine(string path, string prefix)
	{
		var quarantinePath = Path.Combine(Path.GetDirectoryName(path)!,
			$"{prefix}.corrupt-{DateTime.UtcNow:yyyyMMddHHmmssfff}-{Guid.NewGuid():N}.json");
		File.Move(path, quarantinePath);
	}

	private static IReadOnlyList<NxmDownloadItem> Normalize(IEnumerable<NxmDownloadItem> items) =>
		(items ?? Enumerable.Empty<NxmDownloadItem>())
		.Where(item => item != null && !String.IsNullOrWhiteSpace(item.Id) && item.ModId > 0 && item.FileId > 0)
		.OrderBy(item => item.QueuePosition)
		.ThenBy(item => item.Id, StringComparer.Ordinal)
		.ToArray();

	[DataContract]
	private sealed class NxmDownloadManifest
	{
		[DataMember(Order = 1)] public int Version { get; set; }
		[DataMember(Order = 2)] public List<NxmDownloadItem> Items { get; set; } = new();
	}
}
