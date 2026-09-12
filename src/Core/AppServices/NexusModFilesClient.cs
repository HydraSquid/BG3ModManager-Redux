using System.Globalization;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;

using Newtonsoft.Json.Linq;

namespace DivinityModManager.AppServices;

public sealed record NexusFilesResponse(NexusProjectFiles Data, string Error = null,
	DateTimeOffset? RetryAfterUtc = null, bool StopChecks = false);

public interface INexusModFilesClient
{
	Task<NexusFilesResponse> FetchAsync(long projectId, string apiKey, CancellationToken cancellationToken);
}

/// <summary>Reads only public file metadata. Credentials never enter URLs, results, caches, or error messages.</summary>
public sealed class NexusModFilesClient : INexusModFilesClient, IDisposable
{
	private readonly HttpClient _client;
	private readonly bool _ownsClient;
	public NexusModFilesClient(HttpClient client = null)
	{
		_ownsClient = client == null;
		_client = client ?? new HttpClient(new HttpClientHandler { AllowAutoRedirect = false });
	}

	public async Task<NexusFilesResponse> FetchAsync(long projectId, string apiKey, CancellationToken cancellationToken)
	{
		if (projectId <= 0 || String.IsNullOrWhiteSpace(apiKey))
			return new(null, "A linked Nexus project and API key are required.", StopChecks: true);
		using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
		timeout.CancelAfter(TimeSpan.FromSeconds(30));
		try
		{
			using var request = new HttpRequestMessage(HttpMethod.Get,
				$"https://api.nexusmods.com/v1/games/baldursgate3/mods/{projectId}/files.json");
			request.Headers.Add("apikey", apiKey);
			request.Headers.Add("Application-Name", "BG3ModManager-Redux");
			request.Headers.Add("Application-Version", typeof(NexusModFilesClient).Assembly.GetName().Version?.ToString() ?? "0.0.0");
			request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
			using var response = await _client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeout.Token).ConfigureAwait(false);
			var now = DateTimeOffset.UtcNow;
			var retry = response.Headers.RetryAfter?.Date ?? (response.Headers.RetryAfter?.Delta is TimeSpan delta ? now + delta : (DateTimeOffset?)null);
			DateTimeOffset? budgetReset = null;
			foreach (var period in new[] { "hourly", "daily" })
			{
				if (response.Headers.TryGetValues($"x-rl-{period}-remaining", out var remaining)
					&& Int64.TryParse(remaining.FirstOrDefault(), out var value) && value <= 5)
				{
					var reset = now + (period == "hourly" ? TimeSpan.FromHours(1) : TimeSpan.FromDays(1));
					if (response.Headers.TryGetValues($"x-rl-{period}-reset", out var resetValues)
						&& DateTimeOffset.TryParse(resetValues.FirstOrDefault(), CultureInfo.InvariantCulture,
							DateTimeStyles.AssumeUniversal, out var parsed) && parsed > now) reset = parsed;
					if (!budgetReset.HasValue || reset > budgetReset) budgetReset = reset;
				}
			}
			if (response.StatusCode == HttpStatusCode.TooManyRequests)
				return new(null, "Nexus rate limit reached. Checks are paused until the retry time.",
					Later(now.AddMinutes(15), Later(retry, budgetReset)), true);
			if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
				return new(null, "Nexus denied access. Check your API key and account access in Preferences.", now.AddMinutes(15), true);
			if (!response.IsSuccessStatusCode)
				return new(null, $"Nexus returned HTTP {(int)response.StatusCode}. No update conclusion was made.",
					Later(now.AddMinutes(15), retry), response.StatusCode != HttpStatusCode.NotFound);

			const int maximumBytes = 8 * 1024 * 1024;
			if (response.Content.Headers.ContentLength > maximumBytes) throw new InvalidDataException();
			await using var stream = await response.Content.ReadAsStreamAsync(timeout.Token).ConfigureAwait(false);
			using var buffer = new MemoryStream();
			var chunk = new byte[16384];
			int read;
			while ((read = await stream.ReadAsync(chunk, timeout.Token).ConfigureAwait(false)) > 0)
			{
				if (buffer.Length + read > maximumBytes) throw new InvalidDataException();
				buffer.Write(chunk, 0, read);
			}
			var data = Parse(System.Text.Encoding.UTF8.GetString(buffer.ToArray()));
			return new(data, RetryAfterUtc: budgetReset, StopChecks: budgetReset.HasValue);
		}
		catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
		catch (Exception ex) when (ex is HttpRequestException or OperationCanceledException or IOException or InvalidDataException
			or Newtonsoft.Json.JsonException or FormatException or ArgumentException or OverflowException)
		{
			return new(null, "The Nexus file check failed or timed out. No update conclusion was made; retry is delayed.",
				DateTimeOffset.UtcNow.AddMinutes(15), true);
		}
	}

	private static DateTimeOffset? Later(DateTimeOffset? left, DateTimeOffset? right) =>
		!left.HasValue ? right : !right.HasValue ? left : left > right ? left : right;

	public static NexusProjectFiles Parse(string json)
	{
		var root = JObject.Parse(json, new JsonLoadSettings { DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error });
		if (root["files"] is not JArray files || root["file_updates"] is not JArray updates
			|| files.Count > 10000 || updates.Count > 20000
			|| files.Any(file => file is not JObject) || updates.Any(update => update is not JObject)) throw new InvalidDataException();
		long Id(JToken value) => value?.Type == JTokenType.Integer && value.Value<long>() > 0
			? value.Value<long>() : throw new InvalidDataException();
		string Text(JToken value) => value?.Type == JTokenType.String ? value.Value<string>()[..Math.Min(value.Value<string>().Length, 500)] : String.Empty;
		var parsedFiles = files.Select(file => new NexusRemoteFile(Id(file["file_id"]), Text(file["name"]),
			Text(file["version"]), file["category_id"]?.Type == JTokenType.Integer ? file["category_id"].Value<int>() : 0)).ToList();
		if (parsedFiles.Select(file => file.FileId).Distinct().Count() != parsedFiles.Count) throw new InvalidDataException();
		return new(parsedFiles, updates.Select(update => new NexusFileReplacement(Id(update["old_file_id"]), Id(update["new_file_id"]))).ToList());
	}

	public void Dispose() { if (_ownsClient) _client.Dispose(); }
}
