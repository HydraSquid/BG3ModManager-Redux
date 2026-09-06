using DivinityModManager.AppServices;
using DivinityModManager.Util;
using DivinityModManager.ViewModels;
using Microsoft.Win32;
using System.Windows;
using System.Windows.Controls;

namespace DivinityModManager.Views;

public partial class NativeModsWindow : AdonisUI.Controls.AdonisWindow
{
	private readonly MainWindowViewModel _viewModel;
	private bool _busy;
	public NativeModsWindow(Window owner, MainWindowViewModel viewModel, long projectId = 944)
	{
		InitializeComponent();
		_viewModel = viewModel;
		if (owner?.IsLoaded == true) Owner = owner;
		ReduxWindowBehavior.AttachDialogTransitions(this, 40);
		ReduxWindowBehavior.AttachRoundedCorners(this);
		if (viewModel != null) ReduxThemeService.Apply(Resources, viewModel.Settings.ColorTheme, ReduxThemeService.GetActiveTheme(viewModel.Settings));
		Projects.ItemsSource = NativeModCatalog.All;
		Projects.SelectedItem = NativeModCatalog.Find(projectId) ?? NativeModCatalog.All[0];
		Activated += (_, _) => { if (!_busy) RefreshStatus(); };
		Closing += (_, e) => { if (_busy) e.Cancel = true; };
		RefreshStatus();
	}

	private void RefreshStatus()
	{
		if (Projects.SelectedItem is not NativeModDefinition selected) return;
		LoaderStatusText.Text = _viewModel?.GetNativeLoaderStatus().Description ?? "Status unavailable until a game installation is configured.";
		GameVersionText.Text = $"Detected game build: {_viewModel?.GetNativeGameVersion()?.ToString() ?? "unknown"}. "
			+ (selected.RequiresLoader ? "This plugin requires Native Mod Loader and BG3 Hotfix 34 (4.1.1.6931813) or newer." : "No reliable loader-version range is published; presence does not establish compatibility with every game update.");
		TargetFilesText.Text = "Game-relative files:\n" + String.Join("\n", selected.RelativeFiles);
		GameDirectoryText.Text = "Game directory: " + (_viewModel?.GetNativeGameDirectory() ?? "Not configured");
		Actions.IsEnabled = _viewModel != null && !_busy;
		NexusButton.IsEnabled = _viewModel?.Modules.SourceIntegrationsEnabled == true;
		_viewModel?.RefreshNativeDownloadBadges();
	}

	private void Projects_SelectionChanged(object sender, SelectionChangedEventArgs e) { if (Actions != null) RefreshStatus(); }
	private void Refresh_Click(object sender, RoutedEventArgs e) => RefreshStatus();
	private void RecoveryFolder_Click(object sender, RoutedEventArgs e) => ProcessHelper.TryOpenPath(DivinityApp.GetAppDirectory("Data", "NativeInstalls"), System.IO.Directory.Exists);
	private void Nexus_Click(object sender, RoutedEventArgs e)
	{
		if (_viewModel?.Modules.SourceIntegrationsEnabled == true && Projects.SelectedItem is NativeModDefinition selected)
			ProcessHelper.TryOpenUrl($"https://www.nexusmods.com/baldursgate3/mods/{selected.NexusModId}?tab=files");
	}
	private async void Install_Click(object sender, RoutedEventArgs e)
	{
		if (Projects.SelectedItem is not NativeModDefinition selected) return;
		var dialog = new OpenFileDialog { Title = $"Select the author's {selected.Name} ZIP", Filter = "Supported native ZIP archive|*.zip", Multiselect = false };
		if (dialog.ShowDialog(this) == true) await RunAsync(() => _viewModel.InstallNativeArchiveAsync(selected.NexusModId, dialog.FileName, this));
	}
	private async void Restore_Click(object sender, RoutedEventArgs e)
	{
		if (Projects.SelectedItem is NativeModDefinition selected) await RunAsync(() => _viewModel.RestoreNativeModAsync(selected.NexusModId, this));
	}
	private async Task RunAsync(Func<Task> operation)
	{
		if (_busy || _viewModel == null) return;
		_busy = true;
		Actions.IsEnabled = false;
		Projects.IsEnabled = false;
		ResultText.Text = "Checking files and recovery records...";
		try { await operation(); ResultText.Text = "Review finished. Installed files change only after confirmation; see the refreshed status and any completion notification."; }
		catch (Exception ex) { ResultText.Text = MainWindowViewModel.NativeInstallError(ex); }
		finally { _busy = false; Projects.IsEnabled = true; RefreshStatus(); }
	}
}
