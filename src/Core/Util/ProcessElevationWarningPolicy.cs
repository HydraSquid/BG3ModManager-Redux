using DivinityModManager.Models.App;

namespace DivinityModManager.Util;

public static class ProcessElevationWarningPolicy
{
	public static bool ShouldShow(ProcessElevationInfo elevation, bool warningDisabled) =>
		elevation.State == ProcessElevationState.Elevated && !warningDisabled;

	public static bool TryMarkScheduled(ref int schedulingGate) =>
		Interlocked.CompareExchange(ref schedulingGate, 1, 0) == 0;

	public static bool TryPersistSuppression(ConfirmationSettings settings, Func<bool> saveSettings)
	{
		ArgumentNullException.ThrowIfNull(settings);
		ArgumentNullException.ThrowIfNull(saveSettings);

		var previousValue = settings.DisableAdminModeWarning;
		var saved = false;
		settings.DisableAdminModeWarning = true;
		try
		{
			saved = saveSettings();
			return saved;
		}
		finally
		{
			if (!saved) settings.DisableAdminModeWarning = previousValue;
		}
	}
}
