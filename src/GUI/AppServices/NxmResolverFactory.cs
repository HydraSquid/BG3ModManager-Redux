using DivinityModManager.AppServices;
using DivinityModManager.Models.NexusMods;

using NexusModsNET;

namespace DivinityModManager.GUI.AppServices;

public sealed class NxmResolverFactory : INxmResolverFactory
{
	private readonly Func<string> _apiKey;
	private readonly Func<string> _appName;
	private readonly Func<string> _appVersion;
	private readonly NxmDownloadScheduler _apiScheduler = new(6);

	public NxmResolverFactory(Func<string> apiKey, Func<string> appName, Func<string> appVersion)
	{
		_apiKey = apiKey;
		_appName = appName;
		_appVersion = appVersion;
	}

	public async Task<NxmDownloadDescriptor> ResolveMetadataAsync(NexusModManagerLink link, CancellationToken cancellationToken)
	{
		using var client = NexusModsClient.Create(_apiKey(), _appName(), _appVersion());
		return await new NexusNxmResolver(new ScheduledNxmApi(new NexusModsNetNxmApi(client), _apiScheduler))
			.ResolveMetadataAsync(link, cancellationToken);
	}

	public async Task<Uri> ResolveDownloadUriAsync(NexusModManagerLink link, CancellationToken cancellationToken)
	{
		using var client = NexusModsClient.Create(_apiKey(), _appName(), _appVersion());
		return await new NexusNxmResolver(new ScheduledNxmApi(new NexusModsNetNxmApi(client), _apiScheduler))
			.ResolveDownloadUriAsync(link, cancellationToken);
	}
}
