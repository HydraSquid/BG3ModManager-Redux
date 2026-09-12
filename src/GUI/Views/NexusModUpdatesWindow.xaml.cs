using DivinityModManager.AppServices;
using DivinityModManager.Util;
using DivinityModManager.ViewModels;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;

namespace DivinityModManager.Views;

public sealed class NexusModUpdatesViewModel : ReactiveObject
{
	public ObservableCollection<NexusModUpdateResult> Results { get; } = new();
	[Reactive] public bool IsChecking { get; set; }
	[Reactive] public bool CanCheck { get; set; }
	[Reactive] public string StatusText { get; set; } = "No network requests are made until you choose Check now.";
	[Reactive] public string Summary { get; set; }
	public void SetResults(IEnumerable<NexusModUpdateResult> results)
	{
		Results.Clear();
		foreach (var result in results) Results.Add(result);
		Summary = Results.Count == 0 ? "No installed Nexus-linked mods found."
			: $"{Results.Count} installed Nexus-linked mod(s) · {Results.Count(row => row.Status == NexusModUpdateStatus.UpdateAvailable)} replacement(s) · "
				+ $"{Results.Count(row => row.Status == NexusModUpdateStatus.NeedsReview)} need manual review";
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
		ViewModel.SetResults(_service.GetCachedResults(Snapshot()));
		if (_service.CacheWarning != null) ViewModel.StatusText = _service.CacheWarning;
		_main.Settings.PropertyChanged += SettingsChanged;
		UpdateAvailability();
	}

	private NexusInstalledFile[] Snapshot() => _main.UserMods
		.Where(mod => File.Exists(mod.FilePath)).Select(NexusInstalledFile.FromMod)
		.Where(file => file != null).ToArray();

	private void UpdateAvailability()
	{
		ViewModel.CanCheck = _main != null && !ViewModel.IsChecking && !_main.Settings.LocalOnlyMode
			&& !String.IsNullOrWhiteSpace(_main.Settings.NexusModsAPIKey);
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
		ViewModel.IsChecking = true;
		UpdateAvailability();
		_cancellation = new CancellationTokenSource();
		var apiKey = _main.Settings.NexusModsAPIKey;
		try
		{
			var snapshot = Snapshot();
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
				ViewModel.StatusText = run.Message;
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
		if ((sender as FrameworkElement)?.Tag is NexusModUpdateResult result)
			ProcessHelper.TryOpenUrl(result.FilesPageUrl);
	}
	private void OnClosed(object sender, EventArgs e)
	{
		_closed = true;
		_cancellation?.Cancel();
		if (_main != null) _main.Settings.PropertyChanged -= SettingsChanged;
		if (!ViewModel.IsChecking) _client?.Dispose();
	}
}
