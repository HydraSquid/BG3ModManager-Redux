using DivinityModManager.AppServices;
using DivinityModManager.Models.NexusMods;
using DivinityModManager.Util;
using DivinityModManager.ViewModels;

using System.Collections.Specialized;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;

namespace DivinityModManager.Views;

public partial class ReduxNexusDownloadsWindow : AdonisUI.Controls.AdonisWindow
{
	private readonly MainWindowViewModel _viewModel;

	public ReduxNexusDownloadsWindow()
	{
		InitializeComponent();
		ReduxWindowBehavior.AttachDialogTransitions(this, 40);
		ReduxWindowBehavior.AttachRoundedCorners(this);
	}

	public ReduxNexusDownloadsWindow(MainWindow owner, MainWindowViewModel viewModel)
		: this()
	{
		Owner = owner;
		_viewModel = viewModel;
		DataContext = viewModel;
		((CollectionViewSource)Resources["PendingDownloadsView"]).Source = viewModel.NxmDownloads;
		((CollectionViewSource)Resources["InstalledDownloadsView"]).Source = viewModel.NxmDownloads;
		ReduxThemeService.Apply(Resources, viewModel.Settings.ColorTheme,
			ReduxThemeService.GetActiveTheme(viewModel.Settings), viewModel.Settings.UsesGeneratedGradients);
		if (viewModel.NxmDownloads is INotifyCollectionChanged changed)
			changed.CollectionChanged += Downloads_CollectionChanged;
		foreach (var item in viewModel.NxmDownloads) item.PropertyChanged += Download_PropertyChanged;
		Closed += (_, _) =>
		{
			if (viewModel.NxmDownloads is INotifyCollectionChanged source)
				source.CollectionChanged -= Downloads_CollectionChanged;
			foreach (var item in viewModel.NxmDownloads) item.PropertyChanged -= Download_PropertyChanged;
		};
		UpdateEmptyState();
		UpdateAssociationButton();
	}

	private static NxmDownloadItem Item(object sender) => (sender as FrameworkElement)?.Tag as NxmDownloadItem;
	private void PendingDownloads_Filter(object sender, FilterEventArgs e) =>
		e.Accepted = e.Item is NxmDownloadItem item && !item.IsInstalledHistory;
	private void InstalledDownloads_Filter(object sender, FilterEventArgs e) =>
		e.Accepted = e.Item is NxmDownloadItem item && item.IsInstalledHistory;
	private void Downloads_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
	{
		if (e.OldItems != null)
			foreach (NxmDownloadItem item in e.OldItems) item.PropertyChanged -= Download_PropertyChanged;
		if (e.NewItems != null)
			foreach (NxmDownloadItem item in e.NewItems) item.PropertyChanged += Download_PropertyChanged;
		Dispatcher.BeginInvoke(RefreshViews);
	}
	private void Download_PropertyChanged(object sender, PropertyChangedEventArgs e)
	{
		if (e.PropertyName is nameof(NxmDownloadItem.State) or nameof(NxmDownloadItem.IsInstalledHistory))
			Dispatcher.BeginInvoke(RefreshViews);
	}
	private void RefreshViews()
	{
		((CollectionViewSource)Resources["PendingDownloadsView"]).View?.Refresh();
		((CollectionViewSource)Resources["InstalledDownloadsView"]).View?.Refresh();
		UpdateEmptyState();
	}
	private void UpdateEmptyState()
	{
		IEnumerable<NxmDownloadItem> downloads = _viewModel.NxmDownloads ?? Enumerable.Empty<NxmDownloadItem>();
		var hasPending = downloads.Any(item => !item.IsInstalledHistory);
		var hasInstalled = downloads.Any(item => item.IsInstalledHistory);
		EmptyText.Visibility = hasPending ? Visibility.Collapsed : Visibility.Visible;
		InstalledEmptyText.Visibility = hasInstalled ? Visibility.Collapsed : Visibility.Visible;
		ClearInstalledButton.IsEnabled = hasInstalled;
		ClearInstalledButton.Visibility = DownloadsTabs.SelectedIndex == 1 ? Visibility.Visible : Visibility.Collapsed;
	}
	private void UpdateAssociationButton()
	{
		var status = _viewModel.GetNxmAssociationStatus();
		AssociationText.Text = status.Status switch
		{
			NxmAssociationStatus.Owned => "Disable NXM Links...",
			NxmAssociationStatus.NeedsRepair => "Repair NXM Links...",
			NxmAssociationStatus.OwnedByAnotherHandler => "NXM Links Managed Elsewhere",
			_ => "Enable NXM Links..."
		};
		AssociationIcon.SetResourceReference(DivinityModManager.Controls.ReduxIcon.StrokeDataProperty,
			status.Status == NxmAssociationStatus.Owned ? "Redux.Icon.UnlinkStroke" : "Redux.Icon.LinkStroke");
		var styleResource = status.Status switch
		{
			NxmAssociationStatus.Owned => "ReduxMinorDestructiveActionButtonStyle",
			NxmAssociationStatus.NeedsRepair => "ReduxMinorWarningActionButtonStyle",
			_ => "ReduxMinorActionButtonStyle"
		};
		AssociationButton.SetResourceReference(StyleProperty, styleResource);
		AssociationButton.IsEnabled = status.Success && status.Status != NxmAssociationStatus.OwnedByAnotherHandler;
		AssociationButton.ToolTip = status.Message;
	}

	private void DownloadsList_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
	{
		if (DownloadsList.SelectedItem is NxmDownloadItem item) _viewModel.SelectedNxmDownload = item;
	}
	private void InstalledDownloadsList_SelectionChanged(object sender, SelectionChangedEventArgs e)
	{
		if (InstalledDownloadsList.SelectedItem is NxmDownloadItem item) _viewModel.SelectedNxmDownload = item;
	}
	private void DownloadsTabs_SelectionChanged(object sender, SelectionChangedEventArgs e)
	{
		if (ReferenceEquals(e.Source, DownloadsTabs)) UpdateEmptyState();
	}

	private async void PasteLinkButton_Click(object sender, RoutedEventArgs e)
	{
		if (!Clipboard.ContainsText())
		{
			_viewModel.ShowAlert("Copy a Nexus Mod Manager link first.", AlertType.Warning);
			return;
		}
		await _viewModel.HandleNxmLinkAsync(Clipboard.GetText());
	}

	private async void AddPackageButton_Click(object sender, RoutedEventArgs e)
	{
		var dialog = new Microsoft.Win32.OpenFileDialog
		{
			Title = "Add Packages to Download Manager",
			Filter = "Supported packages|*.pak;*.lsv;*.zip;*.7z;*.7zip;*.rar;*.tar;*.gz;*.gzip;*.tgz|All files|*.*",
			Multiselect = true,
			CheckFileExists = true
		};
		if (dialog.ShowDialog(this) == true)
			await _viewModel.AddLocalPackagesToDownloadManagerAsync(dialog.FileNames);
	}

	private async void Window_PreviewDrop(object sender, DragEventArgs e)
	{
		if (!e.Data.GetDataPresent(DataFormats.FileDrop)
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

	private async void PauseAllButton_Click(object sender, RoutedEventArgs e) => await _viewModel.PauseAllNxmDownloadsAsync();
	private async void ResumeAllButton_Click(object sender, RoutedEventArgs e) => await _viewModel.ResumeAllNxmDownloadsAsync();
	private async void PauseButton_Click(object sender, RoutedEventArgs e) => await _viewModel.PauseNxmDownloadAsync(Item(sender));
	private async void ResumeButton_Click(object sender, RoutedEventArgs e) => await _viewModel.ResumeNxmDownloadAsync(Item(sender));
	private async void ReviewButton_Click(object sender, RoutedEventArgs e) => await _viewModel.ReviewNxmDownloadAsync(Item(sender), this);
	private async void RemoveButton_Click(object sender, RoutedEventArgs e) => await _viewModel.RemoveNxmDownloadAsync(Item(sender));
	private async void ClearInstalledButton_Click(object sender, RoutedEventArgs e) => await _viewModel.ClearInstalledNxmHistoryAsync();
	private void NexusButton_Click(object sender, RoutedEventArgs e)
	{
		if (Item(sender)?.NexusPage is { } page) ProcessHelper.TryOpenUrl(page.ToString());
	}
	private void OpenFolderButton_Click(object sender, RoutedEventArgs e) =>
		ProcessHelper.TryOpenPath(_viewModel.NxmDownloadsDirectory, Directory.Exists);
	private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();
	private void AssociationButton_Click(object sender, RoutedEventArgs e)
	{
		_viewModel.ConfigureNxmAssociation();
		UpdateAssociationButton();
	}
}
