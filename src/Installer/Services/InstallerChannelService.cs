using ReduxInstaller.Models;

using System;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ReduxInstaller.Services;

internal sealed class InstallerChannelService : IDisposable
{
	private readonly HttpClient _client;
	private readonly bool _ownsClient;

	public InstallerChannelService(HttpClient? client = null)
	{
		_client = client ?? CreateClient();
		_ownsClient = client == null;
	}

	public async Task<InstallerReleaseManifest> FetchAsync(CancellationToken cancellationToken)
	{
		using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
		timeout.CancelAfter(TimeSpan.FromSeconds(20));
		using var request = new HttpRequestMessage(HttpMethod.Get, InstallerManifestService.ManifestUrl);
		request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
		using var response = await _client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeout.Token)
			.ConfigureAwait(false);
		response.EnsureSuccessStatusCode();
		if (response.Content.Headers.ContentLength.HasValue
			&& response.Content.Headers.ContentLength.Value > InstallerManifestService.MaximumManifestBytes)
			throw new InvalidDataException("The update manifest is larger than the supported limit.");

		using var stream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false);
		var json = await ReadBoundedAsync(stream, timeout.Token).ConfigureAwait(false);
		return InstallerManifestService.ParseAndValidate(json);
	}

	private static HttpClient CreateClient()
	{
		var client = new HttpClient();
		client.DefaultRequestHeaders.UserAgent.ParseAdd("BG3ModManager-Redux-Setup");
		return client;
	}

	private static async Task<string> ReadBoundedAsync(Stream stream, CancellationToken cancellationToken)
	{
		using var output = new MemoryStream();
		var buffer = new byte[8192];
		while (true)
		{
			var read = await stream.ReadAsync(buffer, 0, buffer.Length, cancellationToken).ConfigureAwait(false);
			if (read == 0) break;
			if (output.Length + read > InstallerManifestService.MaximumManifestBytes)
				throw new InvalidDataException("The update manifest is larger than the supported limit.");
			output.Write(buffer, 0, read);
		}
		return Encoding.UTF8.GetString(output.ToArray());
	}

	public void Dispose()
	{
		if (_ownsClient) _client.Dispose();
	}
}
