using DivinityModManager.AppServices;
using DivinityModManager.Models.Updates;
using DivinityModManager.Util;
using DivinityModManager.Views;

using System.Globalization;
using System.Threading;
using System.Windows;
using System.Windows.Input;

namespace DivinityModManager.ViewModels;

public enum ReduxUpdateCheckState
{
	Idle,
	Checking,
	UpToDate,
	UpdateAvailable,
	PreparingUpdate,
	InstalledVersionIsNewer,
	Failed
}

public partial class AppUpdateWindowViewModel : ReactiveObject
{
	private readonly ReduxUpdateChannelService _updates;
	private readonly ReduxUpdatePackageService _packages;
	private readonly ReduxUpdateLaunchService _launcher;
	private int _checkInProgress;
	private string _releaseNotesUrl = DivinityApp.URL_REDUX_RELEASES;
	private ReduxUpdateDecision _availableUpdate;

	[Reactive] public bool IsVisible { get; set; }
	[Reactive] public bool IsChecking { get; set; }
	[Reactive] public bool CanConfirm { get; set; }
	[Reactive] public bool CanSkip { get; set; } = true;
	[Reactive] public string ConfirmButtonText { get; set; } = "View Release";
	[Reactive] public string SkipButtonText { get; set; } = "Close";
	[Reactive] public string UpdateDescription { get; set; } = "Check for a newer Redux public-alpha release.";
	[Reactive] public string UpdateChangelogView { get; set; } = String.Empty;
	[Reactive] public double UpdateProgress { get; set; }
	[Reactive] public bool IsProgressVisible { get; set; }
	[Reactive] public bool HasAvailableUpdate { get; set; }
	[Reactive] public ReduxUpdateCheckState CheckState { get; set; } = ReduxUpdateCheckState.Idle;

	public ICommand ConfirmCommand { get; }
	public ICommand SkipCommand { get; }

	public AppUpdateWindowViewModel(
		ReduxUpdateChannelService updates,
		ReduxUpdatePackageService packages,
		ReduxUpdateLaunchService launcher)
	{
		_updates = updates ?? throw new ArgumentNullException(nameof(updates));
		_packages = packages ?? throw new ArgumentNullException(nameof(packages));
		_launcher = launcher ?? throw new ArgumentNullException(nameof(launcher));
		var canConfirm = this.WhenAnyValue(x => x.CanConfirm);
		ConfirmCommand = ReactiveCommand.CreateFromTask(PrepareAndRestartAsync, canConfirm, RxApp.MainThreadScheduler);
		var canSkip = this.WhenAnyValue(x => x.CanSkip);
		SkipCommand = ReactiveCommand.Create(() => IsVisible = false, canSkip, RxApp.MainThreadScheduler);
	}

	public void ScheduleUpdateCheck(bool showAlerts = false) => _ = CheckForUpdatesAsync(showAlerts);

	public async Task CheckForUpdatesAsync(bool showAlerts = false, CancellationToken cancellationToken = default)
	{
		if (Interlocked.CompareExchange(ref _checkInProgress, 1, 0) != 0)
		{
			if (showAlerts) MainWindow.Self?.ViewModel?.ShowAlert("Redux is already checking for updates.", AlertType.Info, 12);
			return;
		}

		try
		{
			await RunOnMainThreadAsync(() =>
			{
				var main = MainWindow.Self?.ViewModel;
				if (main != null)
				{
					main.Settings.LastUpdateCheckAttempt = DateTimeOffset.Now.ToUnixTimeSeconds();
					main.SaveSettings();
				}
				CheckState = ReduxUpdateCheckState.Checking;
				IsChecking = true;
				CanConfirm = false;
				CanSkip = false;
				UpdateDescription = "Checking for updates...";
				UpdateChangelogView = String.Empty;
				SkipButtonText = "Close";
			});

			var decision = await _updates.CheckAsync(DivinityApp.REDUX_INTERNAL_VERSION, cancellationToken);
			await RunOnMainThreadAsync(() => ApplyDecision(decision, showAlerts));
		}
		catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
		{
			DivinityApp.Log("Redux update check was cancelled.");
			await RunOnMainThreadAsync(() => ApplyFailure("The update check was cancelled.", showAlerts));
		}
		catch (OperationCanceledException ex)
		{
			DivinityApp.Log($"Redux update check timed out:\n{ex}");
			await RunOnMainThreadAsync(() => ApplyFailure("Redux could not reach the update channel before the request timed out.", showAlerts));
		}
		catch (Exception ex)
		{
			DivinityApp.Log($"Error checking for a Redux update:\n{ex}");
			await RunOnMainThreadAsync(() => ApplyFailure("Redux could not verify the public-alpha update channel. Your current installation was not changed.", showAlerts));
		}
		finally
		{
			Interlocked.Exchange(ref _checkInProgress, 0);
			await RunOnMainThreadAsync(() =>
			{
				IsChecking = false;
				CanSkip = true;
			});
		}
	}

	private void ApplyDecision(ReduxUpdateDecision decision, bool showAlerts)
	{
		var main = MainWindow.Self?.ViewModel;
		if (main != null)
		{
			main.Settings.LastUpdateCheck = DateTimeOffset.Now.ToUnixTimeSeconds();
			main.SaveSettings();
		}

		_releaseNotesUrl = decision.Manifest.ReleaseNotesUrl;
		HasAvailableUpdate = decision.Availability == ReduxUpdateAvailability.UpdateAvailable;
		var published = decision.Manifest.PublishedAtUtc.ToLocalTime().ToString("d", CultureInfo.CurrentCulture);
		UpdateChangelogView = $"## {decision.Manifest.DisplayVersion}\n\nPublished {published}.\n\n[View the official release notes]({_releaseNotesUrl})";

		switch (decision.Availability)
		{
			case ReduxUpdateAvailability.UpdateAvailable:
				_availableUpdate = decision;
				CheckState = ReduxUpdateCheckState.UpdateAvailable;
				UpdateDescription = $"Redux {decision.Manifest.DisplayVersion} is available. You have {DivinityApp.REDUX_DISPLAY_VERSION}.";
				ConfirmButtonText = "Update & Restart";
				SkipButtonText = "Later";
				CanConfirm = true;
				IsVisible = true;

				break;
			case ReduxUpdateAvailability.InstalledVersionIsNewer:
				CheckState = ReduxUpdateCheckState.InstalledVersionIsNewer;
				UpdateDescription = $"This Redux build is newer than the published {decision.Manifest.DisplayVersion} release.";
				CanConfirm = false;
				IsVisible = showAlerts;

				break;
			default:
				CheckState = ReduxUpdateCheckState.UpToDate;
				UpdateDescription = $"Redux {DivinityApp.REDUX_DISPLAY_VERSION} is up to date.";
				CanConfirm = false;
				IsVisible = showAlerts;

				break;
		}
	}

	private void ApplyFailure(string message, bool showAlerts)
	{
		CheckState = ReduxUpdateCheckState.Failed;
		HasAvailableUpdate = false;
		UpdateDescription = message;
		UpdateChangelogView = "You can keep using Redux and try again later.";
		CanConfirm = false;
		IsVisible = showAlerts;

	}

	private async Task PrepareAndRestartAsync()
	{
		if (CheckState != ReduxUpdateCheckState.UpdateAvailable || _availableUpdate == null) return;
		try
		{
			CheckState = ReduxUpdateCheckState.PreparingUpdate;
			IsChecking = true;
			CanConfirm = false;
			CanSkip = false;
			IsProgressVisible = true;
			UpdateProgress = 0;
			var progress = new Progress<ReduxUpdateProgress>(value =>
			{
				UpdateDescription = value.Status;
				UpdateProgress = Math.Clamp(value.Fraction, 0, 1);
			});
			var prepared = await _packages.DownloadAndStageAsync(_availableUpdate, progress);
			var processPath = Environment.ProcessPath
				?? Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Redux.exe");
			var mainWindow = MainWindow.Self
				?? throw new InvalidOperationException("The Redux main window is not available for restart.");
			_launcher.Queue(prepared, Path.GetDirectoryName(processPath)!, Environment.ProcessId);
			UpdateDescription = "Redux will finish the update after it closes.";
			mainWindow.RequestExitForUpdate();
		}
		catch (Exception ex)
		{
			DivinityApp.Log($"Could not prepare Redux update:\n{ex}");
			_launcher.CancelPending();
			CheckState = ReduxUpdateCheckState.UpdateAvailable;
			UpdateDescription = "Redux could not safely prepare this update. The current installation was not changed.";
			ConfirmButtonText = "Try Again";
			CanConfirm = true;
			IsVisible = true;

		}
		finally
		{
			IsChecking = false;
			CanSkip = true;
			IsProgressVisible = false;
		}
	}

	public void NotifyUpdateExitCancelled(string reason)
	{
		if (_availableUpdate == null) return;
		CheckState = ReduxUpdateCheckState.UpdateAvailable;
		UpdateDescription = reason;
		ConfirmButtonText = "Try Again";
		SkipButtonText = "Later";
		CanConfirm = true;
		CanSkip = true;
		IsProgressVisible = false;
		IsVisible = true;
	}

	private static Task RunOnMainThreadAsync(Action action)
	{
		var dispatcher = Application.Current?.Dispatcher;
		if (dispatcher == null || dispatcher.CheckAccess())
		{
			action();
			return Task.CompletedTask;
		}
		return dispatcher.InvokeAsync(action).Task;
	}
}
