using DivinityModManager.Models.NexusMods;
using DivinityModManager.Util;
using DivinityModManager.ViewModels;

using System.Windows;
using System.Windows.Controls;

namespace DivinityModManager.Views;

public partial class NxmDownloadsPane : UserControl
{
	private MainWindowViewModel ViewModel => DataContext as MainWindowViewModel;
	public NxmDownloadsPane() => InitializeComponent();

	private static NxmDownloadItem Item(object sender) => (sender as FrameworkElement)?.Tag as NxmDownloadItem;
	private void Close_Click(object sender, RoutedEventArgs e) => ViewModel.NxmDownloadsPaneVisible = false;
	private async void PauseAll_Click(object sender, RoutedEventArgs e) => await RunCommandAsync(ViewModel.PauseAllNxmDownloadsAsync);
	private async void ResumeAll_Click(object sender, RoutedEventArgs e) => await RunCommandAsync(ViewModel.ResumeAllNxmDownloadsAsync);
	private async void InstallSelected_Click(object sender, RoutedEventArgs e) => await RunCommandAsync(ViewModel.InstallSelectedNxmDownloadsAsync);
	private async void ClearCompleted_Click(object sender, RoutedEventArgs e) => await RunCommandAsync(ViewModel.ClearCompletedNxmDownloadsAsync);
	private void OpenFolder_Click(object sender, RoutedEventArgs e) => ProcessHelper.TryOpenPath(ViewModel.NxmDownloadsDirectory, Directory.Exists);
	private async void Pause_Click(object sender, RoutedEventArgs e) => await RunCommandAsync(() => ViewModel.PauseNxmDownloadAsync(Item(sender)));
	private async void Resume_Click(object sender, RoutedEventArgs e) => await RunCommandAsync(() => ViewModel.ResumeNxmDownloadAsync(Item(sender)));
	private async void Install_Click(object sender, RoutedEventArgs e) => await RunCommandAsync(() => ViewModel.InstallNxmDownloadAsync(Item(sender)));
	private async void Retry_Click(object sender, RoutedEventArgs e) => await RunCommandAsync(() => ViewModel.RetryNxmDownloadAsync(Item(sender)));
	private async void DownloadAgain_Click(object sender, RoutedEventArgs e) => await RunCommandAsync(() => ViewModel.DownloadAgainNxmAsync(Item(sender)));
	private async void Details_Click(object sender, RoutedEventArgs e) => await RunCommandAsync(() => ViewModel.ShowNxmDownloadDetailsAsync(Item(sender)));
	private async void Cancel_Click(object sender, RoutedEventArgs e) => await RunCommandAsync(() => ViewModel.CancelNxmDownloadAsync(Item(sender)));
	private void OpenPage_Click(object sender, RoutedEventArgs e) { var item = Item(sender); if (item?.NexusPage != null) ProcessHelper.TryOpenUrl(item.NexusPage.ToString()); }
	private async void Remove_Click(object sender, RoutedEventArgs e) => await RunCommandAsync(() => ViewModel.RemoveNxmDownloadAsync(Item(sender)));

	private async Task RunCommandAsync(Func<Task> command)
	{
		try { await command(); }
		catch (Exception ex)
		{
			DivinityApp.Log($"Nexus download command failed: {ex.GetType().Name}");
			var detail = ex switch
			{
				UnauthorizedAccessException => "The command was blocked by file access permissions. Check the Downloads folder and security-software restrictions.",
				IOException => "The command could not save or access the download files. Check available disk space, folder permissions, and file locks, then retry.",
				InvalidOperationException => "The command is not available in the current queue state. Check that online integrations are enabled and shutdown is not in progress.",
				_ => $"The Nexus download command failed ({ex.GetType().Name}). The queue may not have changed; check the item's status before retrying."
			};
			ReduxMessageBox.Show(Window.GetWindow(this), detail,
				"Nexus Downloads", MessageBoxButton.OK, MessageBoxImage.Error, MessageBoxResult.OK);
		}
	}

	private void SelectAllDownloads_Click(object sender, RoutedEventArgs e) => SetSelection(SelectAllDownloadsCheckBox.IsChecked == true);
	private void SelectAllMenu_Click(object sender, RoutedEventArgs e) => SetSelection(true);
	private void ClearSelectionMenu_Click(object sender, RoutedEventArgs e) => SetSelection(false);
	private void DownloadsList_SelectionChanged(object sender, SelectionChangedEventArgs e) => UpdateSelectionHeader();
	private void DownloadsList_ContextMenuOpening(object sender, ContextMenuEventArgs e)
	{
		if (e.OriginalSource is not DependencyObject source) return;
		DownloadsList.Tag = (source as FrameworkElement)?.DataContext as NxmDownloadItem
			?? source.FindVisualParent<ListViewItem>()?.DataContext as NxmDownloadItem
			?? DownloadsList.SelectedItem as NxmDownloadItem;
	}

	private void SetSelection(bool isSelected)
	{
		foreach (var item in DownloadsList.Items.OfType<NxmDownloadItem>()) item.IsSelected = isSelected;
		UpdateSelectionHeader();
	}

	private void UpdateSelectionHeader()
	{
		var items = DownloadsList.Items.OfType<NxmDownloadItem>().ToArray();
		var selected = items.Count(item => item.IsSelected);
		SelectAllDownloadsCheckBox.IsChecked = selected == 0
			? false
			: selected == items.Length
				? true
				: null;
	}
}
