using DivinityModManager.AppServices;
using DivinityModManager.Models;
using DivinityModManager.Models.NexusMods;
using DivinityModManager.Util;
using DivinityModManager.Views;
using DivinityModManager.ViewModels;
using DynamicData;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using System.Windows.Controls.Primitives;
using System.Reflection;
using System.Diagnostics;

namespace Redux.Core.Tests;

public sealed class NexusModUpdateTests
{
	private static NexusInstalledFile Installed(long project = 42, long file = 10, string uuid = "fixture") =>
		new(uuid, "A mod with an optional compatibility patch", project, file, "1.0", true);
	private static NexusProjectFiles Files() => new(
		[new(10, "Original main file", "1.0", 4), new(20, "Intermediate file", "2.0", 4),
		 new(30, "Main replacement", "3.0", 1), new(999, "Unrelated optional patch", "99.0", 3)],
		[new(10, 20), new(20, 30)]);

	public void NexusUpdateFollowsReplacementChainInsteadOfNewestOptionalFile()
	{
		var result = NexusModUpdateEvaluator.Evaluate(Installed(), Files());
		RegressionAssert.Equal(NexusModUpdateStatus.UpdateAvailable, result.Status);
		RegressionAssert.Equal(30L, result.Candidate.FileId);
		RegressionAssert.True(result.FilesPageUrl.EndsWith("&file_id=30"));
		var current = NexusModUpdateEvaluator.Evaluate(Installed(file: 30), Files());
		RegressionAssert.Equal(NexusModUpdateStatus.NoUpdateReported, current.Status);
	}

	public void NexusUpdateAmbiguousMissingAndCyclicFilesRequireReview()
	{
		var branching = Files();
		branching.Replacements.Add(new(10, 999));
		RegressionAssert.Equal(NexusModUpdateStatus.NeedsReview, NexusModUpdateEvaluator.Evaluate(Installed(), branching).Status);
		var cycle = Files();
		cycle.Replacements.Add(new(30, 10));
		RegressionAssert.Equal(NexusModUpdateStatus.NeedsReview, NexusModUpdateEvaluator.Evaluate(Installed(), cycle).Status);
		RegressionAssert.Equal(NexusModUpdateStatus.NeedsReview, NexusModUpdateEvaluator.Evaluate(Installed(file: 404), Files()).Status);
		RegressionAssert.Equal(NexusModUpdateStatus.NeedsReview, NexusModUpdateEvaluator.Evaluate(Installed(), new([new(10, "Old", "1", 4)], [])).Status);
	}

	public void NexusUpdateWeakProjectIdentityCannotClaimAnInstalledFile()
	{
		var mod = new DivinityModData { UUID = Guid.NewGuid().ToString(), OnlineMetadataEnabled = true };
		mod.NexusModsData.ModId = 42;
		mod.NexusModsData.LastFileId = 10;
		mod.NexusModsData.MetadataOrigin = NexusMetadataOrigin.BundledProvenance;
		mod.NexusModsData.OfflineMatchKind = ReduxOfflineMatchKind.ModuleIdentity;
		var target = NexusInstalledFile.FromMod(mod);
		RegressionAssert.False(target.HasExactFileIdentity);
		RegressionAssert.Equal(NexusModUpdateStatus.DownloadNotIdentified, NexusModUpdateEvaluator.Evaluate(target, Files()).Status);
		mod.NexusModsData.OfflineMatchKind = ReduxOfflineMatchKind.ExactPak;
		RegressionAssert.False(NexusInstalledFile.FromMod(mod).HasExactFileIdentity);
		mod.NexusModsData.MetadataOrigin = NexusMetadataOrigin.NexusArchiveImport;
		RegressionAssert.False(NexusInstalledFile.FromMod(mod).HasExactFileIdentity);
		using var fixture = new Fixture();
		fixture.Service().CheckAsync([Installed()], "fixture-key", () => true).GetAwaiter().GetResult();
		var initial = fixture.Service().GetCachedResults([NexusInstalledFile.FromMod(mod)]).Single();
		RegressionAssert.Equal(NexusModUpdateStatus.DownloadNotIdentified, initial.Status);
		RegressionAssert.Equal(null, initial.Candidate);
		RegressionAssert.False(initial.CanChooseReference);
		mod.NexusModsData.MetadataOrigin = NexusMetadataOrigin.ReduxBundleImport;
		RegressionAssert.False(NexusInstalledFile.FromMod(mod).HasExactFileIdentity);
		mod.OnlineMetadataEnabled = false;
		RegressionAssert.Equal(null, NexusInstalledFile.FromMod(mod));
	}

	public void NexusUpdateChecksDeduplicateProjectsAndPaceRequests()
	{
		using var fixture = new Fixture();
		var targets = new[] { Installed(), Installed(uuid: "second"), Installed(43) };
		var run = fixture.Service().CheckAsync(targets, "fixture-key", () => true).GetAwaiter().GetResult();
		RegressionAssert.Equal(2, run.Requests);
		RegressionAssert.Equal(3, run.Results.Count);
		RegressionAssert.True(fixture.Client.Times[1] - fixture.Client.Times[0] >= TimeSpan.FromSeconds(1));
	}

	public void NexusUpdateCacheSurvivesRestartAndReevaluatesInstalledFile()
	{
		using var fixture = new Fixture();
		fixture.Service().CheckAsync([Installed()], "fixture-key", () => true).GetAwaiter().GetResult();
		var restarted = fixture.Service();
		var updatedInstall = restarted.CheckAsync([Installed(file: 30)], "fixture-key", () => true).GetAwaiter().GetResult();
		RegressionAssert.Equal(0, updatedInstall.Requests);
		RegressionAssert.Equal(NexusModUpdateStatus.NoUpdateReported, updatedInstall.Results.Single().Status);
		RegressionAssert.True(updatedInstall.Results.Single().FromCache);
		RegressionAssert.False(File.ReadAllText(fixture.Path).Contains("fixture-key"));
		fixture.Now = fixture.Now.AddHours(24);
		var expired = fixture.Service().CheckAsync([Installed(file: 30)], "fixture-key", () => true).GetAwaiter().GetResult();
		RegressionAssert.Equal(1, expired.Requests);
	}

	public void NexusUpdateSlowCacheWriteCannotConsumeRequestPacing()
	{
		foreach (var cancelFirst in new[] { false, true })
		{
			using var fixture = new Fixture();
			using var cancel = new CancellationTokenSource();
			var writes = 0;
			var service = new NexusModUpdateService(fixture.Client, fixture.Path, () => fixture.Now,
				(delay, token) => { token.ThrowIfCancellationRequested(); fixture.Now += delay; return Task.CompletedTask; },
				(path, json) =>
				{
					AtomicFileWriter.WriteAllText(path, json, path + ".bak");
					if (++writes == 1) fixture.Now = fixture.Now.AddSeconds(2);
				});
			if (cancelFirst) fixture.Client.Response = _ => { cancel.Cancel(); throw new OperationCanceledException(cancel.Token); };
			var run = service.CheckAsync([Installed(), Installed(43)], "fixture-key", () => true,
				cancellationToken: cancel.Token).GetAwaiter().GetResult();
			RegressionAssert.Equal(cancelFirst ? 1 : 2, run.Requests);
			if (cancelFirst)
			{
				RegressionAssert.True(run.Cancelled);
				fixture.Client.Response = _ => new(Files());
				var retry = fixture.Service().CheckAsync([Installed(), Installed(43)], "fixture-key", () => true).GetAwaiter().GetResult();
				RegressionAssert.Equal(1, retry.Requests);
			}
			RegressionAssert.True(fixture.Client.Times[1] - fixture.Client.Times[0] >= TimeSpan.FromSeconds(1));
		}
	}

	public void NexusUpdateUnknownDownloadGetsFileListButDisabledProviderMakesNoRequests()
	{
		using var fixture = new Fixture();
		var service = fixture.Service();
		var unknown = service.CheckAsync([Installed() with { HasExactFileIdentity = false }], "fixture-key", () => true).GetAwaiter().GetResult();
		RegressionAssert.Equal(1, unknown.Requests);
		RegressionAssert.Equal(NexusModUpdateStatus.DownloadNotIdentified, unknown.Results.Single().Status);
		RegressionAssert.Equal(4, unknown.Results.Single().AvailableFiles.Count);
		RegressionAssert.Equal(null, unknown.Results.Single().Candidate);
		RegressionAssert.Equal(0, service.CheckAsync([Installed()], "fixture-key", () => false).GetAwaiter().GetResult().Requests);
		RegressionAssert.Equal(0, service.CheckAsync([Installed()], "", () => true).GetAwaiter().GetResult().Requests);
	}

	public void NexusUpdateRateLimitStopsOtherProjectsAndSurvivesRestart()
	{
		using var fixture = new Fixture();
		fixture.Client.Response = _ => new(null!, "Rate limited", fixture.Now.AddHours(1), true);
		var first = fixture.Service().CheckAsync([Installed(), Installed(43)], "fixture-key", () => true).GetAwaiter().GetResult();
		RegressionAssert.Equal(1, first.Requests);
		RegressionAssert.Equal(0, fixture.Service().CheckAsync([Installed(43)], "fixture-key", () => true).GetAwaiter().GetResult().Requests);
		fixture.Now = fixture.Now.AddHours(1);
		fixture.Client.Response = _ => new(Files());
		RegressionAssert.Equal(1, fixture.Service().CheckAsync([Installed(43)], "fixture-key", () => true).GetAwaiter().GetResult().Requests);
	}

	public void NexusUpdateFailedRefreshDoesNotClaimCurrentOrEraseCheckTime()
	{
		using var fixture = new Fixture();
		var original = fixture.Service().CheckAsync([Installed()], "fixture-key", () => true).GetAwaiter().GetResult().Results.Single();
		fixture.Now = fixture.Now.AddDays(1);
		fixture.Client.Response = _ => new(null!, "Request failed", fixture.Now.AddMinutes(15), true);
		var failed = fixture.Service().CheckAsync([Installed()], "fixture-key", () => true).GetAwaiter().GetResult();
		RegressionAssert.Equal(NexusModUpdateStatus.CheckFailed, failed.Results.Single().Status);
		RegressionAssert.Equal(original.CheckedUtc, failed.Results.Single().CheckedUtc);
		RegressionAssert.Equal(0, fixture.Service().CheckAsync([Installed()], "fixture-key", () => true).GetAwaiter().GetResult().Requests);
	}

	public void NexusUpdateLongServerBackoffSurvivesRestart()
	{
		using var fixture = new Fixture();
		fixture.Client.Response = _ => new(null!, "Rate limited", fixture.Now.AddDays(3), true);
		fixture.Service().CheckAsync([Installed()], "fixture-key", () => true).GetAwaiter().GetResult();
		var restarted = fixture.Service();
		RegressionAssert.True(String.IsNullOrEmpty(restarted.CacheWarning));
		RegressionAssert.Equal(0, restarted.CheckAsync([Installed(), Installed(43)], "fixture-key", () => true).GetAwaiter().GetResult().Requests);
		fixture.Now = fixture.Now.AddDays(3);
		fixture.Client.Response = _ => new(Files());
		RegressionAssert.Equal(1, fixture.Service().CheckAsync([Installed()], "fixture-key", () => true).GetAwaiter().GetResult().Requests);
	}

	public void NexusUpdateCancellationPersistsAttemptCooldown()
	{
		using var fixture = new Fixture();
		using var cancel = new CancellationTokenSource();
		fixture.Client.Response = _ => { cancel.Cancel(); throw new OperationCanceledException(cancel.Token); };
		var run = fixture.Service().CheckAsync([Installed(), Installed(43)], "fixture-key", () => true, cancellationToken: cancel.Token).GetAwaiter().GetResult();
		RegressionAssert.True(run.Cancelled);
		RegressionAssert.Equal(1, run.Requests);
		fixture.Client.Response = _ => new(Files());
		RegressionAssert.Equal(0, fixture.Service().CheckAsync([Installed()], "fixture-key", () => true).GetAwaiter().GetResult().Requests);
	}

	public void NexusUpdateLockedCacheCannotStartDuplicateRequests()
	{
		using var fixture = new Fixture();
		using var held = new FileStream(fixture.Path + ".lock", FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
		var result = fixture.Service().CheckAsync([Installed()], "fixture-key", () => true).GetAwaiter().GetResult();
		RegressionAssert.Equal(0, result.Requests);
		RegressionAssert.True(result.Message.Contains("locked"));
	}

	public void NexusUpdateInvalidCacheShowsWarningWithoutRequests()
	{
		using var fixture = new Fixture();
		foreach (var json in new[] { "{broken", "{\"SchemaVersion\":99}",
			"{\"Projects\":{\"42\":null}}", "{\"Projects\":{\"42\":{\"Data\":{\"Files\":null,\"Replacements\":[]}}}}",
			Newtonsoft.Json.JsonConvert.SerializeObject(new { Projects = new Dictionary<long, object> { [42] = new { Data = Files() } } }),
			Newtonsoft.Json.JsonConvert.SerializeObject(new { Projects = new Dictionary<long, object> { [42] = new { Data = Files(), CheckedUtc = DateTimeOffset.MinValue } } }) })
		{
			File.WriteAllText(fixture.Path, json);
			var service = fixture.Service();
			RegressionAssert.True(!String.IsNullOrEmpty(service.CacheWarning));
			RegressionAssert.Equal(NexusModUpdateStatus.NotChecked, service.GetCachedResults([Installed()]).Single().Status);
			RegressionAssert.Equal(0, fixture.Client.Times.Count);
		}
	}

	public void NexusFileApiUsesFixedEndpointAndDiscardsUnneededRemoteData()
	{
		using var http = new HttpClient(new Handler(request =>
		{
			RegressionAssert.Equal("https://api.nexusmods.com/v1/games/baldursgate3/mods/42/files.json", request.RequestUri!.AbsoluteUri);
			RegressionAssert.Equal("fixture-key", request.Headers.GetValues("apikey").Single());
			return new(HttpStatusCode.OK) { Content = new StringContent(ValidResponse) };
		}));
		using var client = new NexusModFilesClient(http);
		var response = client.FetchAsync(42, "fixture-key", CancellationToken.None).GetAwaiter().GetResult();
		RegressionAssert.Equal(30L, response.Data.Files[0].FileId);
		var serialized = Newtonsoft.Json.JsonConvert.SerializeObject(response.Data);
		RegressionAssert.False(serialized.Contains("private-capability"));
	}

	public void NexusFileApiHonorsRetryAfterAndDoesNotExposeErrorBody()
	{
		using var http = new HttpClient(new Handler(_ =>
		{
			var response = new HttpResponseMessage(HttpStatusCode.TooManyRequests) { Content = new StringContent("fixture-key secret body") };
			response.Headers.RetryAfter = new RetryConditionHeaderValue(TimeSpan.FromHours(2));
			return response;
		}));
		using var client = new NexusModFilesClient(http);
		var result = client.FetchAsync(42, "fixture-key", CancellationToken.None).GetAwaiter().GetResult();
		RegressionAssert.True(result.StopChecks);
		RegressionAssert.True(result.RetryAfterUtc > DateTimeOffset.UtcNow.AddMinutes(119));
		RegressionAssert.False(result.Error.Contains("fixture-key"));
	}

	public void NexusFileApiRejectsMalformedAndOversizedResponses()
	{
		RegressionAssert.Throws<InvalidDataException>(() => NexusModFilesClient.Parse("{\"files\":[\"bad\"],\"file_updates\":[]}"));
		RegressionAssert.Throws<InvalidDataException>(() => NexusModFilesClient.Parse("{\"files\":[]}"));
		using var http = new HttpClient(new Handler(_ =>
		{
			var response = new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(ValidResponse) };
			response.Content.Headers.ContentLength = 9 * 1024 * 1024;
			return response;
		}));
		using var client = new NexusModFilesClient(http);
		var result = client.FetchAsync(42, "fixture-key", CancellationToken.None).GetAwaiter().GetResult();
		RegressionAssert.Equal(null, result.Data);
		RegressionAssert.True(result.StopChecks);
	}

	public void NexusUpdateWindowShowsReviewAndReplacementWithinCompactBounds()
	{
		var resources = Application.Current.Resources;
		Application.Current.Resources = WpfRenderCapture.CreateReduxApplicationResources();
		var shutdown = Application.Current.ShutdownMode;
		var reduceMotion = ReduxWindowBehavior.ReduceMotion;
		var effects = ReduxWindowBehavior.BackgroundEffectsDisabled;
		Application.Current.ShutdownMode = ShutdownMode.OnExplicitShutdown;
		ReduxWindowBehavior.ConfigureAccessibility(true, effects);
		var window = new NexusModUpdatesWindow { Left = -15000, Top = -15000, WindowStartupLocation = WindowStartupLocation.Manual, ShowActivated = false };
		ReduxThemeService.Apply(window.Resources, ReduxThemeType.ReduxDark);
		try
		{
			window.ViewModel.SetResults([
				NexusModUpdateEvaluator.Evaluate(Installed(), Files(), DateTimeOffset.Now, true) with
				{
					AvailableFiles = Files().Files, CanChooseReference = true,
					Acknowledgement = new(Guid.NewGuid().ToString(), 42, 20, new string('a', 64), "Previous chosen main release", "2.0", DateTimeOffset.UtcNow)
				},
				NexusModUpdateEvaluator.Evaluate(Installed(uuid: "review") with { HasExactFileIdentity = false }, null!),
				new(Installed(uuid: "failure"), NexusModUpdateStatus.CheckFailed, "Nexus request budget is low. Retry later.")]);
			window.ViewModel.CanCheck = true;
			window.Show();
			foreach (var theme in new[] { ReduxThemeType.ReduxDark, ReduxThemeType.ReduxLight, ReduxThemeType.Parchment })
			foreach (var width in new[] { 900d, 560d })
			{
				ReduxThemeService.Apply(window.Resources, theme);
				window.Width = width;
				window.Dispatcher.Invoke(() => { }, DispatcherPriority.ContextIdle);
				window.UpdateLayout();
				var check = (Button)window.FindName("CheckNowButton");
				RegressionAssert.True(check.IsEnabled);
				RegressionAssert.False(((Button)window.FindName("CancelCheckButton")).IsEnabled);
				var list = (ListBox)window.FindName("ResultsList");
				RegressionAssert.Equal(3, list.Items.Count);
				var choices = WpfRenderCapture.Descendants<ComboBox>(list).First();
				RegressionAssert.Equal(4, choices.Items.Count);
				RegressionAssert.Equal(null, choices.SelectedItem);
				var prompt = WpfRenderCapture.Descendants<TextBlock>(list).First(text => text.Name == "ReferencePrompt");
				RegressionAssert.True(ReferenceEquals(prompt.Foreground, window.FindResource("ReduxTextSecondaryBrush")));
				WpfRenderCapture.AssertFullyWithin(prompt, list);
				var scroll = WpfRenderCapture.Descendants<ScrollViewer>(list).First();
				RegressionAssert.True(scroll.ScrollableWidth <= 1);
				WpfRenderCapture.CaptureIfRequested(window, $"nexus-mod-updates-{theme}-{width:0}");
			}
		}
		finally
		{
			window.Close();
			ReduxWindowBehavior.ConfigureAccessibility(reduceMotion, effects);
			Application.Current.ShutdownMode = shutdown;
			Application.Current.Resources = resources;
		}
	}

	public void NexusUpdateWindowSavesAndResetsReferenceThroughActualControls()
	{
		using var fixture = new Fixture();
		using var watcher = WpfRenderCapture.RegisterNoOpFileWatcherService();
		var oldResources = Application.Current.Resources;
		var shutdown = Application.Current.ShutdownMode;
		var reduceMotion = ReduxWindowBehavior.ReduceMotion;
		var effects = ReduxWindowBehavior.BackgroundEffectsDisabled;
		Application.Current.Resources = WpfRenderCapture.CreateReduxApplicationResources();
		ReduxThemeService.Apply(Application.Current.Resources, ReduxThemeType.ReduxDark);
		Application.Current.ShutdownMode = ShutdownMode.OnExplicitShutdown;
		ReduxWindowBehavior.ConfigureAccessibility(true, effects);
		var pak = System.IO.Path.ChangeExtension(fixture.Path, ".pak");
		File.WriteAllText(pak, "controlled UI-test package bytes");
		var mod = new RegressionModData { UUID = Guid.NewGuid().ToString(), Name = "Example UI package",
			FilePath = pak, IsUserMod = true, OnlineMetadataEnabled = true, NexusModsEnabled = true };
		mod.NexusModsData.ModId = 42;
		var main = new MainWindowViewModel();
		main.Settings.LocalOnlyMode = false;
		var models = (SourceCache<DivinityModData, string>)typeof(MainWindowViewModel)
			.GetField("mods", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(main)!;
		models.AddOrUpdate(mod);
		var service = fixture.Service();
		service.CheckAsync([Installed()], "fixture-key", () => true).GetAwaiter().GetResult();
		var window = new NexusModUpdatesWindow(null!, main)
		{ Left = -15000, Top = -15000, ShowActivated = false, WindowStartupLocation = WindowStartupLocation.Manual };
		typeof(NexusModUpdatesWindow).GetField("_service", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(window, service);
		void WaitFor(Func<bool> ready)
		{
			var timer = Stopwatch.StartNew();
			while (!ready() && timer.Elapsed < TimeSpan.FromSeconds(15))
			{
				window.Dispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
				Thread.Sleep(10);
			}
			if (!ready()) throw new InvalidOperationException($"UI did not settle: busy={window.ViewModel.IsChecking}, rows={window.ViewModel.Results.Count}, "
				+ $"status={window.ViewModel.StatusText}; " + String.Join("; ", window.ViewModel.Results.Select(row => $"{row.Result.Status}, choices={row.Result.AvailableFiles.Count}, canChoose={row.Result.CanChooseReference}, identity={row.Result.Installed.IdentityDescription}")));
			window.UpdateLayout();
		}
		try
		{
			window.Show();
			WaitFor(() => !window.ViewModel.IsChecking && window.ViewModel.Results.Count == 1
				&& window.ViewModel.Results[0].Result.CanChooseReference);
			RegressionAssert.Equal(1, fixture.Client.Times.Count); // Opening used only cached listings.
			var list = (ListBox)window.FindName("ResultsList");
			var combo = WpfRenderCapture.Descendants<ComboBox>(list).First();
			RegressionAssert.Equal(null, combo.SelectedItem);
			combo.SelectedItem = window.ViewModel.Results[0].Result.AvailableFiles.Single(file => file.FileId == 30);
			var set = WpfRenderCapture.Descendants<Button>(list).First(button => button.Name == "SetReferenceButton");
			window.Dispatcher.Invoke(() => set.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent)));
			WaitFor(() => !window.ViewModel.IsChecking && window.ViewModel.Results[0].Result.Acknowledgement != null);
			RegressionAssert.Equal(NexusModUpdateStatus.Acknowledged, window.ViewModel.Results[0].Result.Status);
			WpfRenderCapture.CaptureIfRequested(window, "nexus-reference-acknowledged");
			var reset = WpfRenderCapture.Descendants<Button>(list).First(button => button.Name == "ResetReferenceButton");
			window.Dispatcher.Invoke(() => reset.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent)));
			WaitFor(() => window.ViewModel.Results[0].Result.Acknowledgement == null);
			RegressionAssert.Equal(NexusModUpdateStatus.DownloadNotIdentified, window.ViewModel.Results[0].Result.Status);
			RegressionAssert.Equal(1, fixture.Client.Times.Count);
			RegressionAssert.Equal("controlled UI-test package bytes", File.ReadAllText(pak));
			WpfRenderCapture.CaptureIfRequested(window, "nexus-reference-reset");
		}
		finally
		{
			window.Close();
			ReduxWindowBehavior.ConfigureAccessibility(reduceMotion, effects);
			Application.Current.ShutdownMode = shutdown;
			Application.Current.Resources = oldResources;
		}
	}

	private const string ValidResponse = """
		{"files":[{"file_id":30,"name":"Main file","version":"3.0","category_id":1,"download_link":"https://example.com/private-capability"}],
		"file_updates":[{"old_file_id":10,"new_file_id":30}]}
		""";
	private sealed class Handler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
	{
		protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => Task.FromResult(respond(request));
	}
	private sealed class FakeClient(Func<DateTimeOffset> now) : INexusModFilesClient
	{
		public List<DateTimeOffset> Times { get; } = new();
		public Func<long, NexusFilesResponse> Response { get; set; } = _ => new(Files());
		public Task<NexusFilesResponse> FetchAsync(long projectId, string apiKey, CancellationToken cancellationToken)
		{
			Times.Add(now());
			return Task.FromResult(Response(projectId));
		}
	}
	private sealed class Fixture : IDisposable
	{
		private readonly string _directory = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "redux-nexus-check-tests-" + Guid.NewGuid().ToString("N"));
		public DateTimeOffset Now = DateTimeOffset.UtcNow;
		public string Path => System.IO.Path.Combine(_directory, "checks.json");
		public FakeClient Client { get; }
		public Fixture() { Directory.CreateDirectory(_directory); Client = new FakeClient(() => Now); }
		public NexusModUpdateService Service() => new(Client, Path, () => Now, (delay, token) => { token.ThrowIfCancellationRequested(); Now += delay; return Task.CompletedTask; });
		public void Dispose() { Directory.Delete(_directory, true); }
	}
}
