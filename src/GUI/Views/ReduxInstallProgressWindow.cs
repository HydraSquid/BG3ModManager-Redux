using System.Windows;
using System.Windows.Controls;
using System.Windows.Automation;
using System.Windows.Threading;
using System.Windows.Shell;
using DivinityModManager.Util;
using DivinityModManager.Controls;
using DivinityModManager.Models;

namespace DivinityModManager.Views;

/// <summary>Keeps the workspace modal throughout batch inspection and installation.</summary>
public sealed class ReduxInstallProgressWindow : Window
{
	private readonly TextBlock _status = new() { TextWrapping = TextWrapping.Wrap };
	private readonly ProgressBar _progress = new() { Height = 6, Margin = new Thickness(0, 16, 0, 12), IsIndeterminate = true };
	private bool _finished;

	public ReduxInstallProgressWindow(Window owner)
	{
		Owner = owner;
		Title = "Installing packages";
		Width = 480;
		SizeToContent = SizeToContent.Height;
		ResizeMode = ResizeMode.NoResize;
		WindowChrome.SetWindowChrome(this, new WindowChrome { CaptionHeight = 0, ResizeBorderThickness = new Thickness(0), GlassFrameThickness = new Thickness(0), UseAeroCaptionButtons = false });
		ShowInTaskbar = false;
		WindowStartupLocation = WindowStartupLocation.CenterOwner;
		Resources.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri("/Redux;component/Themes/MainResourceDictionary.xaml", UriKind.Relative) });
		var settings = MainWindow.Self?.ViewModel?.Settings;
		ReduxThemeService.Apply(Resources, settings?.ColorTheme ?? ReduxThemeType.ReduxDark,
			settings == null ? null : ReduxThemeService.GetActiveTheme(settings));
		SetResourceReference(BackgroundProperty, "ReduxSurfaceBrush");
		SetResourceReference(ForegroundProperty, "ReduxTextPrimaryBrush");
		SetResourceReference(FontFamilyProperty, "Redux.FontFamily.UI");
		SetResourceReference(FontSizeProperty, "Redux.FontSize.14");
		SetResourceReference(TemplateProperty, "ReduxWindowTemplate");
		var root = new StackPanel();
		root.Children.Add(new ReduxWindowTitleBar());
		var body = new StackPanel { Margin = new Thickness(24) };
		AutomationProperties.SetLiveSetting(_status, AutomationLiveSetting.Polite);
		_status.Text = "Preparing Install All…";
		body.Children.Add(_status);
		_progress.SetResourceReference(StyleProperty, "ReduxProgressBarStyle");
		AutomationProperties.SetName(_progress, "Package progress");
		body.Children.Add(_progress);
		var activity = new ProgressBar { Height = 3, IsIndeterminate = true, Margin = new Thickness(0, 0, 0, 12) };
		activity.SetResourceReference(StyleProperty, "ReduxProgressBarStyle");
		AutomationProperties.SetName(activity, "Current package is being processed");
		body.Children.Add(activity);
		body.Children.Add(new TextBlock { Text = "Please wait while Redux checks and installs your packages.", TextWrapping = TextWrapping.Wrap });
		root.Children.Add(body);
		Content = root;
		Closing += (_, e) => { if (!_finished) e.Cancel = true; };
	}

	public async Task ReportAsync(string phase, string name, int current, int total)
	{
		_status.Text = $"{phase} {current} of {total}\n{name}";
		_progress.IsIndeterminate = false;
		_progress.Maximum = Math.Max(1, total);
		_progress.Value = Math.Max(0, current - 1);
		// Present feedback before beginning potentially expensive archive inspection.
		await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.Background);
	}

	public void Run(Func<Task> operation)
	{
		Exception failure = null;
		ContentRendered += async (_, _) =>
		{
			try { await operation(); }
			catch (Exception ex) { failure = ex; }
			finally { _finished = true; Close(); }
		};
		ShowDialog();
		if (failure != null) System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(failure).Throw();
	}
}
