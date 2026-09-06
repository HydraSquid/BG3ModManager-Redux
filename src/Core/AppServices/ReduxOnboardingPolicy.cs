using DivinityModManager.Models;

namespace DivinityModManager.AppServices;

public static class ReduxOnboardingPolicy
{
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
