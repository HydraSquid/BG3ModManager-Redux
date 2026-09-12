using System.Net.Http;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Shell;
using DivinityModManager.Controls;
using DivinityModManager.Util;
using DivinityModManager.Models;
using Newtonsoft.Json.Linq;

namespace DivinityModManager.Views;

public sealed class ReduxWhatsNewWindow : Window
{
	private readonly FlowDocumentScrollViewer _notes = new() { IsToolBarVisible = false, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
	private readonly CancellationTokenSource _closed = new();
	public ReduxWhatsNewWindow(Window owner, string version)
	{
		Owner = owner;
		Title = "What's New in Redux";
		Width = 720; Height = 560; MinWidth = 420; MinHeight = 320;
		ShowInTaskbar = false;
		ShowActivated = owner?.WindowState != WindowState.Minimized;
		WindowStartupLocation = WindowStartupLocation.CenterOwner;
		WindowChrome.SetWindowChrome(this, new WindowChrome { CaptionHeight = 0, ResizeBorderThickness = new Thickness(6), GlassFrameThickness = new Thickness(0), UseAeroCaptionButtons = false });
		Resources.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri("/Redux;component/Themes/MainResourceDictionary.xaml", UriKind.Relative) });
		Resources.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri("/Redux;component/Themes/DefaultStyles/MarkdownStyle.xaml", UriKind.Relative) });
		var settings = MainWindow.Self?.ViewModel?.Settings;
		ReduxThemeService.Apply(Resources, settings?.ColorTheme ?? ReduxThemeType.ReduxDark, settings == null ? null : ReduxThemeService.GetActiveTheme(settings));
		SetResourceReference(TemplateProperty, "ReduxWindowTemplate");
		SetResourceReference(BackgroundProperty, "ReduxSurfaceBrush");
		SetResourceReference(ForegroundProperty, "ReduxTextPrimaryBrush");
		SetResourceReference(FontFamilyProperty, "Redux.FontFamily.UI");
		var root = new Grid();
		root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
		root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
		root.RowDefinitions.Add(new RowDefinition());
		root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
		root.Children.Add(new ReduxWindowTitleBar());
		var heading = new TextBlock { Text = $"What's new in Redux {version}", FontSize = 22, Margin = new Thickness(24, 20, 24, 12), TextWrapping = TextWrapping.Wrap };
		Grid.SetRow(heading, 1); root.Children.Add(heading);
		_notes.Margin = new Thickness(24, 0, 24, 12);
		Grid.SetRow(_notes, 2); root.Children.Add(_notes);
		var tag = "v" + version.TrimStart('v');
		var url = "https://github.com/circleainn/BG3ModManager-Redux/releases/tag/" + Uri.EscapeDataString(tag);
		var actions = new WrapPanel { HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(24, 12, 24, 20) };
		var github = new Button { Content = "View on GitHub", Margin = new Thickness(0, 0, 8, 0) };
		github.SetResourceReference(StyleProperty, "ReduxSecondaryActionButtonStyle");
		github.Click += (_, _) => ProcessHelper.TryOpenUrl(url);
		var close = new Button { Content = "Continue" };
		close.SetResourceReference(StyleProperty, "ReduxPrimaryActionButtonStyle");
		close.Click += (_, _) => Close();
		actions.Children.Add(github); actions.Children.Add(close);
		Grid.SetRow(actions, 3); root.Children.Add(actions); Content = root;
		ReduxWindowBehavior.AttachAdaptiveSizing(this);
		Closed += (_, _) => _closed.Cancel();
		Loaded += async (_, _) =>
		{
			SetNotes("Loading release notes from GitHub…");
			try
			{
				using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(12), MaxResponseContentBufferSize = 1024 * 1024 };
				client.DefaultRequestHeaders.UserAgent.ParseAdd("BG3ModManagerRedux");
				var json = await client.GetStringAsync("https://api.github.com/repos/circleainn/BG3ModManager-Redux/releases/tags/" + Uri.EscapeDataString(tag), _closed.Token);
				var body = JObject.Parse(json).Value<string>("body");
				if (!_closed.IsCancellationRequested) SetNotes(String.IsNullOrWhiteSpace(body) ? "Release notes are available on GitHub." : body);
			}
			catch (Exception ex) when (ex is HttpRequestException or OperationCanceledException or Newtonsoft.Json.JsonException)
			{
				if (!_closed.IsCancellationRequested) SetNotes("Release notes couldn't be loaded. You can keep using Redux or view this release on GitHub.");
			}
		};
	}
	private void SetNotes(string text) => _notes.Document = ((Markdown)FindResource("DefaultMarkdown")).Transform(text);
}
