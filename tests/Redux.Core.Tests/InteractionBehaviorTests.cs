using DivinityModManager.AppServices;
using DivinityModManager.Controls;
using DivinityModManager.Models;
using DivinityModManager.Models.App;
using DivinityModManager.Models.Modio;
using DivinityModManager.Util;
using DivinityModManager.Views;

using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace Redux.Core.Tests;

public sealed class InteractionBehaviorTests
{
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
		var window = new ReduxCommandPaletteWindow(null!, null!, null!);
		try
		{
			var list = (ListBox)window.FindName("CommandList");
			list.ItemsSource = new[]
			{
				new ReduxCommandPaletteItem(
					"Save Current Order",
					"File",
					"Save changes.",
					"Ctrl + S",
					"save",
					() => { },
					tone: ReduxCommandPaletteTone.Success)
			};
			window.Measure(new Size(620, 530));
			window.Arrange(new Rect(0, 0, 620, 530));
			window.UpdateLayout();
			RegressionAssert.Equal(1, list.Items.Count);
		}
		finally
		{
			window.Close();
		}
	}

	public void ReduxDialogTemplatesResolveCoreBindingsAtRuntime()
	{
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
				0))
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
