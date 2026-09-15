using DivinityModManager.AppServices;
using DivinityModManager.Util;
using DivinityModManager.ViewModels;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;

namespace DivinityModManager.Views;

public sealed class NexusModUpdateRow : ReactiveObject
{
	public NexusModUpdateResult Result { get; }
	[Reactive] public NexusRemoteFile SelectedRelease { get; set; }
	public NexusModUpdateRow(NexusModUpdateResult result) => Result = result;
}

public sealed class NexusModUpdatesViewModel : ReactiveObject
{
	public ObservableCollection<NexusModUpdateRow> Results { get; } = new();
	[Reactive] public bool IsChecking { get; set; }
	[Reactive] public bool CanCheck { get; set; }
	[Reactive] public bool CanEdit { get; set; } = true;
	[Reactive] public string StatusText { get; set; } = "Check for updates reads Nexus listings only. Nothing will be downloaded, installed or reordered.";
	[Reactive] public string Summary { get; set; }
	public void SetResults(IEnumerable<NexusModUpdateResult> results)
	{
		Results.Clear();
		foreach (var result in results) Results.Add(new(result));
		Summary = Results.Count == 0 ? "No installed Nexus-linked mods found."
			: $"{Results.Count} linked mods in {Results.Select(row => row.Result.Installed.ModId).Distinct().Count()} projects · {Results.Count(row => row.Result.Installed.HasExactFileIdentity || row.Result.Acknowledgement != null)} can compare · "
				+ $"{Results.Count(row => !row.Result.Installed.HasExactFileIdentity && row.Result.Acknowledgement == null)} need a download reference · "
				+ $"{Results.Count(row => row.Result.Status == NexusModUpdateStatus.UpdateAvailable)} updates · "
				+ $"{Results.Count(row => row.Result.Acknowledgement != null)} using your references";
	}
}

public partial class NexusModUpdatesWindow : AdonisUI.Controls.AdonisWindow
{
	public NexusModUpdatesViewModel ViewModel { get; } = new();
	private readonly MainWindowViewModel _main;
	private readonly NexusModFilesClient _client;
	private readonly NexusModUpdateService _service;
	private CancellationTokenSource _cancellation;
	private bool _closed;
	private NexusUpdateInstalledFile[] _snapshot = Array.Empty<NexusUpdateInstalledFile>();

	public NexusModUpdatesWindow()
	{
		InitializeComponent();
		DataContext = ViewModel;
		ReduxWindowBehavior.AttachDialogTransitions(this, 40);
		ReduxWindowBehavior.AttachRoundedCorners(this);
		Closed += OnClosed;
	}

	public NexusModUpdatesWindow(MainWindow owner, MainWindowViewModel main) : this()
	{
		Owner = owner;
		_main = main;
		_client = new NexusModFilesClient();
		_service = new NexusModUpdateService(_client, DivinityApp.GetAppDirectory("Data", "NexusUpdateChecks.json"));
		ReduxThemeService.Apply(Resources, main.Settings.ColorTheme,
			ReduxThemeService.GetActiveTheme(main.Settings), main.Settings.UsesGeneratedGradients);
		_snapshot = Snapshot();
		ViewModel.SetResults(_service.GetCachedResults(_snapshot));
		if (_service.CacheWarning != null) ViewModel.StatusText = _service.CacheWarning;
		_main.Settings.PropertyChanged += SettingsChanged;
		UpdateAvailability();
		Loaded += LoadLocalIdentities;
	}

	private NexusUpdateInstalledFile[] Snapshot() => _main.UserMods
		.Where(mod => File.Exists(mod.FilePath)).Select(NexusUpdateInstalledFile.FromMod)
		.Where(file => file != null).ToArray();

	private void UpdateAvailability()
	{
		ViewModel.CanCheck = _main != null && !ViewModel.IsChecking && !_main.Settings.LocalOnlyMode
			&& !String.IsNullOrWhiteSpace(_main.Settings.NexusModsAPIKey);
		ViewModel.CanEdit = !ViewModel.IsChecking;
		if (_main != null && (_main.Settings.LocalOnlyMode || String.IsNullOrWhiteSpace(_main.Settings.NexusModsAPIKey)))
			ViewModel.StatusText = "Enable online mod information and configure your Nexus API key in Preferences. Cached results remain available.";
	}

	private void SettingsChanged(object sender, PropertyChangedEventArgs e)
	{
		if (e.PropertyName is nameof(_main.Settings.LocalOnlyMode) or nameof(_main.Settings.NexusModsAPIKey))
		{
			_cancellation?.Cancel();
			UpdateAvailability();
		}
	}

	private async void CheckNow_Click(object sender, RoutedEventArgs e)
	{
		if (!ViewModel.CanCheck || _main == null) return;
		await RunCheckAsync(true);
	}

	private async void LoadLocalIdentities(object sender, RoutedEventArgs e)
	{
		Loaded -= LoadLocalIdentities;
		if (!_closed && !ViewModel.IsChecking) await RunCheckAsync(false);
	}

	private async Task RunCheckAsync(bool requestNetwork)
	{
		ViewModel.IsChecking = true;
		UpdateAvailability();
		_cancellation = new CancellationTokenSource();
		var apiKey = _main.Settings.NexusModsAPIKey;
		try
		{
			var snapshot = Snapshot();
			// Rechecking/cancelling must not leave a previous scan's proof attached to changed bytes.
			_snapshot = snapshot;
			ViewModel.SetResults(_service.GetCachedResults(snapshot));
			var evidence = _main.GetNexusUpdateIdentityEvidence();
			var resolver = new NexusInstalledIdentityResolver();
			for (var index = 0; index < snapshot.Length; index++)
			{
				ViewModel.StatusText = $"Identifying local downloads (no network): {index + 1}/{snapshot.Length}";
				var installed = snapshot[index];
				snapshot[index] = await Task.Run(() => resolver.ResolveAsync(installed, evidence, _cancellation.Token));
			}
			_snapshot = snapshot;
			if (_closed) return;
			ViewModel.SetResults(_service.GetCachedResults(snapshot));
			if (!requestNetwork)
			{
				ViewModel.StatusText = _service.ReferenceWarning ?? _service.CacheWarning
					?? "Local matching complete. Check for updates reads Nexus listings only; it does not change mods or orders.";
				return;
			}
			var progress = new Progress<NexusUpdateProgress>(state =>
			{
				if (!_closed && ViewModel.IsChecking)
					ViewModel.StatusText = $"Checking projects: {state.CompletedProjects}/{state.TotalProjects} · {state.Requests} request(s) · {state.CachedProjects} cached";
			});
			var run = await _service.CheckAsync(snapshot, apiKey,
				() => !_main.Settings.LocalOnlyMode && _main.Settings.NexusModsAPIKey == apiKey && !_main.IsRefreshing,
				progress, _cancellation.Token);
			if (!_closed)
			{
				ViewModel.SetResults(run.Results);
				ViewModel.StatusText = _service.ReferenceWarning ?? run.Message;
			}
		}
		catch (OperationCanceledException) { if (!_closed) ViewModel.StatusText = "Check cancelled."; }
		catch (Exception ex) when (ex is InvalidOperationException or IOException or UnauthorizedAccessException)
		{
			if (!_closed) ViewModel.StatusText = "The check could not complete. No installed files or load orders were changed.";
		}
		finally
		{
			_cancellation.Dispose();
			_cancellation = null;
			ViewModel.IsChecking = false;
			if (_closed) _client.Dispose(); else UpdateAvailability();
		}
	}

	private void CancelCheck_Click(object sender, RoutedEventArgs e) => _cancellation?.Cancel();
	private void Close_Click(object sender, RoutedEventArgs e) => Close();
	private void OpenFiles_Click(object sender, RoutedEventArgs e)
	{
		if ((sender as FrameworkElement)?.Tag is NexusModUpdateRow row)
			ProcessHelper.TryOpenUrl(row.Result.FilesPageUrl);
	}

	private async void SetReference_Click(object sender, RoutedEventArgs e)
	{
		if (!ViewModel.CanEdit || (sender as FrameworkElement)?.Tag is not NexusModUpdateRow row || _service == null) return;
		if (row.SelectedRelease == null) { ViewModel.StatusText = "Choose the matching release first. Main files and optional patches are different downloads."; return; }
		var current = Snapshot().FirstOrDefault(file => file.Uuid == row.Result.Installed.Uuid);
		if (current?.ModId != row.Result.Installed.ModId || current.FilePath != row.Result.Installed.FilePath)
		{ ViewModel.StatusText = "The mod's source or location changed. Reopen this window to refresh it."; return; }
		ViewModel.IsChecking = true;
		UpdateAvailability();
		_cancellation = new();
		try
		{
			await _service.SetReferenceAsync(row.Result.Installed, row.SelectedRelease.FileId, _cancellation.Token);
			if (!_closed)
			{
				ViewModel.SetResults(_service.GetCachedResults(_snapshot));
				ViewModel.StatusText = "Reference saved. Later linked replacements will appear again. Installed metadata, mods and orders are unchanged.";
			}
		}
		catch (OperationCanceledException) { if (!_closed) ViewModel.StatusText = "Reference change cancelled."; }
		catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException or InvalidOperationException or Newtonsoft.Json.JsonException)
		{ if (!_closed) ViewModel.StatusText = "Reference was not saved. The package, checked file list or reference store may have changed. Recheck and try again."; }
		finally
		{
			_cancellation.Dispose(); _cancellation = null; ViewModel.IsChecking = false;
			if (_closed) _client.Dispose(); else UpdateAvailability();
		}
	}

	private void ResetReference_Click(object sender, RoutedEventArgs e)
	{
		if (!ViewModel.CanEdit || (sender as FrameworkElement)?.Tag is not NexusModUpdateRow row || _service == null) return;
		try
		{
			_service.ResetReference(row.Result.Installed);
			ViewModel.SetResults(_service.GetCachedResults(_snapshot));
			ViewModel.StatusText = "Reference cleared. Comparison now uses the identified installed download, when available.";
		}
		catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException or Newtonsoft.Json.JsonException)
		{ ViewModel.StatusText = "The reference could not be cleared. Existing saved references were preserved."; }
	}
	private void OnClosed(object sender, EventArgs e)
	{
		_closed = true;
		_cancellation?.Cancel();
		if (_main != null) _main.Settings.PropertyChanged -= SettingsChanged;
		if (!ViewModel.IsChecking) _client?.Dispose();
	}
}
