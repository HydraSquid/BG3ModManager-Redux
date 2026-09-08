using System;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Media;

using DivinityModManager.AppServices;
using DivinityModManager.Models;
using DivinityModManager.Models.App;
using DivinityModManager.ModUpdater.Cache;
using DivinityModManager.Util;

namespace Redux.Core.Tests;

internal sealed class ReduxModuleStateTests
{
	public void DefaultsKeepModDiagnosticsOnAndGuidanceOptIn()
	{
		var settings = new DivinityModManagerSettings();
		using var modules = new ReduxModuleState(settings);

		RegressionAssert.True(modules.SourceIntegrationsEnabled);
		RegressionAssert.True(modules.ModDiagnosticsEnabled);
		RegressionAssert.False(modules.LoadOrderGuidanceEnabled);
		RegressionAssert.False(settings.RetainInstalledPackageArchives);
		RegressionAssert.Equal(10, settings.RetainedPackageArchiveQuotaGb);
	}

	public void FirstRunOnboardingStartsWithIntegrationsAndGuidanceOff()
	{
		var settings = new DivinityModManagerSettings
		{
			LocalOnlyMode = false,
			EnableLoadOrderAdvisor = true,
			HasSeenReduxWelcome = false
		};

		ReduxOnboardingPolicy.ApplyFirstRunDefaults(settings);

		RegressionAssert.True(settings.LocalOnlyMode);
		RegressionAssert.True(settings.EnableModHealth);
		RegressionAssert.False(settings.EnableLoadOrderAdvisor);
	}

	public void ReturningUsersKeepTheirOptionalFeatureChoices()
	{
		var settings = new DivinityModManagerSettings
		{
			LocalOnlyMode = false,
			EnableLoadOrderAdvisor = true,
			HasSeenReduxWelcome = true
		};

		ReduxOnboardingPolicy.ApplyFirstRunDefaults(settings);

		RegressionAssert.False(settings.LocalOnlyMode);
		RegressionAssert.True(settings.EnableModHealth);
		RegressionAssert.True(settings.EnableLoadOrderAdvisor);
	}

	public void LocalOnlyModeChangesOnlySourceIntegrations()
	{
		var settings = new DivinityModManagerSettings
		{
			EnableLoadOrderAdvisor = true
		};
		using var modules = new ReduxModuleState(settings);

		settings.LocalOnlyMode = true;

		RegressionAssert.False(modules.SourceIntegrationsEnabled);
		RegressionAssert.True(modules.ModDiagnosticsEnabled);
		RegressionAssert.True(modules.LoadOrderGuidanceEnabled);

		settings.LocalOnlyMode = false;

		RegressionAssert.True(modules.SourceIntegrationsEnabled);
		RegressionAssert.True(modules.ModDiagnosticsEnabled);
		RegressionAssert.True(modules.LoadOrderGuidanceEnabled);
	}

	public void CategoryInteractionSettingSynchronizesLegacyPresentationFlags()
	{
		var settings = new DivinityModManagerSettings
		{
			UseCategoryColorsForHover = false,
			UseCategoryColorsForSidebarSelection = true
		};

		RegressionAssert.True(settings.UseCategoryColorsForInteractions);

		settings.UseCategoryColorsForInteractions = false;
		RegressionAssert.False(settings.UseCategoryColorsForHover);
		RegressionAssert.False(settings.UseCategoryColorsForSidebarSelection);

		settings.UseCategoryColorsForInteractions = true;
		RegressionAssert.True(settings.UseCategoryColorsForHover);
		RegressionAssert.True(settings.UseCategoryColorsForSidebarSelection);
	}

	public void IconsOnlySettingSynchronizesLegacySourceFlag()
	{
		var settings = new DivinityModManagerSettings();

		settings.UseIconsOnly = true;
		RegressionAssert.True(settings.UseSourceIconsOnly);

		settings.UseSourceIconsOnly = false;
		RegressionAssert.False(settings.UseIconsOnly);
	}

	public void CustomThemeClonePreservesUnifiedPresentationSettings()
	{
		var theme = new ReduxCustomTheme
		{
			UseCategoryColorsForHover = false,
			UseCategoryColorsForSidebarSelection = true,
			ShowCategoryIconsInPills = true,
			UseIconsOnly = true,
			UsesGeneratedGradients = false
		};

		var clone = theme.Clone();

		RegressionAssert.True(clone.UseCategoryColorsForInteractions);
		RegressionAssert.True(clone.UseCategoryColorsForHover);
		RegressionAssert.True(clone.UseCategoryColorsForSidebarSelection);
		RegressionAssert.True(clone.UseIconsOnly);
		RegressionAssert.True(clone.UseSourceIconsOnly);
		RegressionAssert.False(clone.UsesGeneratedGradients);
	}

	public void CustomThemePreviewRegeneratesEverySemanticPillGradient()
	{
		var theme = ReduxThemeService.CreateFromBase("Gradient test", ReduxThemeType.ReduxDark);
		theme.AccentColor = "#123456";
		theme.SuccessColor = "#238A57";
		theme.WarningColor = "#C07819";
		theme.ErrorColor = "#D23A4E";
		theme.InfoColor = "#367BC0";

		var resources = new ResourceDictionary();
		foreach (var key in new[]
		{
			"ReduxAccentPillBackground", "ReduxSelectionPillBackground", "ReduxSuccessPillBackground",
			"ReduxWarningPillBackground", "ReduxErrorPillBackground", "ReduxInfoPillBackground",
			"ReduxPrimaryActionBackgroundBrush", "ReduxDestructiveActionBackgroundBrush",
			"ReduxDestructiveActionForegroundBrush"
		})
		{
			resources[key] = Brushes.Transparent;
		}

		ReduxThemeService.PreviewColors(resources, theme);

		AssertPillColor(resources, "ReduxAccentPillBackground", Color.FromRgb(0x12, 0x34, 0x56));
		AssertPillColor(resources, "ReduxSelectionPillBackground", Color.FromRgb(0x15, 0x1E, 0x30));
		AssertPillColor(resources, "ReduxSuccessPillBackground", Color.FromRgb(0x23, 0x8A, 0x57));
		AssertPillColor(resources, "ReduxWarningPillBackground", Color.FromRgb(0xC0, 0x78, 0x19));
		AssertPillColor(resources, "ReduxErrorPillBackground", Color.FromRgb(0xD2, 0x3A, 0x4E));
		AssertPillColor(resources, "ReduxInfoPillBackground", Color.FromRgb(0x36, 0x7B, 0xC0));
	}

	public void CustomThemePreviewReusesUnchangedSemanticBrushes()
	{
		var theme = ReduxThemeService.CreateFromBase("Incremental preview", ReduxThemeType.ReduxDark);
		var resources = new ResourceDictionary();
		ReduxThemeService.PreviewColors(resources, theme);

		var success = resources["ReduxSuccessPillBackground"];
		var warning = resources["ReduxWarningPillBackground"];
		var error = resources["ReduxErrorPillBackground"];
		var info = resources["ReduxInfoPillBackground"];
		var accent = resources["ReduxAccentPillBackground"];

		theme.AccentColor = "#7654D8";
		ReduxThemeService.PreviewColors(resources, theme);

		RegressionAssert.True(ReferenceEquals(success, resources["ReduxSuccessPillBackground"]));
		RegressionAssert.True(ReferenceEquals(warning, resources["ReduxWarningPillBackground"]));
		RegressionAssert.True(ReferenceEquals(error, resources["ReduxErrorPillBackground"]));
		RegressionAssert.True(ReferenceEquals(info, resources["ReduxInfoPillBackground"]));
		RegressionAssert.False(ReferenceEquals(accent, resources["ReduxAccentPillBackground"]));
	}

	public void RepeatedThemeApplicationReusesTheLoadedColorScheme()
	{
		var resources = new ResourceDictionary();
		ReduxThemeService.Apply(resources, ReduxThemeType.ReduxDark);
		var merged = resources.MergedDictionaries.ToArray();

		ReduxThemeService.Apply(resources, ReduxThemeType.ReduxDark);

		RegressionAssert.Equal(merged.Length, resources.MergedDictionaries.Count);
		if (!merged.Zip(resources.MergedDictionaries).All(pair => ReferenceEquals(pair.First, pair.Second)))
			throw new InvalidOperationException("A repeated theme application reloaded the color-scheme dictionary.");
	}

	public void CustomThemeBackgroundEditsPreserveUntouchedBaseRoles()
	{
		var theme = ReduxThemeService.CreateFromBase("Background test", ReduxThemeType.ReduxDark);
		theme.BackgroundColor = "#0C0B10";
		var resources = new ResourceDictionary();

		ReduxThemeService.PreviewColors(resources, theme);

		AssertResourceColor(resources, "ReduxWindowColor", Color.FromRgb(0x0C, 0x0B, 0x10));
		AssertResourceColor(resources, "ReduxSurfaceElevatedColor", Color.FromRgb(0x1C, 0x16, 0x23));
		AssertResourceColor(resources, "ReduxTextPrimaryColor", Color.FromRgb(0xF2, 0xED, 0xF7));
		AssertResourceColor(resources, "ReduxTextSecondaryColor", Color.FromRgb(0xC8, 0xBD, 0xD4));
		AssertResourceColor(resources, "ReduxTextMutedColor", Color.FromRgb(0xA0, 0x94, 0xAE));
	}

	public void GeneratedActionGradientsFollowThemeDefaultsAndCustomChoice()
	{
		var dark = ReduxThemeService.CreateFromBase("Dark", ReduxThemeType.ReduxDark);
		var light = ReduxThemeService.CreateFromBase("Light", ReduxThemeType.ReduxLight);
		var parchment = ReduxThemeService.CreateFromBase("Parchment", ReduxThemeType.Parchment);
		RegressionAssert.True(dark.UsesGeneratedGradients);
		RegressionAssert.True(light.UsesGeneratedGradients);
		RegressionAssert.False(parchment.UsesGeneratedGradients);
		RegressionAssert.True(new ReduxCustomTheme { BaseTheme = ReduxThemeType.ReduxDark }.UsesGeneratedGradients);
		RegressionAssert.False(new ReduxCustomTheme { BaseTheme = ReduxThemeType.Parchment }.UsesGeneratedGradients);
		var builtInSettings = new DivinityModManagerSettings { ColorTheme = ReduxThemeType.ReduxDark };
		RegressionAssert.True(builtInSettings.UsesGeneratedGradients);
		builtInSettings.ColorTheme = ReduxThemeType.Parchment;
		RegressionAssert.False(builtInSettings.UsesGeneratedGradients);
		builtInSettings.UsesGeneratedGradients = true;
		RegressionAssert.True(builtInSettings.UsesGeneratedGradients);

		var resources = new ResourceDictionary
		{
			["ReduxPrimaryActionBackgroundBrush"] = Brushes.Transparent,
			["ReduxDestructiveActionBackgroundBrush"] = Brushes.Transparent,
			["ReduxDestructiveActionForegroundBrush"] = Brushes.Transparent
		};
		dark.UsesGeneratedGradients = false;
		ReduxThemeService.PreviewColors(resources, dark);
		RegressionAssert.True(resources["ReduxPrimaryActionBackgroundBrush"] is SolidColorBrush);
		RegressionAssert.True(resources["ReduxDestructiveActionBackgroundBrush"] is SolidColorBrush);

		dark.UsesGeneratedGradients = true;
		ReduxThemeService.PreviewColors(resources, dark);
		RegressionAssert.True(resources["ReduxPrimaryActionBackgroundBrush"] is LinearGradientBrush);
		RegressionAssert.True(resources["ReduxDestructiveActionBackgroundBrush"] is LinearGradientBrush);
	}

	private static void AssertPillColor(ResourceDictionary resources, string key, Color expected)
	{
		var brush = resources[key] as LinearGradientBrush;
		RegressionAssert.True(brush != null);
		var actual = brush!.GradientStops[0].Color;
		RegressionAssert.Equal(expected.R, actual.R);
		RegressionAssert.Equal(expected.G, actual.G);
		RegressionAssert.Equal(expected.B, actual.B);
	}

	private static void AssertResourceColor(ResourceDictionary resources, string key, Color expected)
	{
		RegressionAssert.True(resources[key] is Color);
		var actual = (Color)resources[key];
		RegressionAssert.Equal(expected.R, actual.R);
		RegressionAssert.Equal(expected.G, actual.G);
		RegressionAssert.Equal(expected.B, actual.B);
	}

	public void LoadOrderGuidanceFollowsItsOwnPreference()
	{
		var settings = new DivinityModManagerSettings
		{
			EnableLoadOrderAdvisor = true
		};
		using var modules = new ReduxModuleState(settings);

		RegressionAssert.True(modules.ModDiagnosticsEnabled);
		RegressionAssert.True(modules.LoadOrderGuidanceEnabled);

		settings.EnableLoadOrderAdvisor = false;

		RegressionAssert.True(modules.ModDiagnosticsEnabled);
		RegressionAssert.False(modules.LoadOrderGuidanceEnabled);
	}

	public void DisposedModuleStateStopsTrackingSettings()
	{
		var settings = new DivinityModManagerSettings
		{
			EnableLoadOrderAdvisor = false,
			LocalOnlyMode = false
		};
		var modules = new ReduxModuleState(settings);

		modules.Dispose();
		settings.LocalOnlyMode = true;
		settings.EnableLoadOrderAdvisor = true;

		RegressionAssert.True(modules.SourceIntegrationsEnabled);
		RegressionAssert.True(modules.ModDiagnosticsEnabled);
		RegressionAssert.False(modules.LoadOrderGuidanceEnabled);
	}

	public void DisabledNexusProviderCannotInitializeItsClient()
	{
		NexusModsDataLoader.Dispose();
		var provider = new NexusModsCacheHandler
		{
			IsEnabled = false,
			APIKey = "unused-test-key",
			AppName = "Redux regression tests",
			AppVersion = "0"
		};

		var changed = provider.Update(Array.Empty<DivinityModData>(), CancellationToken.None)
			.GetAwaiter()
			.GetResult();

		RegressionAssert.False(changed);
		RegressionAssert.False(NexusModsDataLoader.IsInitialized);
	}
}
