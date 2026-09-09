using DivinityModManager.AppServices;
using DivinityModManager.Models.Updates;

using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Redux.Core.Tests;

public sealed class ReduxUpdateChannelServiceTests
{
	public void FetchesAndEvaluatesTheOfficialChannelManifest()
	{
		using var client = new HttpClient(new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
		{
			Content = new StringContent(ValidManifest, Encoding.UTF8, "application/json")
		}));
		var service = new ReduxUpdateChannelService(client);

		var result = service.CheckAsync("0.1.0.14").GetAwaiter().GetResult();

		RegressionAssert.Equal(ReduxUpdateAvailability.UpdateAvailable, result.Availability);
		RegressionAssert.Equal("0.1.0-alpha.15", result.Manifest.DisplayVersion);
		RegressionAssert.Equal(ReduxUpdateArtifactKinds.Portable, result.Artifact.Kind);
	}

	public void RejectsOversizedManifestBeforeReadingItsBody()
	{
		using var client = new HttpClient(new StubHandler(_ =>
		{
			var response = new HttpResponseMessage(HttpStatusCode.OK)
			{
				Content = new StringContent("{}")
			};
			response.Content.Headers.ContentLength = ReduxUpdateManifestService.MaximumManifestBytes + 1;
			return response;
		}));
		var service = new ReduxUpdateChannelService(client);

		RegressionAssert.Throws<InvalidDataException>(() =>
			service.CheckAsync("0.1.0.14").GetAwaiter().GetResult());
	}

	public void RejectsUntrustedChannelEndpoint()
	{
		using var client = new HttpClient(new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)));

		RegressionAssert.Throws<ArgumentException>(() =>
			_ = new ReduxUpdateChannelService(client, new Uri("https://example.com/update.json")));
	}

	public void AutomaticChecksUseTheLastSuccessfulCheckTime()
	{
		var now = DateTimeOffset.FromUnixTimeSeconds(2_000_000_000);

		RegressionAssert.True(ReduxUpdateChannelService.IsAutomaticCheckDue(0, 0, now));
		RegressionAssert.False(ReduxUpdateChannelService.IsAutomaticCheckDue(
			now.AddHours(-11).ToUnixTimeSeconds(), 0, now));
		RegressionAssert.True(ReduxUpdateChannelService.IsAutomaticCheckDue(
			now.AddHours(-12).ToUnixTimeSeconds(), 0, now));
		RegressionAssert.True(ReduxUpdateChannelService.IsAutomaticCheckDue(
			now.AddHours(1).ToUnixTimeSeconds(), 0, now));
	}

	public void FailedAutomaticChecksBackOffBeforeRetrying()
	{
		var now = DateTimeOffset.FromUnixTimeSeconds(2_000_000_000);

		RegressionAssert.False(ReduxUpdateChannelService.IsAutomaticCheckDue(
			0, now.AddMinutes(-59).ToUnixTimeSeconds(), now));
		RegressionAssert.True(ReduxUpdateChannelService.IsAutomaticCheckDue(
			0, now.AddHours(-1).ToUnixTimeSeconds(), now));
	}

	private const string ValidManifest = """
		{
		  "schemaVersion": 1,
		  "channel": "public-alpha",
		  "displayVersion": "0.1.0-alpha.15",
		  "internalVersion": "0.1.0.15",
		  "publishedAtUtc": "2026-09-08T16:30:00Z",
		  "releaseNotesUrl": "https://github.com/circleainn/BG3ModManager-Redux/releases/tag/v0.1.0-alpha.15",
		  "artifacts": [
		    {
		      "kind": "portable",
		      "url": "https://github.com/circleainn/BG3ModManager-Redux/releases/download/v0.1.0-alpha.15/BG3ModManager-Redux_v0.1.0-alpha.15.zip",
		      "sizeBytes": 8192,
		      "sha256": "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb"
		    }
		  ]
		}
		""";

	private sealed class StubHandler : HttpMessageHandler
	{
		private readonly Func<HttpRequestMessage, HttpResponseMessage> _response;

		public StubHandler(Func<HttpRequestMessage, HttpResponseMessage> response) => _response = response;

		protected override Task<HttpResponseMessage> SendAsync(
			HttpRequestMessage request,
			CancellationToken cancellationToken) => Task.FromResult(_response(request));
	}
}
