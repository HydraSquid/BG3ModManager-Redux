using DivinityModManager.AppServices;
using DivinityModManager.Models.NexusMods;
using DivinityModManager.Util;
using DivinityModManager.ViewModels;

using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;

namespace DivinityModManager.Views;

public partial class NxmDownloadsPane : UserControl
{
	private MainWindowViewModel _viewModel;
	private INotifyCollectionChanged _downloads;
	private INotifyCollectionChanged _archives;
	private readonly HashSet<NxmDownloadItem> _subscribedDownloads = new();
	private bool _updatingSelectedDownload;

	public NxmDownloadsPane()
	{
		InitializeComponent();
		DataContextChanged += NxmDownloadsPane_DataContextChanged;
		Loaded += (_, _) => UpdateEmptyState();
		IsVisibleChanged += (_, _) => { if (IsVisible) UpdateAssociationButton(); };
	}

	public void FocusDownload(NxmDownloadItem item)
	{
		if (item == null) return;
		Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.ContextIdle, new Action(() =>
		{
			var list = item.IsInstalledHistory ? HistoryDownloadsList : InboxDownloadsList;
			DownloadsTabs.SelectedIndex = item.IsInstalledHistory ? 1 : 0;
			if (!list.Items.Contains(item)) return;
			list.SelectedItem = item;
			list.ScrollIntoView(item);
			list.Focus();
		}));
	}

	private void NxmDownloadsPane_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
	{
		DetachViewModel();
		_viewModel = e.NewValue as MainWindowViewModel;
		if (_viewModel == null) return;
		_viewModel.PropertyChanged += ViewModel_PropertyChanged;
		AttachCollections();
		UpdateEmptyState();
		UpdateAssociationButton();
	}

	private void AttachCollections()
	{
		if (_downloads != null) _downloads.CollectionChanged -= Downloads_CollectionChanged;
		if (_archives != null) _archives.CollectionChanged -= Archives_CollectionChanged;
		foreach (var item in _subscribedDownloads) item.PropertyChanged -= Download_PropertyChanged;
		_subscribedDownloads.Clear();

		_downloads = _viewModel?.NxmDownloads as INotifyCollectionChanged;
		_archives = _viewModel?.RetainedPackageArchives as INotifyCollectionChanged;
		if (_downloads != null) _downloads.CollectionChanged += Downloads_CollectionChanged;
		if (_archives != null) _archives.CollectionChanged += Archives_CollectionChanged;
		foreach (var item in _viewModel?.NxmDownloads ?? Enumerable.Empty<NxmDownloadItem>()) SubscribeDownload(item);
	}

	private void DetachViewModel()
	{
		if (_viewModel == null) return;
		if (_downloads != null) _downloads.CollectionChanged -= Downloads_CollectionChanged;
		if (_archives != null) _archives.CollectionChanged -= Archives_CollectionChanged;
		foreach (var item in _subscribedDownloads) item.PropertyChanged -= Download_PropertyChanged;
		_subscribedDownloads.Clear();
		_downloads = null;
		_archives = null;
		_viewModel.PropertyChanged -= ViewModel_PropertyChanged;
		_viewModel = null;
	}

	private void Downloads_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
	{
		if (e.OldItems != null)
			foreach (NxmDownloadItem item in e.OldItems) UnsubscribeDownload(item);
		if (e.NewItems != null)
			foreach (NxmDownloadItem item in e.NewItems) SubscribeDownload(item);
		Dispatcher.BeginInvoke(RefreshViews);
	}

	private void SubscribeDownload(NxmDownloadItem item)
	{
		if (item != null && _subscribedDownloads.Add(item)) item.PropertyChanged += Download_PropertyChanged;
	}

	private void UnsubscribeDownload(NxmDownloadItem item)
	{
		if (item != null && _subscribedDownloads.Remove(item)) item.PropertyChanged -= Download_PropertyChanged;
	}

	private void Archives_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e) => Dispatcher.BeginInvoke(UpdateEmptyState);

	private void Download_PropertyChanged(object sender, PropertyChangedEventArgs e)
	{
		if (e.PropertyName is nameof(NxmDownloadItem.State) or nameof(NxmDownloadItem.IsInstalledHistory))
			Dispatcher.BeginInvoke(RefreshViews);
	}

	private void ViewModel_PropertyChanged(object sender, PropertyChangedEventArgs e)
	{
		if (e.PropertyName is nameof(MainWindowViewModel.NxmDownloads) or nameof(MainWindowViewModel.RetainedPackageArchives))
		{
			AttachCollections();
			Dispatcher.BeginInvoke(RefreshViews);
		}
		else if (e.PropertyName == nameof(MainWindowViewModel.SelectedNxmDownload) && !_updatingSelectedDownload)
		{
			FocusDownload(_viewModel.SelectedNxmDownload);
		}
		else if (e.PropertyName == nameof(MainWindowViewModel.DownloadManagerInstallIsActive))
		{
			Dispatcher.BeginInvoke(UpdateEmptyState);
		}
	}

	private void PendingDownloads_Filter(object sender, FilterEventArgs e) => e.Accepted = e.Item is NxmDownloadItem item && !item.IsInstalledHistory;
	private void InstalledDownloads_Filter(object sender, FilterEventArgs e) => e.Accepted = e.Item is NxmDownloadItem item && item.IsInstalledHistory;

	private void RefreshViews()
	{
		((CollectionViewSource)Resources["PendingDownloadsView"]).View?.Refresh();
		((CollectionViewSource)Resources["InstalledDownloadsView"]).View?.Refresh();
		UpdateEmptyState();
	}

	private void UpdateEmptyState()
	{
		var downloads = _viewModel?.NxmDownloads ?? Enumerable.Empty<NxmDownloadItem>();
		var hasPending = downloads.Any(item => !item.IsInstalledHistory);
		var hasInstalled = downloads.Any(item => item.IsInstalledHistory);
		InboxEmptyText.Visibility = hasPending ? Visibility.Collapsed : Visibility.Visible;
		HistoryEmptyText.Visibility = hasInstalled ? Visibility.Collapsed : Visibility.Visible;
		ArchivesEmptyText.Visibility = _viewModel?.RetainedPackageArchives.Count > 0 ? Visibility.Collapsed : Visibility.Visible;
		ClearHistoryButton.Visibility = DownloadsTabs.SelectedIndex == 1 ? Visibility.Visible : Visibility.Collapsed;
		ClearHistoryButton.IsEnabled = hasInstalled;
		ClearArchivesButton.Visibility = DownloadsTabs.SelectedIndex == 2 ? Visibility.Visible : Visibility.Collapsed;
		ClearArchivesButton.IsEnabled = _viewModel?.RetainedPackageArchives.Count > 0;
		OpenFolderText.Text = DownloadsTabs.SelectedIndex == 2 ? "Open Archives" : "Open Folder";
	}

	private static NxmDownloadItem Item(object sender) => (sender as FrameworkElement)?.Tag as NxmDownloadItem;

	private void UpdateAssociationButton()
	{
		if (_viewModel == null) return;
		var status = _viewModel.GetNxmAssociationStatus();
		AssociationText.Text = status.Status switch
		{
			NxmAssociationStatus.Owned => "Disable NXM Links...",
			NxmAssociationStatus.NeedsRepair => "Repair NXM Links...",
			NxmAssociationStatus.OwnedByAnotherHandler => "NXM Links Managed Elsewhere",
			_ => "Enable NXM Links..."
		};
		AssociationIcon.SetResourceReference(Controls.ReduxIcon.StrokeDataProperty,
			status.Status == NxmAssociationStatus.Owned ? "Redux.Icon.UnlinkStroke" : "Redux.Icon.LinkStroke");
		AssociationButton.SetResourceReference(StyleProperty, status.Status switch
		{
			NxmAssociationStatus.Owned => "DownloadDestructiveActionButton",
			NxmAssociationStatus.NeedsRepair => "DownloadWarningActionButton",
			_ => "DownloadNexusActionButton"
		});
		AssociationButton.IsEnabled = status.Success && status.Status != NxmAssociationStatus.OwnedByAnotherHandler;
		AssociationButton.ToolTip = status.Message;
	}

	private void AssociationButton_Click(object sender, RoutedEventArgs e)
	{
		_viewModel?.ConfigureNxmAssociation();
		UpdateAssociationButton();
	}

	private IReadOnlyList<NxmDownloadItem> SelectedDownloads() => InboxDownloadsList.SelectedItems
		.OfType<NxmDownloadItem>()
		.Concat(HistoryDownloadsList.SelectedItems.OfType<NxmDownloadItem>())
		.GroupBy(item => item.Id, StringComparer.Ordinal)
		.Select(group => group.First())
		.ToArray();

	private async Task RunCommandAsync(Func<Task> command)
	{
		if (command == null) return;
		try { await command(); }
		catch (Exception ex)
		{
			DivinityApp.Log($"Nexus download command failed: {ex.GetType().Name}");
			ReduxMessageBox.Show(Window.GetWindow(this), ex.Message, "Nexus Downloads",
				MessageBoxButton.OK, MessageBoxImage.Error, MessageBoxResult.OK);
		}
	}

	private void CloseButton_Click(object sender, RoutedEventArgs e)
	{
		if (_viewModel == null) return;
		_viewModel.Settings.NxmDownloadsPaneVisible = false;
		_viewModel.SaveSettings();
	}

	private async void InstallSelectedButton_Click(object sender, RoutedEventArgs e) => await RunCommandAsync(() =>
		_viewModel?.InstallSelectedNxmDownloadsAsync(SelectedDownloads(), Window.GetWindow(this)) ?? Task.CompletedTask);
	private async void InstallAllButton_Click(object sender, RoutedEventArgs e) => await RunCommandAsync(() =>
		_viewModel?.InstallAllNxmDownloadsAsync(Window.GetWindow(this)) ?? Task.CompletedTask);
	private async void PauseAllButton_Click(object sender, RoutedEventArgs e) => await RunCommandAsync(() => _viewModel?.PauseAllNxmDownloadsAsync() ?? Task.CompletedTask);
	private async void ResumeAllButton_Click(object sender, RoutedEventArgs e) => await RunCommandAsync(() => _viewModel?.ResumeAllNxmDownloadsAsync() ?? Task.CompletedTask);
	private async void RemoveSelectedButton_Click(object sender, RoutedEventArgs e) => await RunCommandAsync(() =>
		_viewModel?.RemoveSelectedNxmDownloadsAsync(SelectedDownloads()) ?? Task.CompletedTask);
	private async void PauseButton_Click(object sender, RoutedEventArgs e) => await RunCommandAsync(() => _viewModel?.PauseNxmDownloadAsync(Item(sender)) ?? Task.CompletedTask);
	private async void ResumeButton_Click(object sender, RoutedEventArgs e) => await RunCommandAsync(() => _viewModel?.ResumeNxmDownloadAsync(Item(sender)) ?? Task.CompletedTask);
	private async void ReviewButton_Click(object sender, RoutedEventArgs e) => await RunCommandAsync(() => _viewModel?.ReviewNxmDownloadAsync(Item(sender), Window.GetWindow(this)) ?? Task.CompletedTask);
	private async void DependenciesButton_Click(object sender, RoutedEventArgs e) => await RunCommandAsync(() => _viewModel?.ReviewNxmDownloadDependenciesAsync(Item(sender), Window.GetWindow(this)) ?? Task.CompletedTask);
	private async void RemoveButton_Click(object sender, RoutedEventArgs e) => await RunCommandAsync(() => _viewModel?.RemoveNxmDownloadAsync(Item(sender)) ?? Task.CompletedTask);
	private async void ClearHistoryButton_Click(object sender, RoutedEventArgs e) => await RunCommandAsync(() => _viewModel?.ClearInstalledNxmHistoryAsync() ?? Task.CompletedTask);
	private async void ClearArchivesButton_Click(object sender, RoutedEventArgs e) => await RunCommandAsync(() => _viewModel?.ClearRetainedPackageArchivesAsync(Window.GetWindow(this)) ?? Task.CompletedTask);
	private async void ReinstallArchiveButton_Click(object sender, RoutedEventArgs e) => await RunCommandAsync(() =>
		_viewModel?.ReinstallRetainedPackageAsync((sender as FrameworkElement)?.Tag as RetainedPackageArchiveEntry, Window.GetWindow(this)) ?? Task.CompletedTask);

	private void NexusButton_Click(object sender, RoutedEventArgs e)
	{
		if (Item(sender)?.NexusPage is { } page) ProcessHelper.TryOpenUrl(page.ToString());
	}

	private void ArchiveNexusButton_Click(object sender, RoutedEventArgs e)
	{
		if ((sender as FrameworkElement)?.Tag is RetainedPackageArchiveEntry { NexusPage: { } page }) ProcessHelper.TryOpenUrl(page.ToString());
	}

	private void OpenFolderButton_Click(object sender, RoutedEventArgs e)
	{
		if (_viewModel == null) return;
		var directory = DownloadsTabs.SelectedIndex == 2 ? _viewModel.RetainedPackageArchiveDirectory : _viewModel.NxmDownloadsDirectory;
		if (Directory.Exists(directory)) ProcessHelper.TryOpenPath(directory, Directory.Exists);
		else _viewModel.ShowAlert("The archive library has not been created yet.", AlertType.Info, 12);
	}

	private async void AddPackageButton_Click(object sender, RoutedEventArgs e)
	{
		if (_viewModel == null) return;
		var dialog = new Microsoft.Win32.OpenFileDialog
		{
			Title = "Add Packages to Download Manager",
			Filter = "Supported packages|*.pak;*.lsv;*.zip;*.7z;*.7zip;*.rar;*.tar;*.gz;*.gzip;*.tgz|All files|*.*",
			Multiselect = true,
			CheckFileExists = true
		};
		if (dialog.ShowDialog(Window.GetWindow(this)) == true) await _viewModel.AddLocalPackagesToDownloadManagerAsync(dialog.FileNames);
	}

	private async void PasteLinkButton_Click(object sender, RoutedEventArgs e)
	{
		if (_viewModel == null) return;
		if (!Clipboard.ContainsText())
		{
			_viewModel.ShowAlert("Copy a Nexus Mod Manager link first.", AlertType.Warning);
			return;
		}
		await _viewModel.HandleNxmLinkAsync(Clipboard.GetText());
	}

	private async void Pane_PreviewDrop(object sender, DragEventArgs e)
	{
		if (_viewModel == null || !e.Data.GetDataPresent(DataFormats.FileDrop)
			|| e.Data.GetData(DataFormats.FileDrop) is not string[] paths) return;
		e.Handled = true;
		var supported = paths.Where(MainWindowViewModel.IsSupportedDownloadManagerInput).ToArray();
		if (supported.Length != paths.Length)
		{
			_viewModel.ShowAlert("Download Manager accepts PAK, save, ZIP, 7z, RAR, TAR, and GZip package files.", AlertType.Warning, 25);
			return;
		}
		await _viewModel.AddLocalPackagesToDownloadManagerAsync(supported);
	}

	private void DownloadsTabs_SelectionChanged(object sender, SelectionChangedEventArgs e)
	{
		if (ReferenceEquals(e.Source, DownloadsTabs)) UpdateEmptyState();
	}

	private void DownloadsList_SelectionChanged(object sender, SelectionChangedEventArgs e)
	{
		foreach (var item in e.RemovedItems.OfType<NxmDownloadItem>()) item.IsSelected = false;
		foreach (var item in e.AddedItems.OfType<NxmDownloadItem>()) item.IsSelected = true;
		if (e.AddedItems.OfType<NxmDownloadItem>().LastOrDefault() is { } selected && _viewModel != null)
		{
			_updatingSelectedDownload = true;
			try { _viewModel.SelectedNxmDownload = selected; }
			finally { _updatingSelectedDownload = false; }
		}
		UpdateSelectAllState();
	}

	private void SelectAllDownloads_Click(object sender, RoutedEventArgs e)
	{
		if (SelectAllDownloadsCheckBox.IsChecked == true) InboxDownloadsList.SelectAll();
		else InboxDownloadsList.UnselectAll();
		UpdateSelectAllState();
	}

	private void UpdateSelectAllState()
	{
		var count = InboxDownloadsList.Items.Count;
		var selected = InboxDownloadsList.SelectedItems.Count;
		SelectAllDownloadsCheckBox.IsChecked = selected == 0 ? false : selected == count ? true : null;
	}
}
