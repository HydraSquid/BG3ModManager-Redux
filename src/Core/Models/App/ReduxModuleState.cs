namespace DivinityModManager.Models.App;

/// <summary>
/// Central runtime contract for optional Redux modules. Core package discovery,
/// load-order persistence, import/export, LSLib, and file operations must not
/// depend on any property in this class.
/// </summary>
public sealed class ReduxModuleState : ReactiveObject, IDisposable
{
	private readonly CompositeDisposable _subscriptions = new();

	[Reactive] public bool SourceIntegrationsEnabled { get; private set; }
	[Reactive] public bool ModDiagnosticsEnabled { get; private set; } = true;
	[Reactive] public bool LoadOrderGuidanceEnabled { get; private set; }

	public ReduxModuleState(DivinityModManagerSettings settings)
	{
		if (settings == null) throw new ArgumentNullException(nameof(settings));

		settings.WhenAnyValue(x => x.LocalOnlyMode)
			.Select(localOnly => !localOnly)
			.DistinctUntilChanged()
			.BindTo(this, x => x.SourceIntegrationsEnabled)
			.DisposeWith(_subscriptions);

		settings.WhenAnyValue(x => x.EnableLoadOrderAdvisor)
			.DistinctUntilChanged()
			.BindTo(this, x => x.LoadOrderGuidanceEnabled)
			.DisposeWith(_subscriptions);
	}

	public void Dispose()
	{
		_subscriptions.Dispose();
	}
}
