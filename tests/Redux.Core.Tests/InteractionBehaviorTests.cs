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
using System.Windows.Media;

namespace Redux.Core.Tests;

public sealed class InteractionBehaviorTests
{
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
			window.Resources["ReduxErrorPillBackground"] = errorInteractionBrush;
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

			Border CreateSelectedSurface(ReduxCommandPaletteItem data)
			{
				var item = new ListBoxItem
				{
					DataContext = data,
					IsSelected = true,
					Style = (Style)window.FindResource("CommandPaletteItemStyle")
				};
				item.Resources["ReduxErrorPillBackground"] = errorInteractionBrush;
				item.ApplyTemplate();
				item.Measure(new Size(560, 60));
				item.Arrange(new Rect(0, 0, 560, 60));
				item.UpdateLayout();
				return (Border)item.Template.FindName("ContextualSelectionSurface", item);
			}

			var errorSurface = CreateSelectedSurface((ReduxCommandPaletteItem)list.Items[0]);
			if (errorSurface.Background is not SolidColorBrush errorBrush
				|| errorBrush.Color != Colors.Red
				|| errorSurface.Opacity != 1)
				throw new InvalidOperationException($"Semantic selection brush did not resolve; received {errorSurface.Background?.GetType().Name ?? "null"} " +
					$"{(errorSurface.Background as SolidColorBrush)?.Color} at opacity {errorSurface.Opacity}; " +
					$"tone {(errorSurface.DataContext as ReduxCommandPaletteItem)?.Tone}, interactions {DivinityApp.UseCategoryColorsForInteractions}.");

			var categorySurface = CreateSelectedSurface((ReduxCommandPaletteItem)list.Items[1]);
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
			installReview
		};

		try
		{
			foreach (var window in windows)
			{
				window.Measure(new Size(860, 700));
				window.Arrange(new Rect(0, 0, 860, 700));
				window.UpdateLayout();
			}
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
