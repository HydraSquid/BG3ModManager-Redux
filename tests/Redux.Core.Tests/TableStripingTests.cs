using System;
using System.ComponentModel;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Automation;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

using DivinityModManager.Controls;
using DivinityModManager.AppServices;
using DivinityModManager.Models;
using DivinityModManager.Models.NexusMods;
using DivinityModManager.Util;
using DivinityModManager.Views;

namespace Redux.Core.Tests;

internal sealed class TableStripingTests
{
	public void TablesAlternateAcrossThemesAndLivePaletteChanges()
	{
		var app = Application.Current;
		var previous = app.Resources;
		var shutdownMode = app.ShutdownMode;
		Window? window = null;
		app.ShutdownMode = ShutdownMode.OnExplicitShutdown;
		try
		{
			app.Resources = new ResourceDictionary();
			// Standalone row templates also resolve the context menu's image base style.
			app.Resources[typeof(Image)] = new Style(typeof(Image));
			foreach (var uri in new[]
			{
				"/BG3ModManager;component/Themes/Typography.xaml",
				"/BG3ModManager;component/Themes/Light.xaml",
				"/BG3ModManager;component/Themes/Dark.xaml",
				"/AdonisUI.ClassicTheme;component/Resources.xaml",
				"/BG3ModManager;component/Themes/MainResourceDictionary.xaml"
			})
			{
				app.Resources.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri(uri, UriKind.Relative) });
			}

			var panel = new StackPanel { Margin = new Thickness(16) };
			var host = new Border { Child = panel };
			window = new Window
			{
				Content = host, ShowInTaskbar = false, ShowActivated = false,
				WindowStyle = WindowStyle.None, Left = -15000, Top = -15000, Width = 1100, Height = 680
			};
			host.SetResourceReference(Border.BackgroundProperty, "ReduxListInteriorBrush");
			var normal = CreateTable(null);
			var mods = CreateTable("ModOrderListView");
			var updates = CreateTable("VirtualizedModListView");
			updates.ItemContainerStyle = (Style)app.FindResource("DivinityModUpdateNewListItem");
			foreach (var (title, table) in new[] { ("File tables", normal), ("Mod tables", mods), ("Update tables", updates) })
			{
				var label = new TextBlock { Text = title, Margin = new Thickness(0, 8, 0, 4), FontWeight = FontWeights.SemiBold };
				label.SetResourceReference(TextBlock.ForegroundProperty, "ReduxTextPrimaryBrush");
				panel.Children.Add(label);
				panel.Children.Add(table);
			}

			window.Show();
			foreach (var theme in new[] { ReduxThemeType.ReduxDark, ReduxThemeType.ReduxLight, ReduxThemeType.Parchment })
			{
				ReduxThemeService.Apply(app.Resources, theme);
				Layout(host, 1100);
				foreach (var table in new[] { normal, mods, updates }) AssertAlternating(table);
				Capture(host, theme + "-wide");
				Layout(host, 640);
				Capture(host, theme + "-compact");
			}

			var custom = ReduxThemeService.CreateFromBase("Striping test", ReduxThemeType.ReduxDark);
			custom.TextColor = "#D5E8F1";
			custom.BackgroundColor = "#0C1921";
			custom.SurfaceColor = "#162630";
			ReduxThemeService.Apply(app.Resources, custom.BaseTheme, custom);
			Layout(host, 1100);
			RegressionAssert.Equal(Color.FromRgb(0xD5, 0xE8, 0xF1), (Color)app.FindResource("ReduxTextPrimaryColor"));
			AssertAlternating(mods);
			var alternate = (SolidColorBrush)((ListViewItem)mods.ItemContainerGenerator.ContainerFromIndex(1)).Background;
			RegressionAssert.Equal(Color.FromRgb(0xD5, 0xE8, 0xF1), alternate.Color);
			Capture(host, "custom-wide");
			Layout(host, 640);
			Capture(host, "custom-compact");
			custom.TextColor = "#EEDDCB";
			ReduxThemeService.PreviewColors(app.Resources, custom);
			Layout(host, 640);
			RegressionAssert.Equal(Color.FromRgb(0xEE, 0xDD, 0xCB), ((SolidColorBrush)((ListViewItem)mods.ItemContainerGenerator.ContainerFromIndex(1)).Background).Color);

			normal.Items.SortDescriptions.Add(new SortDescription(nameof(DivinityModData.Name), ListSortDirection.Descending));
			normal.Items.Filter = item => ((DivinityModData)item).Name != "Module 1";
			Layout(host, 640);
			AssertAlternating(normal);

			foreach (var table in new[] { normal, mods, updates })
			{
				var row = (ListViewItem)table.ItemContainerGenerator.ContainerFromIndex(1);
				row.IsSelected = true;
				RegressionAssert.False(ReferenceEquals(row.Background, app.FindResource("ReduxTableAlternateRowBrush")));
				row.IsSelected = false;
				AssertAlternating(table);
			}

			var downloadItems = new[]
			{
				new NxmDownloadItem { ProjectName = "Goon's Paladin Overhaul", FileDisplayName = "Paladin Overhaul", State = NxmDownloadState.InstallFailed,
					ErrorCode = "missing-dependencies", ErrorDetails = "Missing dependency: Goon's Library. Install it, then retry installation." },
				new NxmDownloadItem { ProjectName = "Unreadable archive example", FileDisplayName = "Download.zip", State = NxmDownloadState.InstallFailed,
					ErrorCode = "archive-unreadable", ErrorDetails = "The archive could not be read. Try Download Again." },
				new NxmDownloadItem { ProjectName = "Ready archive", FileDisplayName = "Ready.zip", State = NxmDownloadState.Downloaded },
				new NxmDownloadItem { ProjectName = "Active transfer", FileDisplayName = "Downloading.zip", State = NxmDownloadState.Downloading },
				new NxmDownloadItem { ProjectName = "Paused transfer", FileDisplayName = "Paused.zip", State = NxmDownloadState.Paused },
				new NxmDownloadItem { ProjectName = "Installed package", FileDisplayName = "Installed.zip", State = NxmDownloadState.Installed },
				new NxmDownloadItem { ModId = 781, ProjectName = "WASD Character Movement", FileDisplayName = "WASD.zip", State = NxmDownloadState.Downloaded,
					NativeRequirementLabel = "Loader missing / blocked", NativeRequirementStatus = "Requires Native Mod Loader: not installed", NativeRequirementWarning = true },
				new NxmDownloadItem { ModId = 945, ProjectName = "Native Camera Tweaks", FileDisplayName = "Camera.zip", State = NxmDownloadState.Downloaded,
					NativeRequirementLabel = "Loader unverified", NativeRequirementStatus = "Requires Native Mod Loader: external installation, unverified", NativeRequirementWarning = true },
				new NxmDownloadItem { ModId = 944, ProjectName = "Native Mod Loader", FileDisplayName = "Loader.zip", State = NxmDownloadState.Installed,
					NativeRequirementLabel = "Loader verified", NativeRequirementStatus = "Native Mod Loader: verified Redux installation", NativeRequirementWarning = false }
			};
			var downloads = new NxmDownloadsPane { DataContext = new { NxmDownloads = downloadItems } };
			host.Child = downloads;
			ReduxThemeService.Apply(app.Resources, ReduxThemeType.ReduxDark);
			Layout(host, 1500);
			var downloadList = (ListView)downloads.FindName("DownloadsList");
			foreach (var item in downloadItems)
			{
				var row = (ListViewItem)downloadList.ItemContainerGenerator.ContainerFromItem(item);
				var buttons = VisualChildren(row).OfType<Button>().ToArray();
				RegressionAssert.True(buttons.Any(button => AutomationProperties.GetName(button) == "Download failure details" && button.IsVisible));
				RegressionAssert.Equal(item.CanDownloadAgain, buttons.Any(button => AutomationProperties.GetName(button) == "Download fresh copy" && button.IsVisible));
				RegressionAssert.Equal(item.CanRetryInstall, buttons.Any(button => AutomationProperties.GetName(button) == "Retry install" && button.IsVisible));
			}
			var downloadColumns = ((GridView)downloadList.View).Columns;
			var actionsColumn = downloadColumns.Last();
			var downloadScroll = VisualChildren(downloadList).OfType<ScrollViewer>().First();
			AssertDownloadActions(downloadList, true);
			if (downloadScroll.ScrollableWidth >= 1) throw new InvalidOperationException($"Wide downloads must not need horizontal scrolling: {downloadScroll.ScrollableWidth}.");
			var actionHeader = VisualChildren(downloadList).OfType<GridViewColumnHeader>().Single(header => header.Column == actionsColumn);
			RegressionAssert.False(VisualChildren(actionHeader).OfType<System.Windows.Controls.Primitives.Thumb>().Any());
			var initialWidth = actionsColumn.ActualWidth;
			Layout(host, 1700);
			if (Math.Abs(actionsColumn.ActualWidth - initialWidth - 200) >= 2) throw new InvalidOperationException("Actions must grow with the viewport.");
			downloadColumns[1].Width += 80;
			Layout(host, 1700);
			if (Math.Abs(actionsColumn.ActualWidth - initialWidth - 120) >= 2) throw new InvalidOperationException("Actions must yield to resized data columns.");
			downloadColumns[1].Width -= 80;
			downloadColumns.Move(1, downloadColumns.Count - 1);
			Layout(host, 1500);
			RegressionAssert.True(ReferenceEquals(actionsColumn, downloadColumns.Last()));
			downloadColumns.Move(downloadColumns.Count - 2, 1);
			Layout(host, 1500);
			Capture(host, "download-errors-wide");
			Layout(host, 640);
			RegressionAssert.True(downloadScroll.ScrollableWidth > 0);
			AssertDownloadActions(downloadList, false);
			Capture(host, "download-errors-compact");
			downloadScroll.ScrollToRightEnd();
			Layout(host, 640);
			Capture(host, "download-actions-compact");
			AssertDownloadActions(downloadList, true);
			var previousActionFontSize = app.FindResource("Redux.FontSize.11");
			app.Resources["Redux.FontSize.11"] = 16.0;
			downloadItems[2].State = NxmDownloadState.InstallFailed;
			downloadItems[2].ErrorCode = "missing-dependencies";
			Layout(host, 1700);
			downloadScroll.ScrollToLeftEnd();
			Layout(host, 1700);
			AssertDownloadActions(downloadList, true);
			Capture(host, "download-actions-large-text");
			app.Resources["Redux.FontSize.11"] = previousActionFontSize;

			var manyDownloads = Enumerable.Range(0, 200).Select(index => new NxmDownloadItem
			{
				ProjectName = $"Download {index}", FileDisplayName = $"Archive {index}", State = NxmDownloadState.Downloaded
			}).ToArray();
			downloads.DataContext = new { NxmDownloads = manyDownloads };
			Layout(host, 1500);
			var selectAll = (CheckBox)downloads.FindName("SelectAllDownloadsCheckBox");
			selectAll.IsChecked = true;
			selectAll.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
			RegressionAssert.Equal(200, downloadList.SelectedItems.Count);
			RegressionAssert.Equal(200, manyDownloads.Count(item => item.IsSelected));
			downloadList.SelectedItem = manyDownloads[150];
			RegressionAssert.Equal(1, downloadList.SelectedItems.Count);
			RegressionAssert.Equal(1, manyDownloads.Count(item => item.IsSelected));
			downloadList.ScrollIntoView(manyDownloads[0]);
			Layout(host, 640);
			RegressionAssert.Equal(1, manyDownloads.Count(item => item.IsSelected));
			Capture(host, "download-selection-compact");
			Layout(host, 1500);
			Capture(host, "download-selection-wide");
			selectAll.IsChecked = false;
			selectAll.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
			RegressionAssert.Equal(200, manyDownloads.Count(item => item.IsSelected));
			selectAll.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
			RegressionAssert.Equal(0, manyDownloads.Count(item => item.IsSelected));

			var selectionMods = Enumerable.Range(0, 200).Select(index => new RegressionModData { Name = $"Selection mod {index}", UUID = Guid.NewGuid().ToString() }).ToArray();
			var selectionTable = new ModListView
			{
				ItemsSource = selectionMods, DisplayMemberPath = nameof(DivinityModData.Name),
				Style = (Style)app.FindResource("ModOrderListView")
			};
			host.Child = selectionTable;
			Layout(host, 1100);
			selectionTable.SelectAll();
			RegressionAssert.Equal(200, selectionMods.Count(mod => mod.IsSelected));
			selectionTable.SelectedItem = selectionMods[150];
			RegressionAssert.Equal(1, selectionMods.Count(mod => mod.IsSelected));
			selectionTable.ScrollIntoView(selectionMods[0]);
			Layout(host, 1100);
			RegressionAssert.Equal(1, selectionTable.SelectedItems.Count);
			RegressionAssert.Equal(1, selectionMods.Count(mod => mod.IsSelected));
			selectionTable.SelectedItems.Add(selectionMods[1]);
			RegressionAssert.Equal(2, selectionMods.Count(mod => mod.IsSelected));
			selectionTable.UnselectAll();
			RegressionAssert.Equal(0, selectionMods.Count(mod => mod.IsSelected));

			var panes = new HorizontalModLayout();
			((ColumnDefinition)panes.FindName("DownloadsColumn")).Width = new GridLength(780);
			((ColumnDefinition)panes.FindName("DownloadsSplitterColumn")).Width = new GridLength(4);
			var paneDownloads = (NxmDownloadsPane)panes.FindName("DownloadsPane");
			paneDownloads.Visibility = Visibility.Visible;
			host.Child = panes;
			Layout(host, 1800);
			Layout(host, 1200);
			var downloadsRight = paneDownloads.TranslatePoint(new Point(), panes).X + paneDownloads.ActualWidth;
			if (downloadsRight > panes.ActualWidth + 0.5) throw new InvalidOperationException("Downloads overflows the actual view after window resizing.");
			RegressionAssert.True(((ColumnDefinition)panes.FindName("ActiveModsColumn")).ActualWidth >= 180);
			RegressionAssert.True(((ColumnDefinition)panes.FindName("InactiveModsColumn")).ActualWidth >= 180);

			var nativeWindow = new NativeModsWindow(window, null!, 781) { ShowActivated = false, Left = -15000, Top = -15000, WindowStartupLocation = WindowStartupLocation.Manual };
			try
			{
				nativeWindow.Show();
				Layout((FrameworkElement)nativeWindow.Content, 760);
				Capture((FrameworkElement)nativeWindow.Content, "native-mods-wide");
				Layout((FrameworkElement)nativeWindow.Content, 560);
				Capture((FrameworkElement)nativeWindow.Content, "native-mods-compact");
				RegressionAssert.True(((TextBlock)nativeWindow.FindName("GameVersionText")).Text.Contains("Hotfix 34"));
				RegressionAssert.True(((TextBlock)nativeWindow.FindName("TargetFilesText")).Text.Contains("BG3WASD.dll"));
			}
			finally { nativeWindow.Close(); }

			var requirements = new[]
			{
				new ModuleShortDesc { UUID = "069e5871-efe8-44bb-b02a-fe957df5ae0e", Name = "Reviewed dependency" },
				new ModuleShortDesc { UUID = "11111111-1111-4111-8111-111111111111", Name = "Unknown dependency" }
			};
			var assistance = ModDependencyAssistanceService.Build(requirements, [],
				[new NxmDownloadItem { ModId = 3902, FileDisplayName = "Optional compatibility patch", State = NxmDownloadState.Queued }], "", true);
			var dependencyWindow = new NxmDependencyReviewWindow(window,
				"The package declares missing requirements. No files were installed. Review each dependency below.", assistance)
			{ ShowActivated = false, WindowStartupLocation = WindowStartupLocation.Manual, Left = -15000, Top = -15000 };
			try
			{
				dependencyWindow.Show();
				var content = (FrameworkElement)dependencyWindow.Content;
				Layout(content, 800);
				var buttons = VisualChildren(content).OfType<Button>().Where(button => button.IsVisible).ToArray();
				RegressionAssert.Equal(1, buttons.Count(button => Equals(button.Content, "Open Nexus Files")));
				RegressionAssert.Equal(2, buttons.Count(button => Equals(button.Content, "Copy UUID")));
				RegressionAssert.False(buttons.Any(button => Equals(button.Content, "Install reviewed files")));
				Capture(content, "dependency-review-wide");
				Layout(content, 520);
				Capture(content, "dependency-review-compact");
			}
			finally { dependencyWindow.Close(); }
		}
		catch (Exception ex) { throw new InvalidOperationException(ex.ToString(), ex); }
		finally
		{
			window?.Close();
			app.Resources = previous;
			app.ShutdownMode = shutdownMode;
		}
	}

	private static void AssertDownloadActions(ListView list, bool fitsViewport)
	{
		var positions = new Dictionary<string, double>();
		foreach (var row in VisualChildren(list).OfType<ListViewItem>())
		{
			var buttons = VisualChildren(row).OfType<Button>().Where(button => button.IsVisible).ToArray();
			double previousRight = Double.NegativeInfinity;
			foreach (var button in buttons)
			{
				var name = AutomationProperties.GetName(button);
				var left = button.TranslatePoint(new Point(), list).X;
				var right = left + button.ActualWidth;
				if (left < previousRight - 0.5) throw new InvalidOperationException($"{name} overlaps a preceding action.");
				previousRight = right;
				if (!VisualChildren(button).OfType<ReduxIcon>().Any(icon => icon.IsVisible)) throw new InvalidOperationException($"{name} is missing its icon.");
				if (fitsViewport && right > list.ActualWidth + 0.5)
				{
					var scroll = VisualChildren(list).OfType<ScrollViewer>().First();
					throw new InvalidOperationException($"{name} is clipped at the viewport edge: right={right}, list={list.ActualWidth}, viewport={scroll.ViewportWidth}, extent={scroll.ExtentWidth}, offset={scroll.HorizontalOffset}, columns={String.Join(",", ((GridView)list.View).Columns.Select(column => column.ActualWidth))}.");
				}
				var slot = name is "Pause download" or "Resume download" or "Retry download" or "Retry install" or "Install download"
					? "Primary" : name is "Cancel download" or "Remove download" ? "Remove" : name;
				if (positions.TryGetValue(slot, out var expected))
				{
					if (Math.Abs(left - expected) >= 0.5) throw new InvalidOperationException($"{name} is misaligned across rows.");
				}
				else positions.Add(slot, left);
			}
			var presenter = VisualChildren(row).OfType<GridViewRowPresenter>().Single();
			var actionRight = presenter.TranslatePoint(new Point(), list).X + ((GridView)list.View).Columns.Sum(column => column.ActualWidth);
			if (previousRight > actionRight + 0.5) throw new InvalidOperationException("Actions overflow their column even when scrolled into view.");
		}
	}

	private static IEnumerable<DependencyObject> VisualChildren(DependencyObject parent)
	{
		for (var index = 0; index < VisualTreeHelper.GetChildrenCount(parent); index++)
		{
			var child = VisualTreeHelper.GetChild(parent, index);
			yield return child;
			foreach (var descendant in VisualChildren(child)) yield return descendant;
		}
	}

	private static ListView CreateTable(string? styleKey)
	{
		ListView table = styleKey == null ? new ListView() : new ModListView();
		if (styleKey != null) table.Style = (Style)Application.Current.FindResource(styleKey);
		table.Height = 176;
		table.SetResourceReference(Control.ForegroundProperty, "ReduxTextPrimaryBrush");
		table.SetResourceReference(Control.BackgroundProperty, "ReduxListInteriorBrush");
		var columns = new GridView();
		columns.Columns.Add(new GridViewColumn { Header = "Name", Width = 260, DisplayMemberBinding = new Binding(nameof(DivinityModData.Name)) });
		columns.Columns.Add(new GridViewColumn { Header = "Author", Width = 160, DisplayMemberBinding = new Binding(nameof(DivinityModData.Author)) });
		table.View = columns;
		for (var i = 0; i < 4; i++)
			table.Items.Add(new RegressionModData { UUID = Guid.NewGuid().ToString(), Name = $"Module {i}", Author = "Example author" });
		return table;
	}

	private static void AssertAlternating(ListView table)
	{
		RegressionAssert.Equal(2, table.AlternationCount);
		for (var i = 0; i < table.Items.Count; i++)
		{
			var row = (ListViewItem)table.ItemContainerGenerator.ContainerFromIndex(i);
			RegressionAssert.True(row != null);
			RegressionAssert.Equal(i % 2, ItemsControl.GetAlternationIndex(row!));
			RegressionAssert.Equal(i % 2 == 1, ReferenceEquals(row!.Background, Application.Current.FindResource("ReduxTableAlternateRowBrush")));
		}
	}

	private static void Layout(FrameworkElement element, double width)
	{
		if (Window.GetWindow(element) is { } window)
		{
			window.Width = width;
			window.UpdateLayout();
			element.Dispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
			return;
		}
		element.Dispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
		element.Measure(new Size(width, 680));
		element.Arrange(new Rect(0, 0, width, 680));
		element.UpdateLayout();
	}

	private static void Capture(FrameworkElement element, string name)
	{
		var directory = Environment.GetEnvironmentVariable("REDUX_TABLE_SCREENSHOTS");
		if (String.IsNullOrEmpty(directory)) return;
		if (!Directory.Exists(directory)) throw new DirectoryNotFoundException(directory);
		var bitmap = new RenderTargetBitmap((int)element.ActualWidth, (int)element.ActualHeight, 96, 96, PixelFormats.Pbgra32);
		bitmap.Render(element);
		var encoder = new PngBitmapEncoder();
		encoder.Frames.Add(BitmapFrame.Create(bitmap));
		using var stream = File.Create(Path.Combine(directory, name + ".png"));
		encoder.Save(stream);
	}
}
