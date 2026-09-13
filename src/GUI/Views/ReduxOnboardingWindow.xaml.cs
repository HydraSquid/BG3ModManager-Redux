using DivinityModManager.AppServices;
using DivinityModManager.Models;
using DivinityModManager.Util;

using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Animation;
using System.Windows.Media;
using System.Windows.Threading;

using WpfScreenHelper;

namespace DivinityModManager.Views;

public partial class ReduxOnboardingWindow : AdonisUI.Controls.AdonisWindow
{
	private readonly MainWindow _ownerWindow;
	private readonly DivinityModManagerSettings _settings;
	private readonly ReduxThemeType _initialTheme;
	private readonly ReduxCustomTheme _initialCustomTheme;
	private readonly (ReduxTypographyFont Font, string CustomReference) _initialTypography;
	private readonly bool _initialLocalOnlyMode;
	private readonly bool _initialGuidanceEnabled;
	private readonly bool _initialReduceMotion;
	private readonly bool _initialDisableBackgroundEffects;
	private bool _themePreviewActive;
	private bool _modulePreviewActive;
	private bool _accessibilityPreviewActive;
	private bool _nxmAssociationChoiceAvailable = true;
	private bool _isInitializing = true;
	private int _step;
	private int _demoStep;
	private int _detailsAnimationVersion;
	public string SelectedGameExecutablePath { get; private set; }
	public bool OpenDownloadsAfterSetup => OpenDownloadsCheckBox.IsChecked == true;
	public bool WasResolved { get; private set; }
	public bool ApplyChanges { get; private set; }
	public bool ThemeSelectionChanged => _themePreviewActive;
	public ReduxThemeType SelectedTheme => !_themePreviewActive ? _initialTheme : ReduxDarkThemeCard.IsChecked == true
		? ReduxThemeType.ReduxDark
		: ReduxLightThemeCard.IsChecked == true
			? ReduxThemeType.ReduxLight
			: ReduxThemeType.Parchment;
	public bool SelectedLocalOnlyMode => SourceIntegrationsCheckBox.IsChecked != true;
	public bool SelectedGuidanceEnabled => GuidanceCheckBox.IsChecked == true;
	public bool SelectedReduceMotion => ReduceMotionCheckBox.IsChecked == true;
	public bool SelectedDisableBackgroundEffects => DisableBackgroundEffectsCheckBox.IsChecked == true;
	public string SelectedNexusApiKey => NexusApiKeyTextBox.Password?.Trim() ?? String.Empty;
	public string SelectedModioApiKey => ModioApiKeyTextBox.Password?.Trim() ?? String.Empty;
	public bool SelectedNxmAssociationEnabled => NxmLinksCheckBox.IsChecked == true;
	public bool SelectedRetainInstalledPackageArchives => RetainArchivesCheckBox.IsChecked == true;

	public ReduxOnboardingWindow(Window owner, DivinityModManagerSettings settings)
	{
		InitializeComponent();
		ReduxWindowBehavior.AttachDialogTransitions(this, 30);
		ReduxWindowBehavior.AttachRoundedCorners(this);
		_ownerWindow = owner as MainWindow;
		_settings = settings;
		SelectedGameExecutablePath = settings?.GameExecutablePath ?? String.Empty;
		UpdateGameStatus();
		_initialTheme = settings?.ColorTheme ?? ReduxThemeType.ReduxDark;
		_initialCustomTheme = ReduxThemeService.GetActiveTheme(settings);
		_initialTypography = ReduxTypographyService.ResolveSelection(settings);
		_initialLocalOnlyMode = settings?.LocalOnlyMode == true;
		_initialGuidanceEnabled = settings?.EnableLoadOrderAdvisor == true;
		_initialReduceMotion = settings?.ReduceMotion == true;
		_initialDisableBackgroundEffects = settings?.DisableBackgroundEffects == true;
		ApplyAdaptiveDefaultSize(owner);
		Loaded += (_, _) => Dispatcher.BeginInvoke(ClampToCurrentWorkArea, DispatcherPriority.Loaded);

		if (owner?.IsLoaded == true)
		{
			Owner = owner;
		}

		if (settings != null)
		{
			ReduxThemeService.Apply(Resources, settings.ColorTheme, ReduxThemeService.GetActiveTheme(settings), settings.UsesGeneratedGradients);
			ReduxDarkThemeCard.IsChecked = settings.ColorTheme == ReduxThemeType.ReduxDark;
			ReduxLightThemeCard.IsChecked = settings.ColorTheme == ReduxThemeType.ReduxLight;
			ParchmentThemeCard.IsChecked = settings.ColorTheme == ReduxThemeType.Parchment;
			SourceIntegrationsCheckBox.IsChecked = !settings.LocalOnlyMode;
			GuidanceCheckBox.IsChecked = settings.EnableLoadOrderAdvisor;
			NexusApiKeyTextBox.Password = settings.NexusModsAPIKey ?? String.Empty;
			ModioApiKeyTextBox.Password = settings.ModioAPIKey ?? String.Empty;
			ReduceMotionCheckBox.IsChecked = settings.ReduceMotion;
			DisableBackgroundEffectsCheckBox.IsChecked = settings.DisableBackgroundEffects;
			var nxmStatus = _ownerWindow?.ViewModel?.GetNxmAssociationStatus();
			NxmLinksCheckBox.IsChecked = nxmStatus?.Status is NxmAssociationStatus.Owned or NxmAssociationStatus.NeedsRepair;
			RetainArchivesCheckBox.IsChecked = settings.RetainInstalledPackageArchives;
			_nxmAssociationChoiceAvailable = nxmStatus?.Success != false;
			NxmLinksCheckBox.ToolTip = _nxmAssociationChoiceAvailable
				? nxmStatus?.Status == NxmAssociationStatus.OwnedByAnotherHandler
					? "Choose Redux, then confirm replacing the current Nexus link handler."
					: nxmStatus?.Message
				: nxmStatus?.Message;
		}
		else
		{
			ReduxDarkThemeCard.IsChecked = true;
			SourceIntegrationsCheckBox.IsChecked = false;
			GuidanceCheckBox.IsChecked = false;
			RetainArchivesCheckBox.IsChecked = false;
		}

		_isInitializing = false;
		UpdateSourceIntegrationState();
		ShowStep(0);
	}

	private void ApplyAdaptiveDefaultSize(Window owner)
	{
		var workArea = owner != null ? Screen.FromWindow(owner).WorkingArea : SystemParameters.WorkArea;
		const double workAreaMargin = 48;
		var availableWidth = Math.Max(320, workArea.Width - workAreaMargin);
		var availableHeight = Math.Max(320, workArea.Height - workAreaMargin);

		// Keep the footer in the arranged window instead of relying on SizeToContent to
		// clip a fixed-size dialog. On constrained/scaled displays the body ScrollViewer
		// absorbs the reduction while the actions remain visible and keyboard reachable.
		MinWidth = Math.Min(MinWidth, availableWidth);
		MinHeight = Math.Min(MinHeight, availableHeight);
		MaxWidth = Math.Max(MinWidth, Math.Min(MaxWidth, availableWidth));
		MaxHeight = Math.Max(MinHeight, Math.Min(MaxHeight, availableHeight));
		Width = Math.Clamp(Math.Clamp(workArea.Width * 0.44, 700, 780), MinWidth, MaxWidth);
		Height = Math.Clamp(Math.Clamp(workArea.Height * 0.82, 640, 740), MinHeight, MaxHeight);
		SizeToContent = SizeToContent.Manual;
	}

	private void ClampToCurrentWorkArea()
	{
		var workArea = Screen.FromWindow(this).WorkingArea;
		var renderedWidth = ActualWidth > 0 ? ActualWidth : Width;
		var renderedHeight = ActualHeight > 0 ? ActualHeight : Height;
		Left = Math.Clamp(Left, workArea.Left, Math.Max(workArea.Left, workArea.Right - renderedWidth));
		Top = Math.Clamp(Top, workArea.Top, Math.Max(workArea.Top, workArea.Bottom - renderedHeight));
	}

	private void ThemeCard_Click(object sender, RoutedEventArgs e)
	{
		if (sender is RadioButton { Tag: ReduxThemeType theme })
		{
			ReduxThemeService.Apply(Resources, theme);
			_ownerWindow?.PreviewColorTheme(theme);
			var typography = _settings?.UseThemeDefaultTypography != false
				? (ReduxTypographyService.GetThemeDefault(theme), String.Empty)
				: (_settings.TypographyFont, _settings.CustomTypographyFont);
			ReduxTypographyService.Apply(Resources, typography.Item1, typography.Item2);
			_themePreviewActive = true;
		}
	}

	private void SourceIntegrationsCheckBox_Changed(object sender, RoutedEventArgs e)
	{
		if (!_isInitializing)
		{
			UpdateSourceIntegrationState();
			ApplyModulePreview();
		}
	}

	private void UpdateSourceIntegrationState()
	{
		if (SourceCredentialsPanel == null)
		{
			return;
		}

		var showCredentials = SourceIntegrationsCheckBox.IsChecked == true;
		NxmLinksCheckBox.IsEnabled = showCredentials && _nxmAssociationChoiceAvailable;
		if (!IsLoaded || ReduxWindowBehavior.ReduceMotion)
		{
			SourceCredentialsPanel.BeginAnimation(FrameworkElement.HeightProperty, null);
			SourceCredentialsPanel.BeginAnimation(UIElement.OpacityProperty, null);
			SourceCredentialsPanel.ClearValue(FrameworkElement.HeightProperty);
			SourceCredentialsPanel.Opacity = 1;
			SourceCredentialsPanel.Visibility = showCredentials ? Visibility.Visible : Visibility.Collapsed;
			return;
		}

		AnimateSourceCredentials(showCredentials);
	}

	private void AnimateSourceCredentials(bool show)
	{
		var panel = SourceCredentialsPanel;
		panel.BeginAnimation(FrameworkElement.HeightProperty, null);
		panel.BeginAnimation(UIElement.OpacityProperty, null);

		if (show)
		{
			panel.Visibility = Visibility.Visible;
			panel.ClearValue(FrameworkElement.HeightProperty);
			var measureWidth = panel.ActualWidth;
			if (measureWidth <= 1 && panel.Parent is FrameworkElement parent)
			{
				measureWidth = parent.ActualWidth;
			}
			if (measureWidth <= 1)
			{
				measureWidth = Math.Max(1, ActualWidth - 72);
			}
			panel.Measure(new Size(measureWidth, Double.PositiveInfinity));
			var targetHeight = Math.Max(1, panel.DesiredSize.Height);
			panel.Height = 0;
			panel.Opacity = 0;
			AnimateSourceCredentialsTo(panel, targetHeight, 1, collapseWhenComplete: false);
			return;
		}

		var currentHeight = Math.Max(1, panel.ActualHeight);
		panel.Height = currentHeight;
		panel.Opacity = 1;
		AnimateSourceCredentialsTo(panel, 0, 0, collapseWhenComplete: true);
	}

	private static void AnimateSourceCredentialsTo(
		FrameworkElement panel,
		double targetHeight,
		double targetOpacity,
		bool collapseWhenComplete)
	{
		var duration = TimeSpan.FromMilliseconds(180);
		var easing = new CubicEase { EasingMode = EasingMode.EaseInOut };
		var heightAnimation = new DoubleAnimation(targetHeight, duration) { EasingFunction = easing };
		var opacityAnimation = new DoubleAnimation(targetOpacity, duration) { EasingFunction = easing };

		heightAnimation.Completed += (_, _) =>
		{
			panel.BeginAnimation(FrameworkElement.HeightProperty, null);
			panel.BeginAnimation(UIElement.OpacityProperty, null);
			panel.ClearValue(FrameworkElement.HeightProperty);
			panel.Opacity = 1;
			if (collapseWhenComplete)
			{
				panel.Visibility = Visibility.Collapsed;
			}
		};

		panel.BeginAnimation(FrameworkElement.HeightProperty, heightAnimation, HandoffBehavior.SnapshotAndReplace);
		panel.BeginAnimation(UIElement.OpacityProperty, opacityAnimation, HandoffBehavior.SnapshotAndReplace);
	}

	private void GuidanceCheckBox_Changed(object sender, RoutedEventArgs e)
	{
		if (!_isInitializing)
		{
			ApplyModulePreview();
		}
	}

	private void ApplyModulePreview()
	{
		if (_settings == null || _isInitializing)
		{
			return;
		}

		_settings.LocalOnlyMode = SelectedLocalOnlyMode;
		_settings.EnableLoadOrderAdvisor = SelectedGuidanceEnabled;
		_modulePreviewActive = true;
	}

	private void AccessibilityCheckBox_Changed(object sender, RoutedEventArgs e)
	{
		if (_isInitializing)
		{
			return;
		}

		ReduxWindowBehavior.ConfigureAccessibility(
			SelectedReduceMotion,
			SelectedDisableBackgroundEffects);
		_accessibilityPreviewActive = true;
		if (SelectedReduceMotion)
		{
			BeforePlayExpansion_Changed(BeforePlayExpander, new RoutedEventArgs());
			foreach (var element in new FrameworkElement[] { AppearancePage, ConnectionsPage, ManagersPage, OptionsPage, DemoActiveText, DemoInstructionText })
			{
				element.BeginAnimation(OpacityProperty, null);
				element.Opacity = 1;
				element.RenderTransform = Transform.Identity;
			}
		}

	}

	private void CloseButton_Click(object sender, RoutedEventArgs e)
	{
		WasResolved = true;
		ApplyChanges = false;
		Close();
	}

	private void UpdateGameStatus()
	{
		var exists = System.IO.File.Exists(SelectedGameExecutablePath);
		GameStatusText.Text = exists ? "Game executable found" : "Game location needs attention";
		GameStatusText.SetResourceReference(TextBlock.ForegroundProperty, exists ? "ReduxSuccessBrush" : "ReduxWarningBrush");
		GamePathText.Text = exists ? SelectedGameExecutablePath : "Locate bg3.exe or bg3_dx11.exe in the game’s bin folder. You can also do this later in Preferences.";
		GamePathText.ToolTip = GamePathText.Text;
	}

	private void ChooseGame_Click(object sender, RoutedEventArgs e)
	{
		var picker = new Microsoft.Win32.OpenFileDialog { Title = "Locate Baldur’s Gate 3", Filter = "Baldur’s Gate 3|bg3.exe;bg3_dx11.exe", CheckFileExists = true };
		if (picker.ShowDialog(this) != true) return;
		var name = System.IO.Path.GetFileName(picker.FileName);
		if (!name.Equals("bg3.exe", StringComparison.OrdinalIgnoreCase) && !name.Equals("bg3_dx11.exe", StringComparison.OrdinalIgnoreCase)) return;
		SelectedGameExecutablePath = picker.FileName;
		UpdateGameStatus();
	}

	private void DemoAction_Click(object sender, RoutedEventArgs e)
	{
		_demoStep = (_demoStep + 1) % 4;
		DemoInactiveText.Text = _demoStep == 0 ? "Example mod" : "No inactive mods";
		DemoActiveText.Text = _demoStep == 0 ? "No active mods" : "1  Example mod";
		DemoInstructionText.Text = new[] {
			"New mods start inactive. Activate the example mod to include it in your load order.",
			"Active mods are included in your order. Save Current Order keeps this arrangement in Redux.",
			"Saved in Redux. Sync Load Order to Game applies that order to Baldur’s Gate 3; saving alone does not apply it.",
			"Example complete. In your real order, review the sync changes and any Mod Diagnostics before launching the game."
		}[_demoStep];
		DemoActionLabel.Text = new[] { "Activate example mod", "Save example order", "Sync example to game", "Try again" }[_demoStep];
		DemoProgressText.Text = new[] { "Installed → Activate → Save → Sync", "Active → Save → Sync", "Saved in Redux → Sync to game", "Synced · Example complete" }[_demoStep];
		DemoProgressText.SetResourceReference(TextBlock.ForegroundProperty, _demoStep == 3 ? "ReduxSuccessBrush" : "ReduxAccentHoverBrush");
		AnimateEntrance(DemoActiveText, 1);
		AnimateEntrance(DemoInstructionText, 1);

	}

	private void BeforePlayExpansion_Changed(object sender, RoutedEventArgs e)
	{
		if (sender is not Expander expander) return;
		expander.ApplyTemplate();
		if (expander.Template.FindName("Details", expander) is not Border panel) return;
		var version = ++_detailsAnimationVersion;
		var currentHeight = panel.Visibility == Visibility.Visible ? panel.ActualHeight : 0;
		panel.BeginAnimation(HeightProperty, null);
		panel.BeginAnimation(OpacityProperty, null);
		var expanded = expander.IsExpanded;
		if (!IsLoaded || SelectedReduceMotion || ReduxWindowBehavior.ReduceMotion)
		{
			panel.Height = expanded ? Double.NaN : 0;
			panel.Opacity = 1;
			panel.Visibility = expanded ? Visibility.Visible : Visibility.Collapsed;
			return;
		}
		panel.Visibility = Visibility.Visible;
		panel.Child.Measure(new Size(Math.Max(1, expander.ActualWidth), Double.PositiveInfinity));
		var targetHeight = expanded ? panel.Child.DesiredSize.Height : 0;
		panel.Height = currentHeight;
		var duration = TimeSpan.FromMilliseconds(180);
		var animation = new DoubleAnimation(currentHeight, targetHeight, duration)
		{
			EasingFunction = new CubicEase { EasingMode = EasingMode.EaseInOut }
		};
		animation.Completed += (_, _) =>
		{
			if (version != _detailsAnimationVersion) return;
			panel.BeginAnimation(HeightProperty, null);
			panel.BeginAnimation(OpacityProperty, null);
			panel.Height = expander.IsExpanded ? Double.NaN : 0;
			panel.Opacity = 1;
			panel.Visibility = expander.IsExpanded ? Visibility.Visible : Visibility.Collapsed;
		};
		panel.BeginAnimation(HeightProperty, animation, HandoffBehavior.SnapshotAndReplace);
		panel.BeginAnimation(OpacityProperty, new DoubleAnimation(expanded ? 1 : 0, duration), HandoffBehavior.SnapshotAndReplace);
	}

	private void StepButton_Click(object sender, RoutedEventArgs e)
	{
		if (sender is Button button && Int32.TryParse(button.Tag?.ToString(), out var step)) ShowStep(step);
	}

	private void AnimateEntrance(FrameworkElement element, int direction)
	{
		element.BeginAnimation(OpacityProperty, null);
		element.Opacity = 1;
		element.RenderTransform = Transform.Identity;
		if (!IsLoaded || SelectedReduceMotion || ReduxWindowBehavior.ReduceMotion) return;
		var offset = new TranslateTransform();
		element.RenderTransform = offset;
		var duration = TimeSpan.FromMilliseconds(180);
		var ease = new CubicEase { EasingMode = EasingMode.EaseOut };
		element.BeginAnimation(OpacityProperty, new DoubleAnimation(0.25, 1, duration) { FillBehavior = FillBehavior.Stop });
		offset.BeginAnimation(TranslateTransform.XProperty, new DoubleAnimation(12 * direction, 0, duration) { EasingFunction = ease, FillBehavior = FillBehavior.Stop });
	}

	private void BackButton_Click(object sender, RoutedEventArgs e) => ShowStep(_step - 1);

	private void ShowStep(int step)
	{
		var direction = step >= _step ? 1 : -1;
		_step = Math.Clamp(step, 0, 3);
		AppearancePage.Visibility = _step == 0 ? Visibility.Visible : Visibility.Collapsed;
		ConnectionsPage.Visibility = _step == 1 ? Visibility.Visible : Visibility.Collapsed;
		ManagersPage.Visibility = _step == 2 ? Visibility.Visible : Visibility.Collapsed;
		OptionsPage.Visibility = _step == 3 ? Visibility.Visible : Visibility.Collapsed;
		StepProgressText.Text = $"Step {_step + 1} of 4 · {new[] { "Your setup", "Add mods", "Saves & DLLs", "Load order" }[_step]}";
		StepTitleText.Text = new[] { "Welcome to Redux", "Choose how you add mods", "Beyond the load order", "From installed to ready to play" }[_step];
		StepDescriptionText.Text = new[] { "Check your game location and make the interface comfortable.", "Review packages in Download Manager before installing them.", "Bring your saves and game-directory mods into Redux.", "Learn the workflow with an example, then add your own mods." }[_step];
		BackButton.Visibility = _step > 0 ? Visibility.Visible : Visibility.Collapsed;
		ContinueLabel.Text = _step == 3 ? "Finish setup" : "Next";
		ContinueIcon.SetResourceReference(Controls.ReduxIcon.StrokeDataProperty, _step == 3 ? "Redux.Icon.Check" : "Redux.Icon.ChevronRightStroke");
		var stepButtons = new[] { SetupStepButton, SourcesStepButton, ManagersStepButton, OrderStepButton };
		for (var i = 0; i < stepButtons.Length; i++)
		{
			stepButtons[i].SetResourceReference(StyleProperty, i == _step ? "ReduxPrimaryActionButtonStyle" : "ReduxSecondaryActionButtonStyle");
			System.Windows.Automation.AutomationProperties.SetItemStatus(stepButtons[i], i == _step ? "Current step" : "Go to step");
		}
		AnimateEntrance(_step == 0 ? AppearancePage : _step == 1 ? ConnectionsPage : _step == 2 ? ManagersPage : OptionsPage, direction);
		OnboardingContentScrollViewer.ScrollToTop();
		System.Windows.Input.FocusManager.SetFocusedElement(this, _step == 0 ? ReduxDarkThemeCard : _step == 1 ? SourceIntegrationsCheckBox : _step == 2 ? SaveContinueButton : GuidanceCheckBox);
	}

	private void SaveButton_Click(object sender, RoutedEventArgs e)
	{
		if (_step < 3)
		{
			ShowStep(_step + 1);
			return;
		}
		WasResolved = true;
		ApplyChanges = true;
		Close();
	}

	protected override void OnClosing(CancelEventArgs e)
	{
		if (!WasResolved)
		{
			WasResolved = true;
			ApplyChanges = false;
		}

		if (!ApplyChanges && _themePreviewActive)
		{
			_ownerWindow?.PreviewColorTheme(_initialTheme, _initialCustomTheme);
			ReduxTypographyService.Apply(Resources, _initialTypography.Font, _initialTypography.CustomReference);
		}
		if (!ApplyChanges && _modulePreviewActive && _settings != null)
		{
			_settings.LocalOnlyMode = _initialLocalOnlyMode;
			_settings.EnableLoadOrderAdvisor = _initialGuidanceEnabled;
		}
		if (!ApplyChanges && _accessibilityPreviewActive)
		{
			ReduxWindowBehavior.ConfigureAccessibility(
				_initialReduceMotion,
				_initialDisableBackgroundEffects);
		}

		base.OnClosing(e);
	}
}
