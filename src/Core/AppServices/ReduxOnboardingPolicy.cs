using DivinityModManager.Models;

namespace DivinityModManager.AppServices;

public static class ReduxOnboardingPolicy
{
    public static IReadOnlyList<string> StarterSeparatorTitles { get; } = Array.AsReadOnly(new[] { "Foundations", "Interface", "Character Creation", "Classes & Subclasses", "Spells", "Gameplay", "Equipment", "Visuals", "Patches" });

    public static IReadOnlyList<string> MissingStarterSeparators(IEnumerable<ModListVisualDividerData> existing) =>
        StarterSeparatorTitles.Where(title => !(existing ?? Enumerable.Empty<ModListVisualDividerData>())
            .Any(item => item.IsActiveList && String.Equals(item.Title?.Trim(), title, StringComparison.OrdinalIgnoreCase))).ToArray();

	/// <summary>
	/// Keeps optional online features and load-order guidance opt-in for a user's first Redux setup.
	/// Returning users retain the choices already stored in their settings.
	/// </summary>
	public static void ApplyFirstRunDefaults(DivinityModManagerSettings settings)
	{
		if (settings == null || settings.HasSeenReduxWelcome)
		{
			return;
		}

		settings.LocalOnlyMode = true;
		settings.EnableLoadOrderAdvisor = false;
	}
}
