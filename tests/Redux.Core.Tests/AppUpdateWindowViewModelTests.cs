using DivinityModManager.AppServices;
using DivinityModManager.Models.Updates;
using DivinityModManager.ViewModels;

using System;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Redux.Core.Tests;

public sealed class AppUpdateWindowViewModelTests
{
	public void AvailableUpdateOffersVerifiedRestart()
	{
		using var client = ClientReturning(HttpStatusCode.OK, Manifest("0.1.0-alpha.17", "0.1.0.17"));
		var viewModel = CreateViewModel(client);

		viewModel.CheckForUpdatesAsync().GetAwaiter().GetResult();

		RegressionAssert.Equal(ReduxUpdateCheckState.UpdateAvailable, viewModel.CheckState);
		RegressionAssert.True(viewModel.IsVisible);
		RegressionAssert.True(viewModel.CanConfirm);
		RegressionAssert.True(viewModel.HasAvailableUpdate);
		RegressionAssert.Equal("Update & Restart", viewModel.ConfirmButtonText);
		RegressionAssert.False(viewModel.IsChecking);
		RegressionAssert.Contains(viewModel.UpdateDescription, "0.1.0-alpha.17");
		RegressionAssert.Contains(viewModel.UpdateChangelogView, "official release notes");
	}

	public void CurrentReleaseStaysQuietDuringAutomaticCheck()
	{
		using var client = ClientReturning(HttpStatusCode.OK, Manifest("0.1.0-alpha.16", "0.1.0.16"));
		var viewModel = CreateViewModel(client);

		viewModel.CheckForUpdatesAsync(showAlerts: false).GetAwaiter().GetResult();

		RegressionAssert.Equal(ReduxUpdateCheckState.UpToDate, viewModel.CheckState);
		RegressionAssert.False(viewModel.IsVisible);
		RegressionAssert.False(viewModel.CanConfirm);
		RegressionAssert.False(viewModel.HasAvailableUpdate);
		RegressionAssert.True(viewModel.CanSkip);
	}

	public void ManualFailureExplainsThatTheInstallationWasNotChanged()
	{
		using var client = ClientReturning(HttpStatusCode.ServiceUnavailable, "unavailable");
		var viewModel = CreateViewModel(client);

		viewModel.CheckForUpdatesAsync(showAlerts: true).GetAwaiter().GetResult();

		RegressionAssert.Equal(ReduxUpdateCheckState.Failed, viewModel.CheckState);
		RegressionAssert.True(viewModel.IsVisible);
		RegressionAssert.False(viewModel.CanConfirm);
		RegressionAssert.False(viewModel.HasAvailableUpdate);
		RegressionAssert.Contains(viewModel.UpdateDescription, "not changed");
	}

	private static HttpClient ClientReturning(HttpStatusCode statusCode, string content) =>
		new(new StubHandler(_ => new HttpResponseMessage(statusCode)
		{
			Content = new StringContent(content, Encoding.UTF8, "application/json")
		}));

	private static AppUpdateWindowViewModel CreateViewModel(HttpClient client) => new(
		new ReduxUpdateChannelService(client),
		new ReduxUpdatePackageService(client),
		new ReduxUpdateLaunchService());

	private static string Manifest(string displayVersion, string internalVersion) => $$"""
		{
		  "schemaVersion": 1,
		  "channel": "public-alpha",
		  "displayVersion": "{{displayVersion}}",
		  "internalVersion": "{{internalVersion}}",
		  "publishedAtUtc": "2026-09-08T16:30:00Z",
		  "releaseNotesUrl": "https://github.com/circleainn/BG3ModManager-Redux/releases/tag/v{{displayVersion}}",
		  "artifacts": [
		    {
		      "kind": "{{ReduxUpdateArtifactKinds.Portable}}",
		      "url": "https://github.com/circleainn/BG3ModManager-Redux/releases/download/v{{displayVersion}}/BG3ModManager-Redux_v{{displayVersion}}.zip",
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
