using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;

using DivinityModManager.Models;
using DivinityModManager.Controls;
using DivinityModManager.Util;

namespace Redux.Core.Tests;

internal sealed class ForkTableStripingTests
{
	public void TableRowsAlternateAcrossBuiltInAndLiveCustomThemes() => VerifyTableStriping(false);

	public void ActualModRowsAlternateAcrossBuiltInAndLiveCustomThemes() => VerifyTableStriping(true);

	private void VerifyTableStriping(bool actualModRows)
	{
		var app = Application.Current;
		var previousResources = app.Resources;
		var previousShutdownMode = app.ShutdownMode;
		Window? window = null;
		app.ShutdownMode = ShutdownMode.OnExplicitShutdown;
		try
		{
			app.Resources = WpfRenderCapture.CreateReduxApplicationResources();

			var table = CreateTable(actualModRows);
			window = new Window
			{
				Content = table,
				ShowInTaskbar = false,
				ShowActivated = false,
				WindowStyle = WindowStyle.None,
				Left = -15000,
				Top = -15000,
				Width = 760,
				Height = 320,
				Resources = WpfRenderCapture.CreateReduxWindowResources()
			};
			window.SetResourceReference(Control.BackgroundProperty, "ReduxWindowBrush");
			window.Show();

			foreach (var theme in new[] { ReduxThemeType.ReduxDark, ReduxThemeType.ReduxLight, ReduxThemeType.Parchment })
			{
				ReduxThemeService.Apply(app.Resources, theme);
				Layout(window);
				AssertAlternating(table);
				WpfRenderCapture.CaptureIfRequested(window, $"fork-{(actualModRows ? "mod" : "table")}-stripes-{theme}");
			}

			var custom = ReduxThemeService.CreateFromBase("Stripe regression", ReduxThemeType.ReduxDark);
			custom.TextColor = "#D5E8F1";
			ReduxThemeService.Apply(app.Resources, custom.BaseTheme, custom);
			Layout(window);
			AssertAlternating(table);
			RegressionAssert.Equal(Color.FromRgb(0xD5, 0xE8, 0xF1), AlternateBrush(table).Color);
			WpfRenderCapture.CaptureIfRequested(window, $"fork-{(actualModRows ? "mod" : "table")}-stripes-custom");

			custom.TextColor = "#EEDDCB";
			ReduxThemeService.PreviewColors(app.Resources, custom);
			Layout(window);
			RegressionAssert.Equal(Color.FromRgb(0xEE, 0xDD, 0xCB), AlternateBrush(table).Color);

			var alternateRow = (ListViewItem)table.ItemContainerGenerator.ContainerFromIndex(1);
			alternateRow.IsSelected = true;
			RegressionAssert.False(ReferenceEquals(alternateRow.Background, app.FindResource("ReduxTableAlternateRowBrush")));
			alternateRow.IsSelected = false;
			AssertAlternating(table);
		}
		finally
		{
			window?.Close();
			app.Resources = previousResources;
			app.ShutdownMode = previousShutdownMode;
		}
	}

	private static ListView CreateTable(bool actualModRows)
	{
		ListView table = actualModRows ? new ModListView() : new ListView();
		table.Height = 240;
		if (actualModRows) table.SetResourceReference(FrameworkElement.StyleProperty, "ModOrderListView");
		table.SetResourceReference(Control.ForegroundProperty, "ReduxTextPrimaryBrush");
		table.SetResourceReference(Control.BackgroundProperty, "ReduxListInteriorBrush");
		var columns = new GridView();
		columns.Columns.Add(new GridViewColumn
		{
			Header = "Name",
			Width = 260,
			DisplayMemberBinding = new Binding(nameof(DivinityModData.Name))
		});
		columns.Columns.Add(new GridViewColumn
		{
			Header = "Author",
			Width = 160,
			DisplayMemberBinding = new Binding(nameof(DivinityModData.Author))
		});
		table.View = columns;
		foreach (var index in Enumerable.Range(0, 4))
			table.Items.Add(new DivinityModData { UUID = Guid.NewGuid().ToString(), Name = $"Module {index}", Author = "Example author" });
		return table;
	}

	private static void AssertAlternating(ListView table)
	{
		RegressionAssert.Equal(2, table.AlternationCount);
		for (var index = 0; index < table.Items.Count; index++)
		{
			var row = (ListViewItem)table.ItemContainerGenerator.ContainerFromIndex(index);
			RegressionAssert.Equal(index % 2, ItemsControl.GetAlternationIndex(row));
			RegressionAssert.Equal(index % 2 == 1,
				ReferenceEquals(row.Background, Application.Current.FindResource("ReduxTableAlternateRowBrush")));
			if (table is ModListView)
			{
				var rowBorder = (Border)row.Template.FindName("RowBorder", row);
				RegressionAssert.Equal(index % 2 == 1,
					ReferenceEquals(rowBorder.Background, Application.Current.FindResource("ReduxTableAlternateRowBrush")));
			}
		}
	}

	private static SolidColorBrush AlternateBrush(ListView table) =>
		(SolidColorBrush)((ListViewItem)table.ItemContainerGenerator.ContainerFromIndex(1)).Background;

	private static void Layout(Window window)
	{
		window.UpdateLayout();
		window.Dispatcher.Invoke(() => { });
	}
}
