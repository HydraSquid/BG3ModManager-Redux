using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Threading;

using DivinityModManager.Controls;
using DivinityModManager.Models;
using DivinityModManager.Models.NexusMods;
using DivinityModManager.Util;
using DivinityModManager.ViewModels;
using DivinityModManager.Views;
using DynamicData;

namespace Redux.Core.Tests;

internal sealed class PaneSplitterTests
{
	public void OverridePaneResizesAndRetainsHeightAcrossCollapse()
	{
		var app = Application.Current;
		var resources = app.Resources;
		var shutdown = app.ShutdownMode;
		var reduceMotion = ReduxWindowBehavior.ReduceMotion;
		var disableEffects = ReduxWindowBehavior.BackgroundEffectsDisabled;
		Window? window = null;
		using var watcher = WpfRenderCapture.RegisterNoOpFileWatcherService();
		try
		{
			app.Resources = WpfRenderCapture.CreateReduxApplicationResources();
			app.ShutdownMode = ShutdownMode.OnExplicitShutdown;
			var viewModel = new OverrideFixtureViewModel();
			var layout = new HorizontalModLayout { DataContext = viewModel, ViewModel = viewModel, Width = 1200, Height = 760, HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top };
			window = new Window { Content = layout, Width = 1200, Height = 760, WindowStyle = WindowStyle.None, Left = -15000, Top = -15000, ShowInTaskbar = false, ShowActivated = false };
			window.Show();
			Settle(window);
			ReduxWindowBehavior.ConfigureAccessibility(true, disableEffects);
			viewModel.AddOverrides();
			Settle(window);
			var splitter = (GridSplitter)layout.FindName("OverrideModsGridSplitter");
			var section = (FrameworkElement)layout.FindName("AlwaysLoadedSectionGrid");
			var toggle = (ToggleButton)layout.FindName("AlwaysLoadedToggleButton");
			var list = (ModListView)layout.FindName("ForceLoadedModsListView");
			var active = (ModListView)layout.FindName("ActiveModsListView");
			RegressionAssert.True(splitter.IsVisible && list.Items.Count == 12);
			var before = section.ActualHeight;
			splitter.RaiseEvent(new DragStartedEventArgs(0, 0) { RoutedEvent = Thumb.DragStartedEvent });
			splitter.RaiseEvent(new DragDeltaEventArgs(0, -80) { RoutedEvent = Thumb.DragDeltaEvent });
			Settle(window);
			splitter.RaiseEvent(new DragCompletedEventArgs(0, -80, false) { RoutedEvent = Thumb.DragCompletedEvent });
			Settle(window);
			var expandedHeight = section.ActualHeight;
			RegressionAssert.True(expandedHeight > before + 70);
			RegressionAssert.True(Double.IsNaN(list.Height) && active.ActualHeight >= 96);
			AssertChevron("Redux.Icon.ChevronDownStroke");
			WpfRenderCapture.CaptureIfRequested(layout, "override-pane-expanded-wide");
			toggle.IsChecked = false;
			Settle(window);
			RegressionAssert.False(splitter.IsVisible || list.IsVisible);
			RegressionAssert.True(section.ActualHeight < before);
			AssertChevron("Redux.Icon.ChevronUpStroke");
			WpfRenderCapture.CaptureIfRequested(layout, "override-pane-collapsed-wide");
			toggle.IsChecked = true;
			Settle(window);
			RegressionAssert.True(Math.Abs(section.ActualHeight - expandedHeight) < 1);
			viewModel.AddOverrides();
			Settle(window);
			RegressionAssert.True(Math.Abs(section.ActualHeight - expandedHeight) < 1);
			layout.Width = 1000;
			layout.Height = 500;
			Settle(window);
			WpfRenderCapture.AssertFullyWithin(list, layout);
			RegressionAssert.True(active.ActualHeight >= 96);
			WpfRenderCapture.CaptureIfRequested(layout, "override-pane-expanded-compact");
			splitter.RaiseEvent(new DragStartedEventArgs(0, 0) { RoutedEvent = Thumb.DragStartedEvent });
			splitter.RaiseEvent(new DragDeltaEventArgs(0, -10000) { RoutedEvent = Thumb.DragDeltaEvent });
			Settle(window);
			splitter.RaiseEvent(new DragCompletedEventArgs(0, -10000, false) { RoutedEvent = Thumb.DragCompletedEvent });
			Settle(window);
			RegressionAssert.True(active.ActualHeight >= 96 && section.ActualHeight >= 72);
			splitter.RaiseEvent(new DragStartedEventArgs(0, 0) { RoutedEvent = Thumb.DragStartedEvent });
			splitter.RaiseEvent(new DragDeltaEventArgs(0, 10000) { RoutedEvent = Thumb.DragDeltaEvent });
			Settle(window);
			splitter.RaiseEvent(new DragCompletedEventArgs(0, 10000, false) { RoutedEvent = Thumb.DragCompletedEvent });
			Settle(window);
			RegressionAssert.True(active.ActualHeight >= 96 && section.ActualHeight >= 72);
			void AssertChevron(string key) => RegressionAssert.True(ReferenceEquals(
				WpfRenderCapture.Descendants<System.Windows.Shapes.Path>(toggle).Single().Data, layout.FindResource(key)));
		}
		finally
		{
			window?.Close();
			ReduxWindowBehavior.ConfigureAccessibility(reduceMotion, disableEffects);
			app.Resources = resources;
			app.ShutdownMode = shutdown;
		}
	}

	private sealed class OverrideFixtureViewModel : MainWindowViewModel
	{
		public void AddOverrides()
		{
			for (var index = 0; index < 12; index++)
				mods.AddOrUpdate(new DivinityModData { UUID = Guid.NewGuid().ToString(), Name = $"Override example {index + 1}", IsForceLoaded = true });
		}
	}

	public void PaneDividersResizeOnlyTheirNeighborsAndRestoreResponsiveSizing()
	{
		var app = Application.Current;
		var shutdown = app.ShutdownMode;
		app.ShutdownMode = ShutdownMode.OnExplicitShutdown;
		// Hosted Windows runners can clamp top-level windows to their virtual desktop.
		// Give this splitter fixture its own deterministic layout space so a 40px drag
		// tests neighbor isolation rather than accidentally hitting a pane minimum.
		var grid = new Grid { Width = 1400 };
		foreach (var width in new[]
		{
			new GridLength(220), new GridLength(4), new GridLength(1, GridUnitType.Star),
			new GridLength(4), new GridLength(1, GridUnitType.Star), new GridLength(4), new GridLength(360)
		})
			grid.ColumnDefinitions.Add(new ColumnDefinition { Width = width });
		grid.ColumnDefinitions[2].MinWidth = 180;
		grid.ColumnDefinitions[4].MinWidth = 180;
		grid.ColumnDefinitions[6].MinWidth = 280;
		var splitters = new[] { 1, 3, 5 }.Select(index =>
		{
			var splitter = new PaneGridSplitter
			{
				Width = 4,
				HorizontalAlignment = HorizontalAlignment.Stretch,
				VerticalAlignment = VerticalAlignment.Stretch
			};
			Grid.SetColumn(splitter, index);
			grid.Children.Add(splitter);
			return splitter;
		}).ToArray();
		var window = new Window
		{
			Content = grid,
			Width = 1400,
			Height = 500,
			Left = -15000,
			Top = -15000,
			ShowInTaskbar = false,
			ShowActivated = false
		};
		try
		{
			window.Show();
			Settle();
			WpfRenderCapture.CaptureIfRequested(grid, "pane-splitters-expanded");
			foreach (var splitter in splitters)
			{
				var before = Sizes();
				BeginDrag(splitter);
				Delta(splitter, 40);
				AssertNeighbors(before, Grid.GetColumn(splitter), 40);
				EndDrag(splitter, false);
				AssertUnits();

				before = Sizes();
				BeginDrag(splitter);
				Delta(splitter, -30);
				EndDrag(splitter, true);
				for (var index = 0; index < before.Length; index++) Near(before[index], Sizes()[index]);
				AssertUnits();
			}

			var widths = Sizes();
			grid.Width += 200;
			window.Width += 200;
			Settle();
			Near(widths[0], Sizes()[0]);
			RegressionAssert.True(Sizes()[2] > widths[2] && Sizes()[4] > widths[4]);
			Near(widths[2] / widths[4], Sizes()[2] / Sizes()[4]);

			grid.ColumnDefinitions[4].MinWidth = 52;
			grid.ColumnDefinitions[4].MaxWidth = 52;
			grid.ColumnDefinitions[4].Width = new GridLength(52);
			Settle();
			widths = Sizes();
			BeginDrag(splitters[1]);
			Delta(splitters[1], 100);
			EndDrag(splitters[1], false);
			for (var index = 0; index < widths.Length; index++) Near(widths[index], Sizes()[index]);

			grid.ColumnDefinitions[6].MinWidth = 0;
			grid.ColumnDefinitions[6].MaxWidth = 0;
			grid.ColumnDefinitions[6].Width = new GridLength(0);
			grid.ColumnDefinitions[5].Width = new GridLength(0);
			Settle();
			widths = Sizes();
			BeginDrag(splitters[2]);
			Delta(splitters[2], -100);
			EndDrag(splitters[2], false);
			for (var index = 0; index < widths.Length; index++) Near(widths[index], Sizes()[index]);
			WpfRenderCapture.CaptureIfRequested(grid, "pane-splitters-collapsed");
		}
		finally
		{
			window.Close();
			app.ShutdownMode = shutdown;
		}

		double[] Sizes() => grid.ColumnDefinitions.Select(column => column.ActualWidth).ToArray();
		void Settle()
		{
			window.UpdateLayout();
			window.Dispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
		}
		void AssertUnits()
		{
			RegressionAssert.True(grid.ColumnDefinitions[0].Width.IsAbsolute);
			RegressionAssert.True(grid.ColumnDefinitions[2].Width.IsStar && grid.ColumnDefinitions[4].Width.IsStar);
			RegressionAssert.True(grid.ColumnDefinitions[6].Width.IsAbsolute);
		}
		void BeginDrag(PaneGridSplitter splitter)
		{
			splitter.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice, 0, MouseButton.Left)
			{
				RoutedEvent = UIElement.PreviewMouseLeftButtonDownEvent
			});
			splitter.RaiseEvent(new DragStartedEventArgs(0, 0) { RoutedEvent = Thumb.DragStartedEvent });
		}
		void Delta(PaneGridSplitter splitter, double delta)
		{
			splitter.RaiseEvent(new DragDeltaEventArgs(delta, 0) { RoutedEvent = Thumb.DragDeltaEvent });
			Settle();
		}
		void EndDrag(PaneGridSplitter splitter, bool canceled)
		{
			splitter.RaiseEvent(new DragCompletedEventArgs(0, 0, canceled) { RoutedEvent = Thumb.DragCompletedEvent });
			Settle();
		}
		void AssertNeighbors(double[] before, int divider, double delta)
		{
			for (var index = 0; index < before.Length; index++)
				Near(before[index] + (index == divider - 1 ? delta : index == divider + 1 ? -delta : 0), Sizes()[index]);
		}
		static void Near(double expected, double actual)
		{
			if (Math.Abs(expected - actual) > 0.1)
				throw new InvalidOperationException($"Expected pane size {expected}, got {actual}.");
		}
	}

	public void HorizontalLayoutRendersEmbeddedDownloadsWithinWideAndCompactBounds()
	{
		var app = Application.Current;
		var previousResources = app.Resources;
		var previousShutdownMode = app.ShutdownMode;
		Window? window = null;
		IDisposable? fileWatcherRegistration = null;
		app.ShutdownMode = ShutdownMode.OnExplicitShutdown;
		try
		{
			app.Resources = WpfRenderCapture.CreateReduxApplicationResources();
			fileWatcherRegistration = WpfRenderCapture.RegisterNoOpFileWatcherService();
			var downloads = new ObservableCollection<NxmDownloadItem>(Enumerable.Range(0, 8).Select(index => new NxmDownloadItem
			{
				ProjectName = $"Layout package {index}",
				FileDisplayName = $"layout-package-{index}.pak",
				State = NxmDownloadState.Downloaded,
				SourceKind = AcquiredPackageSourceKind.NexusMods,
				ModId = index + 1,
				FileId = index + 1000,
				Progress = 1
			}));
			var viewModel = new MainWindowViewModel();
			SetDownloads(viewModel, downloads);
			viewModel.Settings.NxmDownloadsPaneVisible = true;
			viewModel.Settings.NxmDownloadsPaneWidth = 460;
			var layout = new HorizontalModLayout { DataContext = viewModel, ViewModel = viewModel };
			window = new Window
			{
				Content = layout,
				Width = 1600,
				Height = 900,
				Left = -15000,
				Top = -15000,
				ShowInTaskbar = false,
				ShowActivated = false,
				WindowStyle = WindowStyle.None
			};
			window.Show();
			Settle(window);
			AssertVisiblePanesFit(layout, "horizontal-downloads-wide-1600");

			window.Width = 1100;
			Settle(window);
			AssertVisiblePanesFit(layout, "horizontal-downloads-compact-1100");

			viewModel.Settings.NxmDownloadsPaneVisible = false;
			Settle(window);
			var downloadsPane = (NxmDownloadsPane)layout.FindName("DownloadsPane");
			RegressionAssert.Equal(Visibility.Collapsed, downloadsPane.Visibility);
			RegressionAssert.True(downloadsPane.ActualWidth <= 0.1);
			WpfRenderCapture.CaptureIfRequested(layout, "horizontal-downloads-collapsed-layout");
			var inactiveMods = (FrameworkElement)layout.FindName("InactiveModsListView");
			WpfRenderCapture.CaptureIfRequested(inactiveMods, "horizontal-downloads-collapsed-inactive");
			WpfRenderCapture.AssertFullyWithin(inactiveMods, layout);
		}
		finally
		{
			window?.Close();
			fileWatcherRegistration?.Dispose();
			app.Resources = previousResources;
			app.ShutdownMode = previousShutdownMode;
		}
	}

	private static void AssertVisiblePanesFit(HorizontalModLayout layout, string captureName)
	{
		var downloadsPane = (NxmDownloadsPane)layout.FindName("DownloadsPane");
		var activeMods = (FrameworkElement)layout.FindName("ActiveModsListView");
		var inactiveMods = (FrameworkElement)layout.FindName("InactiveModsListView");
		RegressionAssert.Equal(Visibility.Visible, downloadsPane.Visibility);
		RegressionAssert.True(downloadsPane.ActualWidth > 0);
		WpfRenderCapture.CaptureIfRequested(layout, $"{captureName}-layout");
		WpfRenderCapture.CaptureIfRequested(downloadsPane, $"{captureName}-downloads");
		WpfRenderCapture.CaptureIfRequested(activeMods, $"{captureName}-active");
		WpfRenderCapture.CaptureIfRequested(inactiveMods, $"{captureName}-inactive");
		var grid = (Grid)layout.FindName("WorkspaceGrid");
		if (grid.ActualWidth > layout.ActualWidth + 0.5)
			throw new InvalidOperationException($"Workspace width={grid.ActualWidth}, max={grid.MaxWidth}, desired={grid.DesiredSize.Width}; columns: "
				+ String.Join("; ", grid.ColumnDefinitions.Select(column => $"{column.Width} actual={column.ActualWidth} min={column.MinWidth} max={column.MaxWidth}")));
		WpfRenderCapture.AssertFullyWithin(downloadsPane, layout);
		WpfRenderCapture.AssertFullyWithin(activeMods, layout);
		WpfRenderCapture.AssertFullyWithin(inactiveMods, layout);
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
