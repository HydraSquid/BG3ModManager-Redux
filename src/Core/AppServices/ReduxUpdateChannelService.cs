using DivinityModManager.Models.Updates;

using System.Net.Http.Headers;
using System.Text;

namespace DivinityModManager.AppServices;

/// <summary>
/// Reads Redux's fixed public-alpha channel endpoint without blocking application startup.
/// Parsing, release URL validation, version ordering, and artifact verification remain in
/// <see cref="ReduxUpdateManifestService"/> so the app and standalone web installer can
/// share one release contract.
/// </summary>
public sealed class ReduxUpdateChannelService
{
	public static readonly TimeSpan AutomaticCheckInterval = TimeSpan.FromHours(12);
	public static readonly TimeSpan FailedCheckRetryInterval = TimeSpan.FromHours(1);
	public static readonly Uri PublicAlphaManifestUri = new(
		DivinityApp.URL_REDUX_UPDATE_MANIFEST,
		UriKind.Absolute);

	private static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(15);
	private readonly HttpClient _client;
	private readonly Uri _manifestUri;
	private readonly TimeSpan _timeout;

	public ReduxUpdateChannelService(HttpClient client, Uri manifestUri = null, TimeSpan? timeout = null)
	{
		_client = client ?? throw new ArgumentNullException(nameof(client));
		_manifestUri = manifestUri ?? PublicAlphaManifestUri;
		_timeout = timeout ?? DefaultTimeout;
		if (_timeout <= TimeSpan.Zero)
			throw new ArgumentOutOfRangeException(nameof(timeout), "The update check timeout must be positive.");
		ValidateManifestUri(_manifestUri);
	}

	public async Task<ReduxUpdateDecision> CheckAsync(
		string installedInternalVersion,
		CancellationToken cancellationToken = default)
	{
		using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
		timeout.CancelAfter(_timeout);
		using var request = new HttpRequestMessage(HttpMethod.Get, _manifestUri);
		request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
		using var response = await _client.SendAsync(
			request,
			HttpCompletionOption.ResponseHeadersRead,
			timeout.Token).ConfigureAwait(false);
		response.EnsureSuccessStatusCode();

		if (response.Content.Headers.ContentLength is > ReduxUpdateManifestService.MaximumManifestBytes)
			throw new InvalidDataException("The update manifest is larger than the supported limit.");

		await using var stream = await response.Content.ReadAsStreamAsync(timeout.Token).ConfigureAwait(false);
		var json = await ReadBoundedUtf8Async(stream, timeout.Token).ConfigureAwait(false);
		var manifest = ReduxUpdateManifestService.ParseAndValidate(json);
		return ReduxUpdateManifestService.Evaluate(manifest, installedInternalVersion);
	}

	public static bool IsAutomaticCheckDue(
		long lastSuccessfulCheck,
		long lastCheckAttempt,
		DateTimeOffset now)
	{
		if (!HasElapsed(lastCheckAttempt, now, FailedCheckRetryInterval)) return false;
		return HasElapsed(lastSuccessfulCheck, now, AutomaticCheckInterval);
	}

	private static bool HasElapsed(long timestamp, DateTimeOffset now, TimeSpan interval)
	{
		if (timestamp <= 0) return true;
		try
		{
			var elapsed = now - DateTimeOffset.FromUnixTimeSeconds(timestamp);
			return elapsed < TimeSpan.Zero || elapsed >= interval;
		}
		catch (ArgumentOutOfRangeException)
		{
			return true;
		}
	}

	private static async Task<string> ReadBoundedUtf8Async(Stream stream, CancellationToken cancellationToken)
	{
		var maximum = checked((int)ReduxUpdateManifestService.MaximumManifestBytes);
		using var buffer = new MemoryStream(capacity: Math.Min(maximum, 16 * 1024));
		var chunk = new byte[8192];
		while (true)
		{
			var read = await stream.ReadAsync(chunk.AsMemory(0, chunk.Length), cancellationToken).ConfigureAwait(false);
			if (read == 0) break;
			if (buffer.Length + read > maximum)
				throw new InvalidDataException("The update manifest is larger than the supported limit.");
			buffer.Write(chunk, 0, read);
		}
		return Encoding.UTF8.GetString(buffer.GetBuffer(), 0, checked((int)buffer.Length));
	}

	private static void ValidateManifestUri(Uri uri)
	{
		var valid = uri.IsAbsoluteUri
			&& String.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)
			&& String.Equals(uri.Host, "github.com", StringComparison.OrdinalIgnoreCase)
			&& uri.IsDefaultPort
			&& String.IsNullOrEmpty(uri.UserInfo)
			&& String.IsNullOrEmpty(uri.Query)
			&& String.IsNullOrEmpty(uri.Fragment)
			&& String.Equals(
				uri.AbsolutePath,
				"/circleainn/BG3ModManager-Redux/releases/download/public-alpha/Redux-Update-Public-Alpha.json",
				StringComparison.OrdinalIgnoreCase);
		if (!valid)
			throw new ArgumentException("The update channel must use Redux's official public-alpha manifest URL.", nameof(uri));
	}
}
