using DivinityModManager.Models.NexusMods;

using NexusModsNET;

namespace DivinityModManager.AppServices;

public interface INexusNxmApi
{
	Task<NxmApiUser> GetUserAsync(CancellationToken cancellationToken);
	Task<NxmApiMod> GetModAsync(long modId, CancellationToken cancellationToken);
	Task<NxmApiFile> GetFileAsync(long modId, long fileId, CancellationToken cancellationToken);
	Task<IReadOnlyList<Uri>> GetDownloadLinksAsync(long modId, long fileId, string key, long? expires, CancellationToken cancellationToken);
}

public interface INexusNxmResolver
{
	Task<NxmDownloadDescriptor> ResolveMetadataAsync(NexusModManagerLink link, CancellationToken cancellationToken);
	Task<Uri> ResolveDownloadUriAsync(NexusModManagerLink link, CancellationToken cancellationToken);
}

public sealed class NexusNxmResolver : INexusNxmResolver
{
	private readonly INexusNxmApi _api;
	private readonly Func<DateTimeOffset> _now;

	public NexusNxmResolver(INexusNxmApi api, Func<DateTimeOffset> now = null)
	{
		_api = api ?? throw new ArgumentNullException(nameof(api));
		_now = now ?? (() => DateTimeOffset.UtcNow);
	}

	public Task<NxmDownloadDescriptor> ResolveMetadataAsync(NexusModManagerLink link, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(link);
		return ExecuteAsync(async () =>
		{
			var user = await _api.GetUserAsync(cancellationToken) ?? throw new NxmResolutionException(
				NxmResolutionFailureKind.ServiceUnavailable, "Nexus Mods did not return account information.");

			var modTask = _api.GetModAsync(link.ModId, cancellationToken);
			var fileTask = _api.GetFileAsync(link.ModId, link.FileId, cancellationToken);
			await Task.WhenAll(modTask, fileTask);
			var mod = await modTask ?? throw new NxmResolutionException(NxmResolutionFailureKind.NotFound, "The requested Nexus mod was not found.");
			var file = await fileTask ?? throw new NxmResolutionException(NxmResolutionFailureKind.NotFound, "The requested Nexus file was not found.");
			if (mod.ModId != link.ModId || file.FileId != link.FileId)
				throw new NxmResolutionException(NxmResolutionFailureKind.NotFound, "Nexus Mods returned metadata for a different file.");

			return new NxmDownloadDescriptor(link.ModId, link.FileId, mod.Name, mod.Author, file.Name,
				file.FileName, file.Version, file.SizeBytes, !user.IsPremium, mod.ThumbnailUrl);
		});
	}

	public Task<Uri> ResolveDownloadUriAsync(NexusModManagerLink link, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(link);
		return ExecuteAsync(async () =>
		{
			var user = await _api.GetUserAsync(cancellationToken) ?? throw new NxmResolutionException(
				NxmResolutionFailureKind.ServiceUnavailable, "Nexus Mods did not return account information.");
			string key = null;
			long? expires = null;
			if (!user.IsPremium)
			{
				if (String.IsNullOrWhiteSpace(link.DownloadKey) || link.ExpiresUnixSeconds == null)
					throw new NxmResolutionException(NxmResolutionFailureKind.FreshLinkRequired,
						"A fresh Mod Manager Download link is required for this Nexus Mods account.");
				if (link.ExpiresUnixSeconds <= _now().ToUnixTimeSeconds())
					throw new NxmResolutionException(NxmResolutionFailureKind.ExpiredAuthorization,
						"The Nexus download authorization has expired. Request a new Mod Manager Download link.");
				if (link.UserId != null && link.UserId != user.UserId)
					throw new NxmResolutionException(NxmResolutionFailureKind.AccountMismatch,
						"This Nexus download link belongs to a different account.");
				key = link.DownloadKey;
				expires = link.ExpiresUnixSeconds;
			}
			var links = await _api.GetDownloadLinksAsync(link.ModId, link.FileId, key, expires, cancellationToken);
			return links?.FirstOrDefault(uri => uri != null && uri.Scheme == Uri.UriSchemeHttps)
				?? throw new NxmResolutionException(NxmResolutionFailureKind.ServiceUnavailable,
					"Nexus Mods did not return a secure download location.");
		});
	}

	private async Task<T> ExecuteAsync<T>(Func<Task<T>> operation)
	{
		try { return await operation(); }
		catch (Exception ex) when (ex is not (OperationCanceledException or NxmResolutionException))
		{
			if (ex is HttpRequestException { StatusCode: System.Net.HttpStatusCode.TooManyRequests })
				throw new NxmResolutionException(NxmResolutionFailureKind.RateLimited,
					"Nexus Mods rate-limited this request.", ex, _now().AddMinutes(1));
			throw new NxmResolutionException(NxmResolutionFailureKind.ServiceUnavailable,
				"Nexus Mods could not resolve this download. Check the API key, rate limits, and network connection.", ex);
		}
	}
}

public sealed class ScheduledNxmApi : INexusNxmApi
{
	private readonly INexusNxmApi _inner;
	private readonly NxmDownloadScheduler _scheduler;

	public ScheduledNxmApi(INexusNxmApi inner, NxmDownloadScheduler scheduler)
	{
		_inner = inner ?? throw new ArgumentNullException(nameof(inner));
		_scheduler = scheduler ?? throw new ArgumentNullException(nameof(scheduler));
	}

	public Task<NxmApiUser> GetUserAsync(CancellationToken cancellationToken) => Run(() => _inner.GetUserAsync(cancellationToken), cancellationToken);
	public Task<NxmApiMod> GetModAsync(long modId, CancellationToken cancellationToken) => Run(() => _inner.GetModAsync(modId, cancellationToken), cancellationToken);
	public Task<NxmApiFile> GetFileAsync(long modId, long fileId, CancellationToken cancellationToken) => Run(() => _inner.GetFileAsync(modId, fileId, cancellationToken), cancellationToken);
	public Task<IReadOnlyList<Uri>> GetDownloadLinksAsync(long modId, long fileId, string key, long? expires, CancellationToken cancellationToken) =>
		Run(() => _inner.GetDownloadLinksAsync(modId, fileId, key, expires, cancellationToken), cancellationToken);

	private async Task<T> Run<T>(Func<Task<T>> operation, CancellationToken cancellationToken)
	{
		T result = default;
		await _scheduler.Enqueue(async () =>
		{
			try { result = await operation(); }
			catch (NxmResolutionException ex) when (ex.Kind == NxmResolutionFailureKind.RateLimited)
			{
				_scheduler.SuspendUntil(ex.RetryAfter ?? DateTimeOffset.UtcNow.AddMinutes(1));
				throw;
			}
		}, cancellationToken);
		return result;
	}
}

public sealed class NexusModsNetNxmApi : INexusNxmApi
{
	private readonly InfosInquirer _inquirer;

	public NexusModsNetNxmApi(INexusModsClient client)
	{
		_inquirer = new InfosInquirer(client ?? throw new ArgumentNullException(nameof(client)));
	}

	public Task<NxmApiUser> GetUserAsync(CancellationToken cancellationToken) => Execute(async () =>
	{
		var user = await _inquirer.User.GetUserAsync(cancellationToken);
		return user == null ? null : new NxmApiUser(user.UserId, user.IsPremium);
	});

	public Task<NxmApiMod> GetModAsync(long modId, CancellationToken cancellationToken) => Execute(async () =>
	{
		var mod = await _inquirer.Mods.GetMod(DivinityApp.NEXUSMODS_GAME_DOMAIN, modId, cancellationToken);
		var picture = mod?.PictureUrl;
		var thumbnailUrl = picture != null && picture.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)
			? picture.AbsoluteUri
			: String.Empty;
		return mod == null ? null : new NxmApiMod(mod.ModId, mod.Name, mod.Author, thumbnailUrl);
	});

	public Task<NxmApiFile> GetFileAsync(long modId, long fileId, CancellationToken cancellationToken) => Execute(async () =>
	{
		var file = await _inquirer.ModFiles.GetModFileAsync(DivinityApp.NEXUSMODS_GAME_DOMAIN, modId, fileId, cancellationToken);
		var reportedKilobytes = file?.Size > 0 ? file.Size : file?.SizeKb ?? 0;
		var size = reportedKilobytes > 0 && reportedKilobytes <= Int64.MaxValue / 1024
			? reportedKilobytes * 1024 : 0;
		return file == null ? null : new NxmApiFile(file.FileId, file.Name, file.FileName, file.Version, size);
	});

	public Task<IReadOnlyList<Uri>> GetDownloadLinksAsync(long modId, long fileId, string key, long? expires, CancellationToken cancellationToken) => Execute(async () =>
	{
		var links = key == null
			? await _inquirer.ModFiles.GetModFileDownloadLinksAsync(DivinityApp.NEXUSMODS_GAME_DOMAIN, modId, fileId, cancellationToken)
			: await _inquirer.ModFiles.GetModFileDownloadLinksAsync(DivinityApp.NEXUSMODS_GAME_DOMAIN, modId, fileId, key, expires!.Value, cancellationToken);
		return (IReadOnlyList<Uri>)(links?.Select(link => link.Uri).Where(uri => uri != null).ToArray() ?? Array.Empty<Uri>());
	});

	private async Task<T> Execute<T>(Func<Task<T>> operation)
	{
		try { return await operation(); }
		catch (Exception ex) when (ex is NexusModsNET.Exceptions.LimitsExceededException
			or HttpRequestException { StatusCode: System.Net.HttpStatusCode.TooManyRequests })
		{
			var limits = _inquirer.RateLimitsManagement.APILimits;
			var resets = new List<DateTime>();
			if (limits.HourlyRemaining <= 0) resets.Add(limits.HourlyReset);
			if (limits.DailyRemaining <= 0) resets.Add(limits.DailyReset);
			var retryAfter = resets.Count == 0
				? DateTimeOffset.UtcNow.AddMinutes(1)
				: new DateTimeOffset(resets.Max().ToUniversalTime());
			throw new NxmResolutionException(NxmResolutionFailureKind.RateLimited,
				"Nexus Mods rate-limited this request.", ex, retryAfter);
		}
	}
}
