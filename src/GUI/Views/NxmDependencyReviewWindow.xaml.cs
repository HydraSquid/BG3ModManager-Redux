using DivinityModManager.AppServices;
using DivinityModManager.Models.NexusMods;
using DivinityModManager.Util;

using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows;

namespace DivinityModManager.Views;

public partial class NxmDependencyReviewWindow : AdonisUI.Controls.AdonisWindow
{
	public NxmDownloadItem RequestedDownload { get; private set; }

	public NxmDependencyReviewWindow(Window owner, string packageName,
		IReadOnlyList<ModDependencyAssistance> dependencies)
	{
		InitializeComponent();
		ReduxWindowBehavior.AttachDialogTransitions(this, 40);
		ReduxWindowBehavior.AttachRoundedCorners(this);
		if (owner?.IsLoaded == true) Owner = owner;

		var settings = MainWindow.Self?.ViewModel?.Settings;
		if (settings != null)
			ReduxThemeService.Apply(Resources, settings.ColorTheme,
				ReduxThemeService.GetActiveTheme(settings), settings.UsesGeneratedGradients);

		Title = "Review Mod Dependencies";
		HeadingText.Text = "Dependencies need attention";
		DescriptionText.Text = "Redux matched requirements by module UUID only. It never guesses that a similarly named file is the correct dependency.";
		SummaryText.Text = $"{packageName}: {dependencies?.Count ?? 0} declared dependency requirement{(dependencies?.Count == 1 ? String.Empty : "s")}. Installed and bundled packages are shown for context; no files were changed.";
		DependencyList.ItemsSource = dependencies ?? [];
	}

	private void CopyUuid_Click(object sender, RoutedEventArgs e)
	{
		if ((sender as FrameworkElement)?.DataContext is not ModDependencyAssistance dependency || !dependency.CanCopyUuid) return;
		try { Clipboard.SetText(dependency.Uuid); }
		catch (ExternalException) { }
	}

	private void OpenFiles_Click(object sender, RoutedEventArgs e)
	{
		if ((sender as FrameworkElement)?.DataContext is not ModDependencyAssistance dependency || !dependency.CanOpenFiles) return;
		try
		{
			Process.Start(new ProcessStartInfo(dependency.NexusFilesUrl.AbsoluteUri) { UseShellExecute = true });
		}
		catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException) { }
	}

	private void ShowDownload_Click(object sender, RoutedEventArgs e)
	{
		RequestedDownload = (sender as FrameworkElement)?.Tag as NxmDownloadItem;
		DialogResult = RequestedDownload != null;
	}

	private void Close_Click(object sender, RoutedEventArgs e) => Close();
}
