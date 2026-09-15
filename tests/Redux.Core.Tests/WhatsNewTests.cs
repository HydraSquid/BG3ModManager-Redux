using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using DivinityModManager.Models;
using DivinityModManager.Views;
using Newtonsoft.Json;

namespace Redux.Core.Tests;

internal sealed class WhatsNewTests
{
	public void SuppressionIsSavedAndOlderSettingsKeepNotesEnabled()
	{
		var settings = JsonConvert.DeserializeObject<DivinityModManagerSettings>("{}")!;
		RegressionAssert.True(settings.ShowWhatsNewAfterUpdates);
		string saved = String.Empty;
		var window = new ReduxWhatsNewWindow(null!, "test", settings, () => saved = JsonConvert.SerializeObject(settings));
		var footer = ((Grid)window.Content).Children.OfType<StackPanel>().Single();
		footer.Children.OfType<CheckBox>().Single().IsChecked = true;
		RegressionAssert.False(JsonConvert.DeserializeObject<DivinityModManagerSettings>(saved)!.ShowWhatsNewAfterUpdates);
		footer.Children.OfType<CheckBox>().Single().IsChecked = false;
		RegressionAssert.True(JsonConvert.DeserializeObject<DivinityModManagerSettings>(saved)!.ShowWhatsNewAfterUpdates);
	}

	public void ReleaseMetadataIsHiddenAndCustomBackgroundsHaveReadableText()
	{
		var window = new ReduxWhatsNewWindow(null!, "test");
		RegressionAssert.Equal("What's New", window.Title);
		RegressionAssert.Equal("A maintenance update. Silent hotfixes remain a separate term.",
			ReduxWhatsNewWindow.CleanReleaseNotes("A silent maintenance update. Silent hotfixes remain a separate term."));
		foreach (var color in new[] { Colors.White, Colors.Black, Color.FromRgb(115, 95, 140) })
		{
			var surface = new LinearGradientBrush(color, color, 90);
			window.Resources["ReduxAppBackgroundBrush"] = surface;
			window.Resources["ReduxTextPrimaryBrush"] = new SolidColorBrush(WhatsNewForeground(color));
			window.SetNotes("<!-- redux:no-announce -->\n# Fixed\n\nReadable **body** and `code`.\n<!-- internal\nmetadata -->");
			var document = ((Grid)window.Content).Children.OfType<FlowDocumentScrollViewer>().Single().Document;
			RegressionAssert.True(ReferenceEquals(surface, document.Background));
			var text = new TextRange(document.ContentStart, document.ContentEnd).Text;
			RegressionAssert.False(text.Contains("no-announce") || text.Contains("metadata"));
			RegressionAssert.Contains(text, "Readable");
			RegressionAssert.Equal(WhatsNewForeground(color), ((SolidColorBrush)document.Foreground).Color);
			RegressionAssert.True(document.Blocks.OfType<Paragraph>().All(p => ((SolidColorBrush)p.Foreground).Color == WhatsNewForeground(color)));
		}
	}

	private static Color WhatsNewForeground(Color background) => ReduxWhatsNewWindow.ReadingForeground(background);
}
