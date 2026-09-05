using DivinityModManager.AppServices;
using DivinityModManager.Models;
using DivinityModManager.Models.NexusMods;
using DivinityModManager.Util;

using System.Windows;
using System.Windows.Controls;

namespace DivinityModManager.Views;

public partial class NxmDependencyReviewWindow : AdonisUI.Controls.AdonisWindow
{
	public DivinityModData RequestedInstalledMod { get; private set; }
	public NxmDownloadItem RequestedDownload { get; private set; }

	public NxmDependencyReviewWindow(Window owner, string summary,
		IReadOnlyList<ModDependencyAssistance> dependencies, bool allowInstall = false)
	{
		InitializeComponent();
		ReduxWindowBehavior.AttachDialogTransitions(this, 40);
		ReduxWindowBehavior.AttachRoundedCorners(this);
		if (owner?.IsLoaded == true) Owner = owner;
		var settings = (owner as MainWindow)?.ViewModel?.Settings;
		if (settings != null) ReduxThemeService.Apply(Resources, settings.ColorTheme, ReduxThemeService.GetActiveTheme(settings));
		SummaryText.Text = summary;
		DependenciesList.ItemsSource = dependencies;
		DependenciesPanel.Visibility = dependencies.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
		InstallButton.Visibility = allowInstall ? Visibility.Visible : Visibility.Collapsed;
		if (allowInstall) { Title = "Review Nexus Installation"; DismissButton.Content = "Cancel"; }
	}

	private void OpenFiles_Click(object sender, RoutedEventArgs e)
	{
		if ((sender as FrameworkElement)?.Tag is ModDependencyAssistance { CanOpenFiles: true } dependency)
			ProcessHelper.TryOpenUrl(dependency.NexusFilesUrl.AbsoluteUri);
	}

	private void CopyUuid_Click(object sender, RoutedEventArgs e)
	{
		if ((sender as FrameworkElement)?.Tag is not ModDependencyAssistance { CanCopyUuid: true } dependency) return;
		try { Clipboard.SetText(dependency.Uuid); ActionStatusText.Text = "Dependency UUID copied."; }
		catch { ActionStatusText.Text = "The clipboard is unavailable. Try copying again."; }
	}

	private void ShowInstalled_Click(object sender, RoutedEventArgs e)
	{
		if ((sender as FrameworkElement)?.Tag is not ModDependencyAssistance { CanShowInstalled: true } dependency) return;
		RequestedInstalledMod = dependency.InstalledMod;
		DialogResult = false;
	}

	private void ShowDownload_Click(object sender, RoutedEventArgs e)
	{
		if ((sender as FrameworkElement)?.Tag is not DependencyDownloadMatch match) return;
		RequestedDownload = match.Download;
		DialogResult = false;
	}

	private void Install_Click(object sender, RoutedEventArgs e) => DialogResult = true;
}
