using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Threading;

using DivinityModManager.Models.NexusMods;
using DivinityModManager.ViewModels;
using DivinityModManager.Views;

namespace Redux.Core.Tests;

internal sealed class NxmDownloadsPaneTests
{
	public void EmbeddedPaneKeepsSelectionStateAndFocusesRequestedInboxDownload()
	{
		var app = Application.Current;
		var previousResources = app.Resources;
		var previousShutdownMode = app.ShutdownMode;
		Window? window = null;
		IDisposable? fileWatcherRegistration = null;
		var downloads = new ObservableCollection<NxmDownloadItem>(Enumerable.Range(0, 200).Select(index => new NxmDownloadItem
		{
			ProjectName = $"Rendered package {index:000}",
			FileDisplayName = $"rendered-package-{index:000}.pak",
			State = NxmDownloadState.Downloaded,
			SourceKind = AcquiredPackageSourceKind.NexusMods,
			ModId = index + 1,
			FileId = index + 1000,
			Progress = 1
		}));
		app.ShutdownMode = ShutdownMode.OnExplicitShutdown;
		try
		{
			app.Resources = WpfRenderCapture.CreateReduxApplicationResources();
			fileWatcherRegistration = WpfRenderCapture.RegisterNoOpFileWatcherService();
			var viewModel = new MainWindowViewModel();
			SetDownloads(viewModel, downloads);
			var pane = new NxmDownloadsPane { DataContext = viewModel };
			window = new Window
			{
				Content = pane,
				Width = 410,
				Height = 600,
				Left = -15000,
				Top = -15000,
				ShowInTaskbar = false,
				ShowActivated = false
			};
			window.Show();
			Settle(window);
			var inbox = (ListBox)pane.FindName("InboxDownloadsList");
			var selectAll = (CheckBox)pane.FindName("SelectAllDownloadsCheckBox");

			inbox.SelectedItems.Add(downloads[0]);
			inbox.SelectedItems.Add(downloads[100]);
			Settle(window);
			RegressionAssert.True(downloads[0].IsSelected && downloads[100].IsSelected);

			inbox.SelectedItems.Remove(downloads[0]);
			Settle(window);
			RegressionAssert.False(downloads[0].IsSelected);
			RegressionAssert.True(downloads[100].IsSelected);

			selectAll.IsChecked = true;
			selectAll.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
			Settle(window);
			RegressionAssert.True(downloads.All(item => item.IsSelected));
			inbox.ScrollIntoView(downloads[^1]);
			Settle(window);
			RegressionAssert.True(downloads[0].IsSelected && downloads[100].IsSelected && downloads[^1].IsSelected);

			inbox.ScrollIntoView(downloads[0]);
			Settle(window);
			var firstRow = (ListBoxItem)inbox.ItemContainerGenerator.ContainerFromIndex(0);
			WpfRenderCapture.AssertFullyWithin(firstRow, pane);
			downloads[0].State = NxmDownloadState.NeedsReview;
			Settle(window);
			inbox.ScrollIntoView(downloads[0]);
			Settle(window);
			firstRow = (ListBoxItem)inbox.ItemContainerGenerator.ContainerFromItem(downloads[0]);
			var reviewStyle = (Style)pane.FindResource("ReviewDownloadButton");
			var reviewAction = WpfRenderCapture.Descendants<Button>(firstRow).Single(button => ReferenceEquals(button.Style, reviewStyle));
			RegressionAssert.True(reviewAction.IsVisible && reviewAction.IsEnabled);
			foreach (var action in WpfRenderCapture.Descendants<Button>(firstRow).Where(button => button.IsVisible))
				WpfRenderCapture.AssertFullyWithin(action, pane);
			WpfRenderCapture.CaptureIfRequested(pane, "nxm-downloads-pane-narrow");

			pane.FocusDownload(downloads[^1]);
			Settle(window);
			RegressionAssert.True(ReferenceEquals(downloads[^1], inbox.SelectedItem));
		}
		finally
		{
			window?.Close();
			fileWatcherRegistration?.Dispose();
			app.Resources = previousResources;
			app.ShutdownMode = previousShutdownMode;
		}
	}

	private static void Settle(Window window)
	{
		window.UpdateLayout();
		window.Dispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
	}

	private static void SetDownloads(MainWindowViewModel viewModel, ObservableCollection<NxmDownloadItem> downloads)
	{
		var setter = typeof(MainWindowViewModel)
			.GetProperty(nameof(MainWindowViewModel.NxmDownloads))?
			.GetSetMethod(nonPublic: true);
		if (setter == null) throw new InvalidOperationException("NxmDownloads must remain assignable during Download Manager initialization.");
		setter.Invoke(viewModel, [new ReadOnlyObservableCollection<NxmDownloadItem>(downloads)]);
	}
}
