using DivinityModManager.Util;
using Newtonsoft.Json;

namespace DivinityModManager.AppServices;

public sealed record NexusUpdateProgress(int CompletedProjects, int TotalProjects, int Requests, int CachedProjects);
public sealed record NexusUpdateRun(IReadOnlyList<NexusModUpdateResult> Results, int Requests,
	int CachedProjects, bool Cancelled, string Message);

/// <summary>Manual, project-deduplicated checks with durable cooldowns and one outstanding request.</summary>
public sealed class NexusModUpdateService
{
	public static readonly TimeSpan CacheLifetime = TimeSpan.FromHours(24);
	public static readonly TimeSpan FailureCooldown = TimeSpan.FromMinutes(15);
	private const int MaximumCacheBytes = 32 * 1024 * 1024;
	private readonly INexusModFilesClient _client;
	private readonly string _path;
	private readonly Func<DateTimeOffset> _now;
	private readonly Func<TimeSpan, CancellationToken, Task> _delay;
	private readonly Action<string, string> _writeCache;
	private readonly SemaphoreSlim _gate = new(1, 1);
	private CheckCache _cache = new();
	private readonly NexusUpdateAcknowledgements _acknowledgements;
	public string CacheWarning { get; private set; }
	public string ReferenceWarning => _acknowledgements.Warning;

	public NexusModUpdateService(INexusModFilesClient client, string cachePath,
		Func<DateTimeOffset> now = null, Func<TimeSpan, CancellationToken, Task> delay = null,
		Action<string, string> writeCache = null)
	{
		_client = client ?? throw new ArgumentNullException(nameof(client));
		_path = Path.GetFullPath(cachePath);
		_now = now ?? (() => DateTimeOffset.UtcNow);
		_delay = delay ?? ((duration, token) => Task.Delay(duration, token));
		_writeCache = writeCache ?? ((path, json) => AtomicFileWriter.WriteAllText(path, json, path + ".bak"));
		_acknowledgements = new NexusUpdateAcknowledgements(_path + ".references.json");
		Load();
	}

	public IReadOnlyList<NexusModUpdateResult> GetCachedResults(IEnumerable<NexusInstalledFile> installed)
	{
		var references = _acknowledgements.Read();
		return installed.Select(file => CachedResult(file, references)).OrderBy(result => result.Status).ThenBy(result => result.Name).ToArray();
	}

	private NexusModUpdateResult CachedResult(NexusInstalledFile file, IReadOnlyList<NexusUpdateAcknowledgement> references)
	{
		var reference = NexusUpdateAcknowledgements.Find(references, file);
		if (!_cache.Projects.TryGetValue(file.ModId, out var entry)) return NexusModUpdateEvaluator.Evaluate(file, null) with { Acknowledgement = reference };
		if (!String.IsNullOrEmpty(entry.Error))
			return new(file, NexusModUpdateStatus.CheckFailed, entry.Error + $" Retry after {entry.NextAllowedUtc.LocalDateTime:g}.",
				CheckedUtc: entry.CheckedUtc, FromCache: true) { Acknowledgement = reference };
		if (file.HasExactFileIdentity && NexusUpdateAcknowledgements.IsHash(file.PackageSha256) && String.IsNullOrEmpty(file.DownloadVersion))
		{
			var recorded = entry.Data?.Files.SingleOrDefault(remote => remote.FileId == file.FileId);
			if (recorded != null) file = file with { DownloadVersion = recorded.Version };
		}
		var comparison = reference == null ? file : file with { FileId = reference.FileId, HasExactFileIdentity = true };
		var result = NexusModUpdateEvaluator.Evaluate(comparison, entry.Data, entry.CheckedUtc, true) with
		{
			Installed = file, Acknowledgement = reference,
			AvailableFiles = entry.Data?.Files.Where(remote => remote.CategoryId is 1 or 2 or 3 or 4 or 7)
				.OrderBy(remote => remote.Name).ThenBy(remote => remote.FileId).ToArray() ?? Array.Empty<NexusRemoteFile>(),
			CanChooseReference = NexusUpdateAcknowledgements.IsHash(file.PackageSha256) && entry.CheckedUtc > _now() - CacheLifetime
		};
		if (reference != null)
			result = result with { Status = result.Status == NexusModUpdateStatus.NoUpdateReported ? NexusModUpdateStatus.Acknowledged : result.Status,
				Reason = "Compared from your chosen reference release. " + result.Reason };
		if (entry.CheckedUtc <= _now() - CacheLifetime)
			result = result with { Status = NexusModUpdateStatus.NotChecked,
				Reason = "This cached check is older than 24 hours. Run a check for a current result. " + result.Reason };
		return result;
	}

	public async Task SetReferenceAsync(NexusInstalledFile installed, long fileId, CancellationToken cancellationToken = default)
	{
		await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
		try
		{
			if (!_cache.Projects.TryGetValue(installed.ModId, out var entry) || !String.IsNullOrEmpty(entry.Error)
				|| entry.CheckedUtc <= _now() - CacheLifetime || entry.CheckedUtc == null || entry.Data == null)
				throw new InvalidOperationException("Check this project's file list before choosing a reference.");
			var file = entry.Data.Files.SingleOrDefault(candidate => candidate.FileId == fileId && candidate.CategoryId is 1 or 2 or 3 or 4 or 7)
				?? throw new InvalidOperationException("Choose a file from this project's checked list.");
			await using var package = new FileStream(installed.FilePath, FileMode.Open, FileAccess.Read, FileShare.Read,
				65536, FileOptions.Asynchronous | FileOptions.SequentialScan);
			var hash = Convert.ToHexString(await System.Security.Cryptography.SHA256.HashDataAsync(package, cancellationToken).ConfigureAwait(false));
			if (!String.Equals(hash, installed.PackageSha256, StringComparison.OrdinalIgnoreCase))
				throw new InvalidOperationException("The installed package changed. Recheck before choosing a reference.");
			cancellationToken.ThrowIfCancellationRequested();
			_acknowledgements.Set(installed, file);
		}
		finally { _gate.Release(); }
	}

	public void ResetReference(NexusInstalledFile installed) => _acknowledgements.Reset(installed);

	public async Task<NexusUpdateRun> CheckAsync(IReadOnlyList<NexusInstalledFile> installed, string apiKey,
		Func<bool> checksAllowed, IProgress<NexusUpdateProgress> progress = null, CancellationToken cancellationToken = default)
	{
		if (!await _gate.WaitAsync(0, cancellationToken).ConfigureAwait(false))
			throw new InvalidOperationException("A Nexus update check is already running.");
		var requests = 0;
		var cached = 0;
		var cancelled = false;
		var message = String.Empty;
		var freshProjects = new HashSet<long>();
		try
		{
			if (String.IsNullOrWhiteSpace(apiKey) || !checksAllowed())
				return new(GetCachedResults(installed), 0, 0, false, "Enable online mod information and configure your Nexus API key in Preferences.");
			Directory.CreateDirectory(Path.GetDirectoryName(_path));
			// A second window/process cannot start another scan against the same cache.
			using var lease = new FileStream(_path + ".lock", FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
			Load();
			var groups = installed.Where(file => file.ModId > 0)
				.GroupBy(file => file.ModId).ToArray();
			var complete = 0;
			progress?.Report(new(complete, groups.Length, requests, cached));
			foreach (var group in groups)
			{
				cancellationToken.ThrowIfCancellationRequested();
				if (!checksAllowed()) { message = "Checks stopped because provider settings changed."; break; }
				if (_cache.Projects.TryGetValue(group.Key, out var previous) && previous.NextAllowedUtc > _now())
				{
					cached++;
					progress?.Report(new(++complete, groups.Length, requests, cached));
					continue;
				}
				if (_cache.RetryAfterUtc > _now())
				{
					message = $"Nexus checks are paused until {_cache.RetryAfterUtc.LocalDateTime:g}. Cached results remain available.";
					// Continue to expose/count later cached projects, without sending requests.
					progress?.Report(new(++complete, groups.Length, requests, cached));
					continue;
				}
				var delay = _cache.NextRequestUtc - _now();
				if (delay > TimeSpan.Zero) await _delay(delay, cancellationToken).ConfigureAwait(false);
				cancellationToken.ThrowIfCancellationRequested();
				if (!checksAllowed()) { message = "Checks stopped because provider settings changed."; break; }
				var entry = previous ?? new ProjectCheck();
				entry.NextAllowedUtc = _now() + FailureCooldown;
				entry.Error = "The last check did not complete.";
				_cache.Projects[group.Key] = entry;
				_cache.NextRequestUtc = _now().AddSeconds(1);
				// Persist BEFORE requesting so a crash/reopen cannot immediately repeat requests.
				Save();
				// A slow durable write must not consume the interval between actual request starts.
				_cache.NextRequestUtc = _now().AddSeconds(1);
				requests++;
				NexusFilesResponse response;
				try { response = await _client.FetchAsync(group.Key, apiKey, cancellationToken).ConfigureAwait(false); }
				catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
				{
					// Keep actual-start pacing across cancellation/reopen, before releasing the lease.
					Save();
					throw;
				}
				if (response.Data != null)
				{
					entry.Data = response.Data;
					entry.CheckedUtc = _now();
					entry.NextAllowedUtc = _now() + CacheLifetime;
					entry.Error = null;
					freshProjects.Add(group.Key);
				}
				else
				{
					entry.Error = response.Error ?? "The Nexus check failed.";
					entry.NextAllowedUtc = response.RetryAfterUtc > _now() ? response.RetryAfterUtc.Value : _now() + FailureCooldown;
				}
				if (response.StopChecks)
				{
					_cache.RetryAfterUtc = response.RetryAfterUtc > _now() ? response.RetryAfterUtc.Value : _now() + FailureCooldown;
					message = response.Error ?? $"Nexus request budget is low. Checks paused until {_cache.RetryAfterUtc.LocalDateTime:g}.";
				}
				Save();
				progress?.Report(new(++complete, groups.Length, requests, cached));
			}
			if (String.IsNullOrEmpty(message)) message = groups.Length == 0
				? "No linked Nexus projects to check."
				: $"Check complete: {requests} Nexus request(s), {cached} cached project(s).";
		}
		catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
		{
			cancelled = true;
			message = "Check cancelled. Completed checks are retained; unfinished requests are cooled down.";
		}
		catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException or Newtonsoft.Json.JsonException)
		{
			message = "Checks stopped: the cache is unavailable, locked by another check, or could not be saved. Completed results may be partial.";
		}
		finally { _gate.Release(); }
		var results = GetCachedResults(installed).Select(result => result with { FromCache = !freshProjects.Contains(result.Installed.ModId) }).ToArray();
		return new(results, requests, cached, cancelled, message);
	}

	private void Load()
	{
		CacheWarning = null;
		try
		{
			if (!File.Exists(_path)) { _cache = new(); return; }
			if (new FileInfo(_path).Length > MaximumCacheBytes) throw new InvalidDataException();
			var root = Newtonsoft.Json.Linq.JObject.Parse(File.ReadAllText(_path), new Newtonsoft.Json.Linq.JsonLoadSettings
				{ DuplicatePropertyNameHandling = Newtonsoft.Json.Linq.DuplicatePropertyNameHandling.Error });
			var cache = root.ToObject<CheckCache>();
			if (cache?.SchemaVersion != 1 || cache.Projects == null || cache.Projects.Count > 4096
				|| cache.NextRequestUtc > _now().AddDays(2)) throw new InvalidDataException();
			foreach (var pair in cache.Projects)
			{
				var entry = pair.Value;
				if (pair.Key <= 0 || entry == null || entry.CheckedUtc > _now().AddMinutes(5)) throw new InvalidDataException();
				if (entry.Data is { } data && (!entry.CheckedUtc.HasValue || entry.CheckedUtc < DateTimeOffset.UnixEpoch
					|| data.Files == null || data.Replacements == null
					|| data.Files.Count > 10000 || data.Replacements.Count > 20000
					|| data.Files.Any(file => file == null || file.FileId <= 0 || file.Name?.Length > 500 || file.Version?.Length > 500)
					|| data.Files.Select(file => file.FileId).Distinct().Count() != data.Files.Count
					|| data.Replacements.Any(edge => edge == null || edge.OldFileId <= 0 || edge.NewFileId <= 0))) throw new InvalidDataException();
			}
			_cache = cache;
		}
		catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException or Newtonsoft.Json.JsonException)
		{
			_cache = new();
			CacheWarning = "Previous update-check cache could not be read. Only a manual check will contact Nexus.";
		}
	}

	private void Save()
	{
		var json = JsonConvert.SerializeObject(_cache);
		if (System.Text.Encoding.UTF8.GetByteCount(json) > MaximumCacheBytes) throw new IOException("Check cache is full.");
		_writeCache(_path, json);
	}

	private sealed class CheckCache
	{
		public int SchemaVersion { get; set; } = 1;
		public DateTimeOffset NextRequestUtc { get; set; }
		public DateTimeOffset RetryAfterUtc { get; set; }
		public Dictionary<long, ProjectCheck> Projects { get; set; } = new();
	}
	private sealed class ProjectCheck
	{
		public DateTimeOffset? CheckedUtc { get; set; }
		public DateTimeOffset NextAllowedUtc { get; set; }
		public string Error { get; set; }
		public NexusProjectFiles Data { get; set; }
	}
}
