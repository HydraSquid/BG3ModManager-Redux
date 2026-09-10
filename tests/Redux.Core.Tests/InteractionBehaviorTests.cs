using DivinityModManager;
using DivinityModManager.AppServices;
using DivinityModManager.Controls;
using DivinityModManager.Models;
using DivinityModManager.Models.App;
using DivinityModManager.Models.Modio;
using DivinityModManager.Models.NexusMods;
using DivinityModManager.Util;
using DivinityModManager.Views;

using System;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace Redux.Core.Tests;

public sealed class InteractionBehaviorTests
{
	public void ReduceMotionKeepsPrimaryListStoryboardsFreezeSafeAndInstant()
	{
		ReduxWindowBehavior.ConfigureAccessibility(false, ReduxWindowBehavior.BackgroundEffectsDisabled);
		var animation = new ReduxDoubleAnimation { From = 0, To = 0.62, Duration = TimeSpan.FromMilliseconds(120) };
		var flash = new ReduxSelectionFlashAnimation { Duration = TimeSpan.FromMilliseconds(220) };
		var storyboard = new Storyboard();
		storyboard.Children.Add(animation);
		storyboard.Children.Add(flash);
		storyboard.Freeze();
		RegressionAssert.True(storyboard.IsFrozen);

		ReduxWindowBehavior.ConfigureAccessibility(true, ReduxWindowBehavior.BackgroundEffectsDisabled);
		var opacityCore = typeof(ReduxDoubleAnimation).GetMethod(
			"GetCurrentValueCore",
			BindingFlags.Instance | BindingFlags.NonPublic)
			?? throw new InvalidOperationException("Reduced-motion opacity animation core was not found.");
		var flashCore = typeof(ReduxSelectionFlashAnimation).GetMethod(
			"GetCurrentValueCore",
			BindingFlags.Instance | BindingFlags.NonPublic)
			?? throw new InvalidOperationException("Reduced-motion selection animation core was not found.");
		RegressionAssert.Equal(0.62, (double)opacityCore.Invoke(animation, [0d, 0d, animation.CreateClock()])!);
		RegressionAssert.Equal(0d, (double)flashCore.Invoke(flash, [0d, 0d, flash.CreateClock()])!);

		ReduxWindowBehavior.ConfigureAccessibility(false, ReduxWindowBehavior.BackgroundEffectsDisabled);
	}

	public void SaveCampaignAnimationReplacesFrozenTransforms()
	{
		var content = new Border();
		var frozen = new TranslateTransform(0, 0);
		frozen.Freeze();
		content.RenderTransform = frozen;
		var method = typeof(ReduxSaveManagerWindow).GetMethod(
			"EnsureWritableCampaignTransform",
			BindingFlags.NonPublic | BindingFlags.Static)
			?? throw new InvalidOperationException("Save campaign transform guard was not found.");
		var writable = method.Invoke(null, [content]) as TranslateTransform
			?? throw new InvalidOperationException("Save campaign transform guard returned no transform.");

		RegressionAssert.False(writable.IsFrozen);
		RegressionAssert.False(ReferenceEquals(frozen, writable));
		writable.Y = -5;
		RegressionAssert.Equal(-5d, writable.Y);
	}

	public void ModListHeaderSpansTheGutterAndScrollbarStartsBelowIt()
	{
		var resources = new ResourceDictionary
		{
			Source = new Uri(
				"pack://application:,,,/Redux;component/Themes/MainResourceDictionary.xaml",
				UriKind.Absolute)
		};
		var host = new Grid { Width = 12, Height = 260, Resources = resources };
		var scrollBar = new ScrollBar
		{
			Width = 12,
			Height = 260,
			Maximum = 100,
			ViewportSize = 20,
			Template = (ControlTemplate)resources["ReduxModListVerticalScrollBarTemplate"]
		};
		host.Children.Add(scrollBar);
		host.Measure(new Size(12, 260));
		host.Arrange(new Rect(0, 0, 12, 260));
		host.UpdateLayout();

		var headerSurface = (Border?)scrollBar.Template.FindName("ColumnHeaderSurface", scrollBar)
			?? throw new InvalidOperationException("The mod-list scrollbar header surface was not found.");
		var track = (Track?)scrollBar.Template.FindName("PART_Track", scrollBar)
			?? throw new InvalidOperationException("The mod-list scrollbar track was not found.");

		if (Math.Abs(headerSurface.ActualWidth - scrollBar.ActualWidth) >= 0.01)
			throw new InvalidOperationException($"Header width {headerSurface.ActualWidth} did not match scrollbar width {scrollBar.ActualWidth}.");
		var trackTop = track.TransformToAncestor(scrollBar).Transform(new Point()).Y;
		if (Math.Abs(trackTop - headerSurface.ActualHeight) >= 0.01)
			throw new InvalidOperationException($"Track started at {trackTop} instead of below the {headerSurface.ActualHeight}px header surface.");
	}

	public void DrawerRetainsASelectedModDuringCrossListTransferOnly()
	{
		var displayed = new DivinityModData { UUID = "moving-mod", IsSelected = true };
		var retained = SelectionContinuity.ResolveDisplayedItem<DivinityModData>(
			null,
			displayed,
			mod => mod.IsSelected);
		RegressionAssert.Equal(displayed, retained);

		displayed.IsSelected = false;
		var cleared = SelectionContinuity.ResolveDisplayedItem<DivinityModData>(
			null,
			displayed,
			mod => mod.IsSelected);
		RegressionAssert.True(cleared == null);

		var replacement = new DivinityModData { UUID = "replacement" };
		var replaced = SelectionContinuity.ResolveDisplayedItem(
			replacement,
			displayed,
			_ => true);
		RegressionAssert.Equal(replacement, replaced);
	}

	public void SavingCurrentOrderCanNeverWriteTheGameExportFile()
	{
		var current = new DivinityLoadOrder
		{
			Name = "Current",
			FilePath = @"C:\Profiles\Public\modsettings.lsx",
			IsModSettings = true
		};
		var defensiveLsxCase = new DivinityLoadOrder
		{
			Name = "Unexpected LSX",
			FilePath = @"C:\Profiles\Public\MODSETTINGS.LSX"
		};
		var saved = new DivinityLoadOrder
		{
			Name = "My Order",
			FilePath = @"C:\Orders\My Order.json"
		};

		RegressionAssert.True(LoadOrderPersistencePolicy.RequiresSaveAs(current));
		RegressionAssert.True(LoadOrderPersistencePolicy.RequiresSaveAs(defensiveLsxCase));
		RegressionAssert.False(LoadOrderPersistencePolicy.RequiresSaveAs(saved));
	}

	public void NewBlankOrderContainsNoActivatedMods()
	{
		var order = LoadOrderPersistencePolicy.CreateBlankOrder(
			"New Load Order",
			@"C:\Orders\New Load Order.json");

		RegressionAssert.Equal("New Load Order", order.Name);
		RegressionAssert.Equal(@"C:\Orders\New Load Order.json", order.FilePath);
		RegressionAssert.Equal(0, order.Order.Count);
		RegressionAssert.False(order.IsModSettings);
	}

	public void WorkingChangesStayDetachedUntilExplicitlySaved()
	{
		var saved = new DivinityLoadOrder
		{
			Name = "My Order",
			FilePath = @"C:\Orders\My Order.json",
			Order = [new DivinityLoadOrderEntry { UUID = "saved-mod" }]
		};
		var activeMods = new[]
		{
			new DivinityModData { UUID = "working-mod", Name = "Working Mod" }
		};

		var working = LoadOrderPersistencePolicy.CreateWorkingCopy(saved, activeMods);

		RegressionAssert.Equal(1, saved.Order.Count);
		RegressionAssert.Equal(1, working.Order.Count);
		RegressionAssert.Equal("saved-mod", saved.Order[0].UUID);
		RegressionAssert.Equal("working-mod", working.Order[0].UUID);
		RegressionAssert.Equal(saved.Name, working.Name);
		RegressionAssert.Equal(saved.FilePath, working.FilePath);
		RegressionAssert.False(ReferenceEquals(saved, working));
	}

	public void SavedCurrentStateRestoresIntoTheSingleCurrentEntry()
	{
		var current = new DivinityLoadOrder
		{
			Name = "Current",
			FilePath = @"C:\Profiles\Public\modsettings.lsx",
			IsModSettings = true,
			Order = [new DivinityLoadOrderEntry { UUID = "game-order" }]
		};
		var savedState = new DivinityLoadOrder
		{
			Name = "Current",
			FilePath = @"C:\Redux\Data\CurrentOrders\profile.json",
			Order = [new DivinityLoadOrderEntry { UUID = "saved-working-order" }]
		};

		RegressionAssert.True(LoadOrderPersistencePolicy.RestoreSavedCurrentState(current, savedState));
		RegressionAssert.Equal("Current", current.Name);
		RegressionAssert.Equal(@"C:\Profiles\Public\modsettings.lsx", current.FilePath);
		RegressionAssert.True(current.IsModSettings);
		RegressionAssert.Equal(1, current.Order.Count);
		RegressionAssert.Equal("saved-working-order", current.Order[0].UUID);
	}

	public void DuplicateWandChoiceNormalizesToTheSingleVisibleIcon()
	{
		RegressionAssert.Equal("wand", ReduxIconCatalog.Normalize("wand-sparkles"));
		RegressionAssert.Equal(1, ReduxIconCatalog.Choices.Where(choice =>
			choice.Id.Contains("wand", StringComparison.OrdinalIgnoreCase)).Count());
	}

	public void CustomThemeEditorShellsPreviewTheBackgroundRoleLive()
	{
		Application.Current.ShutdownMode = ShutdownMode.OnExplicitShutdown;
		var theme = ReduxThemeService.CreateFromBase("Live shell preview", ReduxThemeType.ReduxDark);
		var editor = new CustomThemeEditorWindow(theme);
		var picker = new CategoryNameDialog("Background", theme.BackgroundColor, false);
		try
		{
			ReduxThemeService.Apply(picker.Resources, theme.BaseTheme, theme);
			theme.BackgroundColor = "#E409FF";
			ReduxThemeService.PreviewColors(theme, editor.Resources, picker.Resources);
			editor.Measure(new Size(editor.Width, editor.Height));
			editor.Arrange(new Rect(0, 0, editor.Width, editor.Height));
			picker.Measure(new Size(picker.Width, picker.Height));
			picker.Arrange(new Rect(0, 0, picker.Width, picker.Height));
			editor.UpdateLayout();
			picker.UpdateLayout();

			var editorShell = (Border?)editor.FindName("EditorWindowShell")
				?? throw new InvalidOperationException("The custom-theme editor window shell was not found.");
			var pickerShell = (Border?)picker.FindName("DialogWindowShell")
				?? throw new InvalidOperationException("The color-picker window shell was not found.");
			var expected = Color.FromRgb(0xE4, 0x09, 0xFF);
			RegressionAssert.Equal(expected, RequireSolidColor(editor.Background, "editor window"));
			RegressionAssert.Equal(expected, RequireSolidColor(editorShell.Background, "editor shell"));
			RegressionAssert.Equal(expected, RequireSolidColor(pickerShell.Background, "picker shell"));
		}
		finally
		{
			picker.Close();
			editor.Close();
		}

		static Color RequireSolidColor(Brush brush, string surface) =>
			brush is SolidColorBrush solid
				? solid.Color
				: throw new InvalidOperationException($"The {surface} did not resolve a solid semantic background brush.");
	}

	public void PreferencesAndEditorActionsUseModernChromeAndLabeledIcons()
	{
		Application.Current.ShutdownMode = ShutdownMode.OnExplicitShutdown;
		var settings = new SettingsWindow();
		var newCategory = new CategoryNameDialog("", "#8A6AF1", true);
		var existingCategory = new CategoryNameDialog("Gameplay", "#D7A24B", false);
		try
		{
			ReduxThemeService.Apply(settings.Resources, ReduxThemeType.ReduxDark);
			settings.Measure(new Size(settings.Width, settings.Height));
			settings.Arrange(new Rect(0, 0, settings.Width, settings.Height));
			settings.UpdateLayout();

			var modernTemplate = (ControlTemplate)settings.FindResource("ReduxActionButtonTemplate");
			var duplicate = (Button)settings.FindName("DuplicateCustomThemeButton");
			var delete = (Button)settings.FindName("DeleteCustomThemeButton");
			RegressionAssert.True(ReferenceEquals(modernTemplate, duplicate.Template));
			RegressionAssert.True(ReferenceEquals(modernTemplate, delete.Template));
			AssertLabeledIcon(duplicate, "Duplicate");
			AssertLabeledIcon(delete, "Delete");
			RegressionAssert.Equal(
				((SolidColorBrush)settings.FindResource("ReduxErrorBrush")).Color,
				((SolidColorBrush)delete.Foreground).Color);

			var addButton = (Button)newCategory.FindName("ConfirmButton");
			var saveButton = (Button)existingCategory.FindName("ConfirmButton");
			AssertLabeledIcon(addButton, "Add");
			AssertLabeledIcon(saveButton, "Save");
			RegressionAssert.True(ReferenceEquals(
				newCategory.FindResource("Redux.Icon.AddCircle"),
				((ReduxIcon)((StackPanel)addButton.Content).Children[0]).StrokeData));
			RegressionAssert.True(ReferenceEquals(
				existingCategory.FindResource("Redux.Icon.Save"),
				((ReduxIcon)((StackPanel)saveButton.Content).Children[0]).StrokeData));
		}
		finally
		{
			existingCategory.Close();
			newCategory.Close();
			settings.Close();
		}

		static void AssertLabeledIcon(Button button, string expectedLabel)
		{
			if (button.Content is not StackPanel content)
				throw new InvalidOperationException($"The {expectedLabel} action does not retain structured icon-and-label content.");
			RegressionAssert.True(content.Children.OfType<ReduxIcon>().Any());
			RegressionAssert.True(content.Children.OfType<TextBlock>().Any(text => text.Text == expectedLabel));
		}
	}

	public void BuiltInIconPickerHasAUniqueExpandedCatalog()
	{
		var choices = ReduxIconCatalog.Choices.Where(choice => !choice.IsNone).ToList();

		RegressionAssert.Equal(choices.Count, choices.Select(choice => choice.Id)
			.Distinct(StringComparer.OrdinalIgnoreCase).Count());
		RegressionAssert.Equal(choices.Count, choices.Select(choice => choice.ResourceKey)
			.Distinct(StringComparer.OrdinalIgnoreCase).Count());
		RegressionAssert.True(choices.Count >= 110);
		RegressionAssert.True(ReduxIconCatalog.TryGet("backpack", out _));
		RegressionAssert.True(ReduxIconCatalog.TryGet("languages", out _));
		RegressionAssert.True(ReduxIconCatalog.TryGet("workflow", out _));
	}

	public void AsyncProviderMetadataSignalsAutomaticCategoryRefresh()
	{
		var mod = new DivinityModData { UUID = "metadata-refresh" };
		var initialRevision = mod.CategoryMetadataRevision;

		mod.ModioData.Update(new ModioModData
		{
			UUID = mod.UUID,
			ModId = 42,
			Name = "Interface Improvements"
		});

		RegressionAssert.True(mod.CategoryMetadataRevision > initialRevision);
	}

	public void MenuSemanticColorDistinguishesSavingFromSaveNavigation()
	{
		RegressionAssert.True(ReduxMenuItemExtension.IsPositiveCommitAction("Save Current Order"));
		RegressionAssert.True(ReduxMenuItemExtension.IsPositiveCommitAction("Export Redux Modlist..."));
		RegressionAssert.False(ReduxMenuItemExtension.IsPositiveCommitAction("Save Game Manager..."));
		RegressionAssert.False(ReduxMenuItemExtension.IsPositiveCommitAction("Save Games Folder"));
	}

	public void CommandTooltipUsesLiveShortcutAndSharedDescription()
	{
		var hotkey = new Hotkey(System.Windows.Input.Key.S, System.Windows.Input.ModifierKeys.Control)
		{
			DisplayName = "Save Current Order",
			Description = "Save changes to the selected load order."
		};

		RegressionAssert.Equal(
			"Save Current Order (Ctrl + S)\nSave changes to the selected load order.",
			hotkey.CommandToolTip);

		hotkey.Clear();
		RegressionAssert.Equal(
			"Save Current Order\nSave changes to the selected load order.",
			hotkey.CommandToolTip);
	}

	public void CommandPaletteItemTemplateResolvesCoreBindingsAtRuntime()
	{
		var originalInteractionColors = DivinityApp.UseCategoryColorsForInteractions;
		DivinityApp.UseCategoryColorsForInteractions = true;
		var window = new ReduxCommandPaletteWindow(null!, null!, null!);
		try
		{
			var errorInteractionBrush = new SolidColorBrush(Colors.Red);
			var primaryTextBrush = new SolidColorBrush(Colors.White);
			window.Resources["ReduxErrorPillBackground"] = errorInteractionBrush;
			window.Resources["ReduxErrorBrush"] = errorInteractionBrush;
			window.Resources["ReduxTextPrimaryBrush"] = primaryTextBrush;
			var list = (ListBox)window.FindName("CommandList");
			list.ItemsSource = new[]
			{
				new ReduxCommandPaletteItem(
					"Delete Selected Mods...",
					"Mod lists",
					"Delete selected mods.",
					"Delete",
					"trash",
					() => { },
					tone: ReduxCommandPaletteTone.Error),
				new ReduxCommandPaletteItem(
					"Filter category: Gameplay",
					"Category filters",
					"Show mods assigned to this category.",
					String.Empty,
					"gameplay",
					() => { },
					accentColor: "#D7A24B")
			};
			window.Measure(new Size(620, 530));
			window.Arrange(new Rect(0, 0, 620, 530));
			window.UpdateLayout();
			RegressionAssert.Equal(2, list.Items.Count);

			(ListBoxItem Item, Border Surface) CreateSelectedSurface(ReduxCommandPaletteItem data)
			{
				var item = new ListBoxItem
				{
					DataContext = data,
					IsSelected = true,
					Style = (Style)window.FindResource("CommandPaletteItemStyle")
				};
				item.Resources["ReduxErrorPillBackground"] = errorInteractionBrush;
				item.Resources["ReduxErrorBrush"] = errorInteractionBrush;
				item.Resources["ReduxTextPrimaryBrush"] = primaryTextBrush;
				item.Resources["Redux.Rail.Thickness"] = new Thickness(3, 0, 0, 0);
				item.ApplyTemplate();
				item.Measure(new Size(560, 60));
				item.Arrange(new Rect(0, 0, 560, 60));
				item.UpdateLayout();
				return (item, (Border)item.Template.FindName("ContextualSelectionSurface", item));
			}

			var errorSelection = CreateSelectedSurface((ReduxCommandPaletteItem)list.Items[0]);
			var errorSurface = errorSelection.Surface;
			if (errorSurface.Background is not SolidColorBrush errorBrush
				|| errorBrush.Color != Colors.Red
				|| errorSurface.Opacity != 1)
				throw new InvalidOperationException($"Semantic selection brush did not resolve; received {errorSurface.Background?.GetType().Name ?? "null"} " +
					$"{(errorSurface.Background as SolidColorBrush)?.Color} at opacity {errorSurface.Opacity}; " +
					$"tone {(errorSurface.DataContext as ReduxCommandPaletteItem)?.Tone}, interactions {DivinityApp.UseCategoryColorsForInteractions}.");

			var selectionRail = (Border)errorSelection.Item.Template.FindName("SelectionRail", errorSelection.Item);
			var hoverRail = (Border)errorSelection.Item.Template.FindName("HoverRail", errorSelection.Item);
			var gesturePill = (Border)errorSelection.Item.Template.FindName("GesturePill", errorSelection.Item);
			var gestureText = (TextBlock)errorSelection.Item.Template.FindName("GestureText", errorSelection.Item);
			RegressionAssert.Equal(1d, selectionRail.Opacity);
			RegressionAssert.Equal(new Thickness(3, 0, 0, 0), selectionRail.BorderThickness);
			RegressionAssert.Equal(Colors.Red, ((SolidColorBrush)selectionRail.BorderBrush).Color);
			RegressionAssert.Equal(Colors.Transparent, ((SolidColorBrush)hoverRail.BorderBrush).Color);
			RegressionAssert.Equal(Colors.Transparent, ((SolidColorBrush)gesturePill.BorderBrush).Color);
			RegressionAssert.Equal(Colors.White, ((SolidColorBrush)gestureText.Foreground).Color);

			var categorySurface = CreateSelectedSurface((ReduxCommandPaletteItem)list.Items[1]).Surface;
			if (categorySurface.Background is not LinearGradientBrush categoryBrush
				|| categoryBrush.GradientStops.Count != 2
				|| !categoryBrush.GradientStops.All(stop => stop.Color.R == 0xD7
					&& stop.Color.G == 0xA2
					&& stop.Color.B == 0x4B)
				|| categorySurface.Opacity != 1)
				throw new InvalidOperationException($"Category selection brush did not resolve; received {categorySurface.Background?.GetType().Name ?? "null"}.");
		}
		finally
		{
			window.Close();
			DivinityApp.UseCategoryColorsForInteractions = originalInteractionColors;
		}
	}

	public void ReduxDialogTemplatesResolveCoreBindingsAtRuntime()
	{
		var nexusDownloads = new ReduxNexusDownloadsWindow();
		ReduxThemeService.Apply(nexusDownloads.Resources, ReduxThemeType.ReduxDark);
		var installReview = new ReduxInstallReviewWindow(null!,
			[new ReduxInstallReviewItem("Clean package", "Version 1.0", "New mod", ReduxInstallReviewTone.Success)],
			false, "Inactive Mods", "Destination: Inactive Mods · 1 new", true);
		RegressionAssert.Equal(Visibility.Visible,
			((CheckBox)installReview.FindName("SkipCleanReviewCheckBox")).Visibility);
		var downloadsList = (ListBox)nexusDownloads.FindName("DownloadsList");
		downloadsList.ItemsSource = new[]
		{
			new NxmDownloadItem
			{
				ProjectName = "Runtime template check",
				FileName = "runtime-template-check.zip",
				State = NxmDownloadState.Downloaded,
				ThumbnailUrl = ""
			}
		};
		var deleteFiles = new DeleteFilesConfirmationView(null);
		ReduxThemeService.Apply(deleteFiles.Resources, ReduxThemeType.ReduxDark);
		deleteFiles.ViewModel.Files.Add(new ModFileDeletionData
		{
			DisplayName = "Runtime template check",
			FilePath = @"C:\Mods\runtime-template-check.pak",
			UUID = "runtime-template-check",
			IsSelected = true
		});

		var windows = new Window[]
		{
			new ReduxSaveManagerWindow(null!, null!),
			new ReduxFileOverlapWindow(null!, Array.Empty<DivinityModData>()),
			new ReduxExportReviewWindow(null!, new ReduxExportReviewData(
				"Current",
				"Public",
				null!,
				0,
				0,
				0,
				0)),
			nexusDownloads,
			installReview,
			deleteFiles
		};

		try
		{
			foreach (var window in windows)
			{
				window.Measure(new Size(860, 700));
				window.Arrange(new Rect(0, 0, 860, 700));
				window.UpdateLayout();
			}

			var sharedWindowTemplate = (ControlTemplate)nexusDownloads.FindResource("ReduxWindowTemplate");
			var sharedWindowTemplateRoot = (FrameworkElement)sharedWindowTemplate.LoadContent();
			var resizeGlow = sharedWindowTemplateRoot.FindName("SharedResizeGlow") as Border;
			if (resizeGlow == null) throw new InvalidOperationException("The shared Redux window template did not create its resize feedback surface.");
			if (!ReduxWindowBehavior.SupportsResizeFeedback(nexusDownloads)) throw new InvalidOperationException("A resizable Redux window was not eligible for shared resize feedback.");
			if (ReduxWindowBehavior.SupportsResizeFeedback(new Window { ResizeMode = ResizeMode.NoResize })) throw new InvalidOperationException("A non-resizable window was eligible for shared resize feedback.");
			if (!ReduxWindowBehavior.SupportsMoveFeedback(new Window { ResizeMode = ResizeMode.NoResize })) throw new InvalidOperationException("A non-resizable modal was not eligible for shared move feedback.");
			RegressionAssert.Equal(0d, resizeGlow.Opacity);
			RegressionAssert.Equal(
				new Thickness(3),
				(Thickness)nexusDownloads.FindResource("Redux.WindowInteraction.BorderThickness"));

			var deleteButton = (Button)deleteFiles.FindName("DeleteActionButton");
			var deleteIcon = (ReduxIcon)deleteFiles.FindName("DeleteActionIcon");
			RegressionAssert.True(deleteButton.IsEnabled);
			RegressionAssert.Equal(
				((SolidColorBrush)deleteFiles.FindResource("ReduxErrorBrush")).Color,
				((SolidColorBrush)deleteIcon.Foreground).Color);

			var installAllButton = (Button)nexusDownloads.FindName("InstallAllButton");
			var installAllIcon = (ReduxIcon)nexusDownloads.FindName("InstallAllIcon");
			var clearArchivesButton = (Button)nexusDownloads.FindName("ClearArchivesButton");
			var clearArchivesIcon = (ReduxIcon)nexusDownloads.FindName("ClearArchivesIcon");
			RegressionAssert.Equal(
				((SolidColorBrush)nexusDownloads.FindResource("ReduxSuccessBrush")).Color,
				((SolidColorBrush)installAllButton.Foreground).Color);
			RegressionAssert.Equal(
				((SolidColorBrush)installAllButton.Foreground).Color,
				((SolidColorBrush)installAllIcon.Foreground).Color);
			RegressionAssert.Equal(
				((SolidColorBrush)nexusDownloads.FindResource("ReduxWarningBrush")).Color,
				((SolidColorBrush)clearArchivesButton.Foreground).Color);
			RegressionAssert.Equal(
				((SolidColorBrush)clearArchivesButton.Foreground).Color,
				((SolidColorBrush)clearArchivesIcon.Foreground).Color);
			installAllButton.IsEnabled = false;
			RegressionAssert.Equal(
				((SolidColorBrush)nexusDownloads.FindResource("ReduxSuccessBrush")).Color,
				((SolidColorBrush)installAllIcon.Foreground).Color);
		}
		finally
		{
			foreach (var window in windows)
			{
				window.Close();
			}
		}
	}

	public void KeyboardShortcutGroupsAvoidVirtualizedContainerRecycling()
	{
		var window = new SettingsWindow();
		try
		{
			var list = (ListView)window.FindName("KeybindingsListView");
			RegressionAssert.False(VirtualizingPanel.GetIsVirtualizing(list));
			RegressionAssert.False(VirtualizingPanel.GetIsVirtualizingWhenGrouping(list));
		}
		finally
		{
			window.Close();
		}
	}

	public void ThemeCyclingIncludesValidCustomThemesInSavedOrder()
	{
		var settings = new DivinityModManagerSettings
		{
			ColorTheme = ReduxThemeType.Parchment
		};
		var first = ReduxThemeService.CreateFromBase("First", ReduxThemeType.ReduxDark);
		var invalid = ReduxThemeService.CreateFromBase("Invalid", ReduxThemeType.ReduxLight);
		invalid.Name = String.Empty;
		var second = ReduxThemeService.CreateFromBase("Second", ReduxThemeType.ReduxLight);
		settings.CustomThemes.Add(first);
		settings.CustomThemes.Add(invalid);
		settings.CustomThemes.Add(second);

		ReduxThemeService.CycleTheme(settings);
		RegressionAssert.Equal(first.Id, settings.ActiveCustomThemeId);
		ReduxThemeService.CycleTheme(settings);
		RegressionAssert.Equal(second.Id, settings.ActiveCustomThemeId);
		ReduxThemeService.CycleTheme(settings);
		RegressionAssert.Equal(String.Empty, settings.ActiveCustomThemeId);
		RegressionAssert.Equal(ReduxThemeType.ReduxDark, settings.ColorTheme);
	}
}
