using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Threading;
using DivinityModManager.Controls;

namespace Redux.Core.Tests;

internal sealed class PaneSplitterTests
{
	public void PaneDividersResizeOnlyTheirNeighborsAndRestoreResponsiveSizing()
	{
		var app = Application.Current;
		var shutdown = app.ShutdownMode;
		app.ShutdownMode = ShutdownMode.OnExplicitShutdown;
		var grid = new Grid();
		foreach (var width in new[] { new GridLength(220), new GridLength(4), new GridLength(1, GridUnitType.Star), new GridLength(4), new GridLength(1, GridUnitType.Star), new GridLength(4), new GridLength(500) })
			grid.ColumnDefinitions.Add(new ColumnDefinition { Width = width });
		grid.ColumnDefinitions[2].MinWidth = 180;
		grid.ColumnDefinitions[4].MinWidth = 180;
		var splitters = new[] { 1, 3, 5 }.Select(index =>
		{
			var splitter = new PaneGridSplitter { Width = 4, HorizontalAlignment = HorizontalAlignment.Stretch, VerticalAlignment = VerticalAlignment.Stretch };
			Grid.SetColumn(splitter, index);
			grid.Children.Add(splitter);
			return splitter;
		}).ToArray();
		var window = new Window { Content = grid, Width = 1600, Height = 500, Left = -15000, Top = -15000, ShowInTaskbar = false, ShowActivated = false };
		try
		{
			window.Show();
			Settle();
			foreach (var splitter in splitters)
			{
				var before = Sizes();
				BeginDrag(splitter);
				Delta(splitter, 40);
				AssertNeighbors(before, Grid.GetColumn(splitter), 40);
				EndDrag(splitter, false);
				AssertNeighbors(before, Grid.GetColumn(splitter), 40);
				AssertUnits();

				before = Sizes();
				var key = new KeyEventArgs(Keyboard.PrimaryDevice, PresentationSource.FromVisual(window), 0, Key.Left) { RoutedEvent = Keyboard.KeyDownEvent };
				splitter.RaiseEvent(key);
				Settle();
				AssertNeighbors(before, Grid.GetColumn(splitter), -splitter.KeyboardIncrement);
				AssertUnits();

				before = Sizes();
				BeginDrag(splitter);
				Delta(splitter, -30);
				EndDrag(splitter, true);
				for (var i = 0; i < before.Length; i++) Near(before[i], Sizes()[i]);
				AssertUnits();
			}

			var widths = Sizes();
			BeginDrag(splitters[2]);
			Delta(splitters[2], 10000);
			Near(180, Sizes()[6]);
			Near(widths[0], Sizes()[0]);
			Near(widths[2], Sizes()[2]);
			EndDrag(splitters[2], true);
			BeginDrag(splitters[2]);
			Delta(splitters[2], -10000);
			Near(180, Sizes()[4]);
			Near(widths[2], Sizes()[2]);
			EndDrag(splitters[2], true);

			widths = Sizes();
			window.Width += 200;
			Settle();
			Near(widths[0], Sizes()[0]);
			Near(widths[6], Sizes()[6]);
			RegressionAssert.True(Sizes()[2] > widths[2] && Sizes()[4] > widths[4]);
			Near(widths[2] / widths[4], Sizes()[2] / Sizes()[4]);
			grid.ColumnDefinitions[2].Width = new GridLength(3, GridUnitType.Star);
			grid.ColumnDefinitions[4].Width = new GridLength(1, GridUnitType.Star);
			window.Width = 1200;
			Settle();
			RegressionAssert.True(Sizes()[2] >= 180 && Sizes()[4] >= 180);
			Near(grid.ActualWidth, Sizes().Sum());
			window.Width = 1600;
			Settle();

			// A collapsed pane keeps its rail width and cannot be expanded by dragging.
			grid.ColumnDefinitions[4].MinWidth = 52;
			grid.ColumnDefinitions[4].MaxWidth = 52;
			grid.ColumnDefinitions[4].Width = new GridLength(52);
			Settle();
			widths = Sizes();
			BeginDrag(splitters[2]);
			Delta(splitters[2], 100);
			EndDrag(splitters[2], false);
			for (var i = 0; i < widths.Length; i++) Near(widths[i], Sizes()[i]);
		}
		finally
		{
			window.Close();
			app.ShutdownMode = shutdown;
		}

		double[] Sizes() => grid.ColumnDefinitions.Select(column => column.ActualWidth).ToArray();
		void Settle() { window.UpdateLayout(); window.Dispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle); }
		void AssertUnits()
		{
			RegressionAssert.True(grid.ColumnDefinitions[0].Width.IsAbsolute && grid.ColumnDefinitions[6].Width.IsAbsolute);
			RegressionAssert.True(grid.ColumnDefinitions[2].Width.IsStar && grid.ColumnDefinitions[4].Width.IsStar);
		}
		void BeginDrag(PaneGridSplitter splitter)
		{
			splitter.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice, 0, MouseButton.Left) { RoutedEvent = UIElement.PreviewMouseLeftButtonDownEvent });
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
			for (var i = 0; i < before.Length; i++)
				Near(before[i] + (i == divider - 1 ? delta : i == divider + 1 ? -delta : 0), Sizes()[i]);
		}
		static void Near(double expected, double actual)
		{
			if (Math.Abs(expected - actual) > 0.1) throw new InvalidOperationException($"Expected pane size {expected}, got {actual}.");
		}
	}
}
