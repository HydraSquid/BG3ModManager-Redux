using DivinityModManager.Models.NexusMods;
using DivinityModManager.Util;
using DivinityModManager.ViewModels;

using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;

namespace DivinityModManager.Views;

public partial class NxmDownloadsPane : UserControl
{
	private MainWindowViewModel ViewModel => DataContext as MainWindowViewModel;
	public NxmDownloadsPane()
	{
		InitializeComponent();
		var columns = ((GridView)DownloadsList.View).Columns;
		columns.CollectionChanged += (_, _) =>
		{
			// Keep the fill column last while allowing the data columns to be reordered.
			if (columns.IndexOf(ActionsColumn) == columns.Count - 1) return;
			Dispatcher.BeginInvoke(new Action(() =>
			{
				var index = columns.IndexOf(ActionsColumn);
				if (index >= 0 && index != columns.Count - 1) columns.Move(index, columns.Count - 1);
			}));
		};
	}

	private void UpdateActionsColumnWidth(object sender, RoutedEventArgs e)
	{
		if (ActionsColumn == null || !DownloadsList.IsVisible) return;
		var scroll = DownloadsList.FindVisualChildren<ScrollViewer>().FirstOrDefault();
		if (scroll == null || scroll.ViewportWidth <= 0) return;

		// The horizontal stack measures the shared slots without the current cell's width
		// constraint. Reserve the cell's 12px presenter padding plus its 6px leading margin.
		var contentWidth = DownloadsList.FindVisualChildren<Grid>()
			.Where(grid => grid.Name == "DownloadActionSlots")
			.Select(grid => grid.ActualWidth + 18).DefaultIfEmpty(80).Max();
		var otherWidth = ((GridView)DownloadsList.View).Columns
			.Where(column => column != ActionsColumn).Sum(column => column.ActualWidth);
		var width = Math.Max(contentWidth, scroll.ViewportWidth - otherWidth - 6);
		if (Double.IsNaN(ActionsColumn.Width) || Math.Abs(ActionsColumn.Width - width) > 0.5)
			ActionsColumn.Width = width;
	}

	public void FocusDownload(NxmDownloadItem item)
	{
		Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Loaded, new Action(() =>
		{
			if (!DownloadsList.Items.Contains(item)) return;
			DownloadsList.SelectedItem = item;
			DownloadsList.ScrollIntoView(item);
			DownloadsList.Focus();
		}));
	}

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
	private async void RemoveSelected_Click(object sender, RoutedEventArgs e) => await RunCommandAsync(ViewModel.RemoveSelectedNxmDownloadsAsync);

	private async void DownloadsList_PreviewKeyDown(object sender, KeyEventArgs e)
	{
		if (e.Key != Key.Delete || Keyboard.Modifiers != ModifierKeys.None
			|| e.OriginalSource is TextBoxBase || (e.OriginalSource as DependencyObject)?.FindVisualParent<TextBoxBase>() != null) return;
		e.Handled = true;
		await RunCommandAsync(ViewModel.RemoveSelectedNxmDownloadsAsync);
	}

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

	private void SelectAllDownloads_Click(object sender, RoutedEventArgs e) => SetSelection(DownloadsList.SelectedItems.Count < DownloadsList.Items.Count);
	private void SelectAllMenu_Click(object sender, RoutedEventArgs e) => SetSelection(true);
	private void ClearSelectionMenu_Click(object sender, RoutedEventArgs e) => SetSelection(false);
	private void DownloadsList_SelectionChanged(object sender, SelectionChangedEventArgs e)
	{
		foreach (var item in e.RemovedItems.OfType<NxmDownloadItem>().ToArray()) item.IsSelected = false;
		foreach (var item in e.AddedItems.OfType<NxmDownloadItem>().ToArray()) item.IsSelected = true;
		UpdateSelectionHeader();
	}
	private void DownloadsList_ContextMenuOpening(object sender, ContextMenuEventArgs e)
	{
		if (e.OriginalSource is not DependencyObject source) return;
		DownloadsList.Tag = (source as FrameworkElement)?.DataContext as NxmDownloadItem
			?? source.FindVisualParent<ListViewItem>()?.DataContext as NxmDownloadItem
			?? DownloadsList.SelectedItem as NxmDownloadItem;
	}

	private void SetSelection(bool isSelected)
	{
		if (isSelected) DownloadsList.SelectAll();
		else DownloadsList.UnselectAll();
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
