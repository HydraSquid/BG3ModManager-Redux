using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

using DivinityModManager.Controls;
using DivinityModManager.Models;
using DivinityModManager.Util;

namespace Redux.Core.Tests;

internal sealed class TableStripingTests
{
	public void TableRowsAlternateAcrossBuiltInAndLiveCustomThemes() => VerifyStriping(false);

	public void ModRowsAlternateAfterFilteringReorderingAndRecycling() => VerifyStriping(true);

	private static void VerifyStriping(bool modRows)
	{
		var app = Application.Current;
		var resources = app.Resources;
		var shutdownMode = app.ShutdownMode;
		var reduceMotion = ReduxWindowBehavior.ReduceMotion;
		var effects = ReduxWindowBehavior.BackgroundEffectsDisabled;
		Window? window = null;
		app.ShutdownMode = ShutdownMode.OnExplicitShutdown;
		try
		{
			app.Resources = new ResourceDictionary();
			foreach (var uri in new[] { "/Redux;component/Themes/Typography.xaml", "/Redux;component/Themes/Light.xaml",
				"/Redux;component/Themes/Dark.xaml", "/AdonisUI.ClassicTheme;component/Resources.xaml" })
				app.Resources.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri(uri, UriKind.Relative) });
			// The standalone host needs the base used by the mod row's context menu.
			app.Resources[typeof(Image)] = new Style(typeof(Image));
			ListView table = modRows ? new ModListView() : new ListView();
			if (modRows) table.SetResourceReference(FrameworkElement.StyleProperty, "ModOrderListView");
			table.SetResourceReference(Control.ForegroundProperty, "ReduxTextPrimaryBrush");
			table.SetResourceReference(Control.BackgroundProperty, "ReduxListInteriorBrush");
			var columns = new GridView();
			columns.Columns.Add(new GridViewColumn { Header = "Mod name", Width = 330, DisplayMemberBinding = new Binding(nameof(DivinityModData.Name)) });
			columns.Columns.Add(new GridViewColumn { Header = "Author", Width = 220, DisplayMemberBinding = new Binding(nameof(DivinityModData.Author)) });
			table.View = columns;
			var names = new[] { "Community Library", "Interface Improvements", "Expanded Spellbook", "Inventory Helpers", "Character Customisation", "Companion Adjustments" };
			foreach (var name in names)
				table.Items.Add(new DivinityModData { UUID = Guid.NewGuid().ToString(), Name = name, Author = "Example author" });
			window = new Window
			{
				Content = table, Width = 740, Height = 280, Left = -15000, Top = -15000,
				ShowActivated = false, ShowInTaskbar = false, WindowStyle = WindowStyle.None,
				Resources = new ResourceDictionary { Source = new Uri("/Redux;component/Themes/MainResourceDictionary.xaml", UriKind.Relative) }
			};
			window.Show();
			foreach (var theme in new[] { ReduxThemeType.ReduxDark, ReduxThemeType.ReduxLight, ReduxThemeType.Parchment })
			{
				ReduxThemeService.Apply(app.Resources, theme);
				foreach (var motion in new[] { false, true })
				{
					ReduxWindowBehavior.ConfigureAccessibility(motion, effects);
					Layout(window);
					AssertAlternating(table);
				}
				Capture(table, $"{(modRows ? "mod" : "table")}-stripes-{theme}");
				var row = (ListViewItem)table.ItemContainerGenerator.ContainerFromIndex(1);
				row.IsSelected = true;
				Layout(window);
				RegressionAssert.False(ReferenceEquals(row.Background, app.FindResource("ReduxTableAlternateRowBrush")));
				Capture(table, $"{(modRows ? "mod" : "table")}-stripes-{theme}-selected");
				row.IsSelected = false;
				table.IsEnabled = false;
				Layout(window);
				AssertAlternating(table);
				Capture(table, $"{(modRows ? "mod" : "table")}-stripes-{theme}-disabled");
				table.IsEnabled = true;
				window.Width = 560;
				table.FontSize = 18;
				Layout(window);
				AssertAlternating(table);
				Capture(table, $"{(modRows ? "mod" : "table")}-stripes-{theme}-compact-large-text");
				window.Width = 740;
				table.ClearValue(Control.FontSizeProperty);
			}

			var custom = ReduxThemeService.CreateFromBase("Stripe regression", ReduxThemeType.ReduxDark);
			custom.TextColor = "#D5E8F1";
			ReduxThemeService.Apply(app.Resources, custom.BaseTheme, custom);
			Layout(window);
			AssertAlternating(table);
			RegressionAssert.Equal(Color.FromRgb(0xD5, 0xE8, 0xF1), ((SolidColorBrush)((ListViewItem)table.ItemContainerGenerator.ContainerFromIndex(1)).Background).Color);
			custom.TextColor = "#EEDDCB";
			ReduxThemeService.PreviewColors(app.Resources, custom);
			Layout(window);
			RegressionAssert.Equal(Color.FromRgb(0xEE, 0xDD, 0xCB), ((SolidColorBrush)((ListViewItem)table.ItemContainerGenerator.ContainerFromIndex(1)).Background).Color);

			var first = table.Items[0];
			table.Items.RemoveAt(0);
			table.Items.Add(first);
			Layout(window);
			AssertAlternating(table);
			table.Items.Filter = item => ((DivinityModData)item).Name != names[2];
			Layout(window);
			AssertAlternating(table);
			table.Items.Filter = null;
			foreach (var index in Enumerable.Range(0, 60))
				table.Items.Add(new DivinityModData { UUID = Guid.NewGuid().ToString(), Name = $"Extra module {index}" });
			table.ScrollIntoView(table.Items[45]);
			Layout(window);
			AssertAlternating(table);
			table.ScrollIntoView(table.Items[0]);
			Layout(window);
			AssertAlternating(table);
		}
		finally
		{
			window?.Close();
			ReduxWindowBehavior.ConfigureAccessibility(reduceMotion, effects);
			app.Resources = resources;
			app.ShutdownMode = shutdownMode;
		}
	}

	private static void AssertAlternating(ListView table)
	{
		RegressionAssert.Equal(2, table.AlternationCount);
		var realized = 0;
		var previousIndex = -2;
		var previousAlternate = -1;
		for (var index = 0; index < table.Items.Count; index++)
		{
			if (table.ItemContainerGenerator.ContainerFromIndex(index) is not ListViewItem row) continue;
			realized++;
			// WPF may restart the cycle at the first realized item after a virtualized jump.
			// Neighboring realized rows must still alternate, and recycled visuals must match.
			var alternate = ItemsControl.GetAlternationIndex(row);
			RegressionAssert.True(alternate is 0 or 1);
			if (index == previousIndex + 1) RegressionAssert.Equal(1 - previousAlternate, alternate);
			previousIndex = index;
			previousAlternate = alternate;
			RegressionAssert.Equal(alternate == 1, ReferenceEquals(row.Background, Application.Current.FindResource("ReduxTableAlternateRowBrush")));
			if (table is ModListView)
			{
				var border = (Border)row.Template.FindName("RowBorder", row);
				RegressionAssert.Equal(alternate == 1, ReferenceEquals(border.Background, Application.Current.FindResource("ReduxTableAlternateRowBrush")));
			}
		}
		RegressionAssert.True(realized > 1);
	}

	private static void Layout(Window window)
	{
		window.Dispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
		window.UpdateLayout();
	}

	private static void Capture(FrameworkElement element, string name)
	{
		var output = Environment.GetEnvironmentVariable("REDUX_LAYOUT_PREVIEW");
		if (String.IsNullOrWhiteSpace(output)) return;
		Directory.CreateDirectory(output);
		var dpi = VisualTreeHelper.GetDpi(element);
		var bitmap = new RenderTargetBitmap((int)Math.Ceiling(element.ActualWidth * dpi.DpiScaleX),
			(int)Math.Ceiling(element.ActualHeight * dpi.DpiScaleY), 96 * dpi.DpiScaleX, 96 * dpi.DpiScaleY, PixelFormats.Pbgra32);
		bitmap.Render(element);
		var encoder = new PngBitmapEncoder();
		encoder.Frames.Add(BitmapFrame.Create(bitmap));
		using var file = File.Create(Path.Combine(output, $"{name}.png"));
		encoder.Save(file);
	}
}
