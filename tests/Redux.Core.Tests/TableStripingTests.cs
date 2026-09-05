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
					ErrorCode = "archive-unreadable", ErrorDetails = "The archive could not be read. Try Download Again." }
			};
			var downloads = new NxmDownloadsPane { DataContext = new { NxmDownloads = downloadItems } };
			host.Child = downloads;
			ReduxThemeService.Apply(app.Resources, ReduxThemeType.ReduxDark);
			Layout(host, 1100);
			var downloadList = (ListView)downloads.FindName("DownloadsList");
			foreach (var item in downloadItems)
			{
				var row = (ListViewItem)downloadList.ItemContainerGenerator.ContainerFromItem(item);
				var buttons = VisualChildren(row).OfType<Button>().ToArray();
				RegressionAssert.True(buttons.Any(button => AutomationProperties.GetName(button) == "Download failure details" && button.IsVisible));
				RegressionAssert.True(buttons.Any(button => AutomationProperties.GetName(button) == "Download fresh copy" && button.IsVisible));
				RegressionAssert.Equal(item.CanRetryInstall, buttons.Any(button => AutomationProperties.GetName(button) == "Retry install" && button.IsVisible));
			}
			Capture(host, "download-errors-wide");
			Layout(host, 640);
			Capture(host, "download-errors-compact");
		}
		catch (Exception ex) { throw new InvalidOperationException(ex.ToString(), ex); }
		finally
		{
			window?.Close();
			app.Resources = previous;
			app.ShutdownMode = shutdownMode;
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
		if (Window.GetWindow(element) is { } window) window.Width = width;
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
