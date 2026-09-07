using DivinityModManager.Models;
using DivinityModManager.Util;
using DivinityModManager.ViewModels;

using System.Text;
using System.Windows;

namespace DivinityModManager.Views;

public sealed record ReduxDebugValue(string Label, string Value);

public sealed record ReduxDebugSection(
	string Title,
	string Description,
	IReadOnlyList<ReduxDebugValue> Values);

public partial class ReduxDebugInformationWindow : AdonisUI.Controls.AdonisWindow
{
	private readonly MainWindowViewModel _viewModel;

	public ReduxDebugInformationWindow(Window owner, MainWindowViewModel viewModel)
	{
		InitializeComponent();
		ReduxWindowBehavior.AttachDialogTransitions(this, 40);
		ReduxWindowBehavior.AttachRoundedCorners(this);
		if (owner?.IsLoaded == true) Owner = owner;

		_viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
		var settings = _viewModel.Settings;
		ReduxThemeService.Apply(Resources, settings.ColorTheme,
			ReduxThemeService.GetActiveTheme(settings), settings.UsesGeneratedGradients);
		RefreshSnapshot();
	}

	private static string State(bool value) => value ? "Yes" : "No";
	private static string ValueOrUnavailable(string value) =>
		String.IsNullOrWhiteSpace(value) ? "Unavailable" : value;

	private string ActiveThemeName()
	{
		var customTheme = ReduxThemeService.GetActiveTheme(_viewModel.Settings);
		return customTheme?.Name ?? _viewModel.Settings.ColorTheme switch
		{
			ReduxThemeType.ReduxDark => "Redux Dark",
			ReduxThemeType.ReduxLight => "Redux Light",
			ReduxThemeType.Parchment => "Parchment",
			_ => _viewModel.Settings.ColorTheme.ToString()
		};
	}

	private DivinityModData SelectedMod() => _viewModel.UserMods?
		.FirstOrDefault(mod => mod?.IsSelected == true && !mod.IsVisualDivider);

	private string SeparatorFor(DivinityModData mod)
	{
		if (mod == null) return "No mod selected";
		var separator = _viewModel.Settings.VisualModListDividers?
			.FirstOrDefault(item => item.IsActiveList == mod.IsActive
				&& item.MemberModUuids?.Contains(mod.UUID, StringComparer.OrdinalIgnoreCase) == true);
		return separator?.Title ?? "None";
	}

	private IReadOnlyList<ReduxDebugSection> BuildSections(bool includePrivateContext)
	{
		var settings = _viewModel.Settings;
		var profile = _viewModel.SelectedProfile;
		var order = _viewModel.SelectedModOrder;
		var selectedMod = SelectedMod();
		var sections = new List<ReduxDebugSection>
		{
			new("Session", "Current application and load-order state.",
			[
				new("Redux version", _viewModel.Version?.ToString() ?? "Unavailable"),
				new("Active mods", _viewModel.ActiveMods.Count.ToString()),
				new("Inactive mods", _viewModel.InactiveMods.Count.ToString()),
				new("Selected order", includePrivateContext ? ValueOrUnavailable(order?.Name) : State(order != null)),
				new("Working changes", State(_viewModel.HasUnsavedLoadOrderChanges)),
				new("Game order matches", State(_viewModel.IsCurrentOrderExportedToGame)),
				new("Undo available", State(_viewModel.CanUndoLoadOrderChange)),
				new("Redo available", State(_viewModel.CanRedoLoadOrderChange))
			]),
			new("Redux systems", "Feature and presentation state relevant to reproducing behavior.",
			[
				new("Mod diagnostics", State(_viewModel.Modules.ModDiagnosticsEnabled)),
				new("Load Order Advisor", State(_viewModel.Modules.LoadOrderGuidanceEnabled)),
				new("Source integrations", State(_viewModel.Modules.SourceIntegrationsEnabled)),
				new("Active diagnostic attention", ValueOrUnavailable(_viewModel.ActiveDiagnosticSummaryText)),
				new("Advisor state", ValueOrUnavailable(_viewModel.LoadOrderAdvisorStatusText)),
				new("Theme", ActiveThemeName()),
				new("Generated gradients", State(settings.UsesGeneratedGradients)),
				new("Reduced motion", State(settings.ReduceMotion)),
				new("Interface icons hidden", State(!settings.ShowCategoryIconsInPills))
			])
		};

		if (includePrivateContext)
		{
			sections.Add(new("Profile and paths", "Local context shown only inside this window.",
			[
				new("Profile", ValueOrUnavailable(profile?.Name ?? profile?.ProfileName)),
				new("Profile UUID", ValueOrUnavailable(profile?.UUID)),
				new("Game executable", ValueOrUnavailable(settings.GameExecutablePath)),
				new("Game data", ValueOrUnavailable(settings.GameDataPath)),
				new("BG3 AppData", ValueOrUnavailable(_viewModel.PathwayData.AppDataGameFolder)),
				new("Mods folder", ValueOrUnavailable(_viewModel.PathwayData.AppDataModsPath)),
				new("Profiles folder", ValueOrUnavailable(_viewModel.PathwayData.AppDataProfilesPath)),
				new("Game load-order file", ValueOrUnavailable(profile?.ModSettingsFile)),
				new("Save-games folder", profile == null ? "Unavailable" : Path.Combine(profile.Folder, "Savegames", "Story"))
			]));
		}

		sections.Add(new("Selected mod", "Technical identity and state for the currently selected mod.",
		[
			new("Name", ValueOrUnavailable(selectedMod?.DisplayName)),
			new("UUID", ValueOrUnavailable(selectedMod?.UUID)),
			new("Package", includePrivateContext ? ValueOrUnavailable(selectedMod?.FileName) : State(selectedMod != null)),
			new("Load-order state", ValueOrUnavailable(selectedMod?.LoadOrderDisplayText)),
			new("Separator", SeparatorFor(selectedMod)),
			new("Categories", selectedMod?.DisplayCategories?.Count > 0
				? String.Join(", ", selectedMod.DisplayCategories.Select(category => category.Name))
				: "None"),
			new("Source", selectedMod == null ? "Unavailable" : $"{selectedMod.Metadata.SourceLabel} — {selectedMod.Metadata.LinkStatus}"),
			new("Nexus project", selectedMod?.NexusModsData?.ModId > 0 ? selectedMod.NexusModsData.ModId.ToString() : "None"),
			new("mod.io project", selectedMod?.ModioData?.ModId > 0 ? selectedMod.ModioData.ModId.ToString() : "None"),
			new("Creator manifest", selectedMod?.CreatorManifest?.State.ToString() ?? "Unavailable"),
			new("Diagnostics", selectedMod?.HealthSnapshot?.FindingCountSummary ?? "Unavailable")
		]));
		return sections;
	}

	private void RefreshSnapshot() => SectionsList.ItemsSource = BuildSections(true);

	private void RefreshButton_Click(object sender, RoutedEventArgs e) => RefreshSnapshot();

	private void CopySummaryButton_Click(object sender, RoutedEventArgs e)
	{
		var text = new StringBuilder("BG3 Mod Manager Redux — privacy-safe debug summary");
		foreach (var section in BuildSections(false))
		{
			text.AppendLine().AppendLine().Append(section.Title);
			foreach (var value in section.Values)
				text.AppendLine().Append(value.Label).Append(": ").Append(value.Value);
		}
		Clipboard.SetText(text.ToString());
		_viewModel.ShowAlert("Copied a privacy-safe debug summary.", AlertType.Success, 8);
	}

	private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();
}
