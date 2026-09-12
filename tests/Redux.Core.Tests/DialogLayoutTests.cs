using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using DivinityModManager.Models;
using DivinityModManager.Util;
using DivinityModManager.Views;

namespace Redux.Core.Tests;

public sealed class DialogLayoutTests
{
	public void UpdateAndMessageActionsRemainReachableWithLongText()
	{
		foreach (var theme in new[] { ReduxThemeType.ReduxDark, ReduxThemeType.ReduxLight, ReduxThemeType.Parchment })
		{
			var update = new AppUpdateWindow();
			((TextBlock)update.FindName("UpdateDescription")).Text = "Redux could not safely prepare this update. The current installation was not changed.";
			((FlowDocumentScrollViewer)update.FindName("UpdateChangelogView")).Document = new FlowDocument(
				new Paragraph(new Run(String.Join(" ", Enumerable.Repeat("Keep using Redux and try again later.", 20))))) { FontSize = 18 };
			VerifyLayout(update, theme, 560, 440, "update", "SkipButton", "ConfirmButton");
			var message = new ReduxMessageBoxWindow(null!, String.Join(" ", Enumerable.Repeat("Review these changes before continuing.", 30)),
				"Review changes", MessageBoxButton.YesNoCancel, MessageBoxImage.Warning, MessageBoxResult.Cancel);
			message.SetButtonLabel(MessageBoxResult.Yes, "Apply reviewed changes");
			message.SetButtonLabel(MessageBoxResult.No, "Keep existing files");
			VerifyLayout(message, theme, 420, 440, "message", "YesButton", "NoButton", "CancelButton");
		}
	}

	private static void VerifyLayout(Window window, ReduxThemeType theme, int width, int height, string name, params string[] buttons)
	{
		ReduxThemeService.Apply(window.Resources, theme);
		window.Resources["Redux.FontSize.12"] = 18d;
		var root = (Grid)window.Content;
		root.Opacity = 1;
		root.RenderTransform = Transform.Identity;
		root.Background = window.Background;
		root.Measure(new Size(width, height));
		root.Arrange(new Rect(0, 0, width, height));
		root.UpdateLayout();
		foreach (var key in buttons)
		{
			var button = (Button)window.FindName(key);
			var bounds = button.TransformToAncestor(root).TransformBounds(new Rect(button.RenderSize));
			RegressionAssert.True(bounds.Width > 0 && bounds.Height > 0);
			RegressionAssert.True(new Rect(0, 0, width, height).Contains(bounds));
		}
		var output = Environment.GetEnvironmentVariable("REDUX_LAYOUT_PREVIEW");
		if (!String.IsNullOrWhiteSpace(output))
		{
			Directory.CreateDirectory(output);
			var bitmap = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
			bitmap.Render(root);
			var encoder = new PngBitmapEncoder();
			encoder.Frames.Add(BitmapFrame.Create(bitmap));
			using var file = File.Create(Path.Combine(output, $"{name}-{theme}.png"));
			encoder.Save(file);
		}
	}
}
