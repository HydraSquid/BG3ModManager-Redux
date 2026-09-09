using ReduxInstaller.Models;
using ReduxInstaller.Services;

using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

using Forms = System.Windows.Forms;

namespace ReduxInstaller;

public partial class MainWindow : Window
{
	private enum SetupView
	{
		Loading,
		Review,
		Progress,
		Complete,
		Error,
		Uninstall
	}

	private enum ErrorReturn
	{
		None,
		Reload,
		Review,
		Uninstall
	}

	private readonly bool _uninstallMode;
	private readonly CancellationTokenSource _lifetimeCancellation = new CancellationTokenSource();
	private InstallerReleaseManifest? _release;
	private DetectedInstallerPaths? _paths;
	private DesktopRuntimeStatus? _runtime;
	private string _installedApplicationPath = String.Empty;
	private bool _busy;
	private bool _closeAfterCancellation;
	private ErrorReturn _errorReturn;
	private SetupView _view;

	public MainWindow()
	{
		InitializeComponent();
		_uninstallMode = Array.Exists(Environment.GetCommandLineArgs(),
			argument => String.Equals(argument, "--uninstall", StringComparison.OrdinalIgnoreCase));
	}

	private async void Window_Loaded(object sender, RoutedEventArgs e)
	{
		if (_uninstallMode)
		{
			ShowUninstall();
			return;
		}
		await LoadInstallReviewAsync();
	}

	private void Window_Closing(object sender, CancelEventArgs e)
	{
		_closeAfterCancellation = true;
		_lifetimeCancellation.Cancel();
	}

	private async Task LoadInstallReviewAsync()
	{
		if (_busy) return;
		_busy = true;
		ShowView(SetupView.Loading);
		LoadingStatusText.Text = "Checking the official Redux release…";
		try
		{
			var existing = new WindowsInstallerSystemIntegration().GetRegisteredInstallationDirectory();
			if (!String.IsNullOrWhiteSpace(existing))
			{
				ShowError("Redux is already installed at “" + existing
					+ "”. This lightweight Setup handles fresh installations only. Use Redux’s built-in updater for new releases, or uninstall the existing copy first.", ErrorReturn.None);
				return;
			}

			_paths = InstallerPathService.Detect();
			_runtime = DesktopRuntimeService.Detect();
			using var channel = new InstallerChannelService();
			_release = await channel.FetchAsync(_lifetimeCancellation.Token);

			InstallPathTextBox.Text = _paths.RecommendedInstallDirectory;
			GamePathTextBox.Text = _paths.GameDirectory;
			LarianPathText.Text = _paths.LarianDataDirectory;
			LarianPathText.ToolTip = _paths.LarianDataDirectory;
			ModsPathText.Text = _paths.ModsDirectory;
			ModsPathText.ToolTip = _paths.ModsDirectory;
			ReleaseSummaryText.Text = _release.DisplayVersion + "  •  " + FormatBytes(_release.Artifact.SizeBytes)
				+ "  •  published " + _release.PublishedAtUtc.LocalDateTime.ToString("MMM d, yyyy");
			RefreshRuntimeStatus();
			ShowView(SetupView.Review);
		}
		catch (OperationCanceledException) when (_lifetimeCancellation.IsCancellationRequested)
		{
			if (!_closeAfterCancellation) ShowError("The release check was cancelled.", ErrorReturn.Reload);
		}
		catch (Exception ex)
		{
			ShowError(ReleaseChannelFailureMessage(ex), ErrorReturn.Reload);
		}
		finally
		{
			_busy = false;
		}
	}

	private async void InstallRuntime_Click(object sender, RoutedEventArgs e)
	{
		if (_busy) return;
		_busy = true;
		ShowView(SetupView.Progress);
		ProgressDetailText.Text = "Setup accepts only Microsoft’s signed x64 .NET 8 Desktop Runtime installer.";
		var progress = CreateProgress();
		try
		{
			using var runtimeInstaller = new RuntimeInstallerService();
			var result = await runtimeInstaller.DownloadVerifyAndInstallAsync(progress, _lifetimeCancellation.Token);
			_runtime = DesktopRuntimeService.Detect();
			RefreshRuntimeStatus();
			ShowView(SetupView.Review);
			if (result.RestartRecommended)
				MessageBox.Show(this, ".NET 8 is installed. Windows recommends restarting after Redux Setup finishes.",
					"Redux Setup", MessageBoxButton.OK, MessageBoxImage.Information);
		}
		catch (OperationCanceledException) when (_lifetimeCancellation.IsCancellationRequested)
		{
			if (!_closeAfterCancellation) ShowError("The .NET installation was cancelled.", ErrorReturn.Review);
		}
		catch (Exception ex)
		{
			ShowError("Setup could not install .NET 8 automatically. You can use the Microsoft page, then return and try again.\n\n"
				+ FriendlyMessage(ex), ErrorReturn.Review);
		}
		finally
		{
			_busy = false;
		}
	}

	private async Task InstallReduxAsync()
	{
		if (_busy || _release == null) return;
		_runtime = DesktopRuntimeService.Detect();
		RefreshRuntimeStatus();
		if (!_runtime.IsInstalled)
		{
			MessageBox.Show(this, "Install the x64 .NET 8 Desktop Runtime before installing Redux.",
				"Redux Setup", MessageBoxButton.OK, MessageBoxImage.Warning);
			return;
		}

		var gameDirectory = GamePathTextBox.Text.Trim().Trim('"');
		if (gameDirectory.Length > 0 && !InstallerPathService.IsGameDirectory(gameDirectory))
		{
			MessageBox.Show(this, "The selected game folder does not contain Baldur’s Gate 3. Correct it or leave it blank and finish setup inside Redux.",
				"Redux Setup", MessageBoxButton.OK, MessageBoxImage.Warning);
			return;
		}
		var destination = InstallDestinationService.Validate(InstallPathTextBox.Text, gameDirectory, true);
		if (!destination.IsValid)
		{
			MessageBox.Show(this, destination.Message, "Redux Setup", MessageBoxButton.OK, MessageBoxImage.Warning);
			return;
		}

		_busy = true;
		ShowView(SetupView.Progress);
		ProgressDetailText.Text = "The archive’s exact size, SHA-256 hash, safe paths, and release inventory are checked before installation.";
		try
		{
			using var packageService = new InstallerPackageService();
			using var package = await packageService.DownloadAndPrepareAsync(_release, CreateProgress(), _lifetimeCancellation.Token);
			ProgressStatusText.Text = "Installing Redux…";
			InstallProgressBar.Value = 1;
			await Dispatcher.InvokeAsync(() => { }, System.Windows.Threading.DispatcherPriority.Render);

			var result = new InstallerInstallService().Install(new FreshInstallRequest
			{
				Package = package,
				DestinationDirectory = destination.NormalizedPath,
				GameDirectory = gameDirectory,
				SetupExecutablePath = Assembly.GetExecutingAssembly().Location,
				CreateDesktopShortcut = DesktopShortcutCheckBox.IsChecked == true
			});
			_installedApplicationPath = result.ApplicationPath;
			CompleteHeadingText.Text = "Redux " + result.DisplayVersion + " is ready";
			CompleteDetailText.Text = "Installed to “" + result.DestinationDirectory
				+ "”. A Start Menu shortcut and normal Windows uninstall entry were created.";
			ShowView(SetupView.Complete);
		}
		catch (OperationCanceledException) when (_lifetimeCancellation.IsCancellationRequested)
		{
			if (!_closeAfterCancellation) ShowError("Installation was cancelled before Redux was installed.", ErrorReturn.Review);
		}
		catch (Exception ex)
		{
			ShowError("Redux was not installed. Setup rolled back the fresh installation so you can correct the issue and try again.\n\n"
				+ FriendlyMessage(ex), ErrorReturn.Review);
		}
		finally
		{
			_busy = false;
		}
	}

	private void ShowUninstall()
	{
		var executable = Assembly.GetExecutingAssembly().Location;
		var directory = Path.GetDirectoryName(executable) ?? String.Empty;
		HeadingText.Text = "BG3 Mod Manager Redux Setup";
		SubheadingText.Text = "Remove the application while preserving your content.";
		UninstallDetailText.Text = "Remove Redux from “" + directory + "”?\n\n"
			+ "Only files listed in the installed Redux release are removed. Your mods, saves, settings, downloads, archives, and any other unlisted content stay in place.";
		ShowView(SetupView.Uninstall);
	}

	private void UninstallRedux()
	{
		if (_busy) return;
		_busy = true;
		ShowView(SetupView.Progress);
		ProgressStatusText.Text = "Removing Redux application files…";
		ProgressDetailText.Text = "User content that is not part of the release inventory will remain in place.";
		InstallProgressBar.IsIndeterminate = true;
		try
		{
			if (IsReduxRunning())
				throw new InvalidOperationException("Close BG3 Mod Manager Redux before uninstalling it, then try again.");
			var executable = Assembly.GetExecutingAssembly().Location;
			var directory = Path.GetDirectoryName(executable)
				?? throw new InvalidOperationException("Setup cannot determine the Redux installation folder.");
			var result = new InstallerUninstallService().Uninstall(directory, executable);
			CompleteHeadingText.Text = "Redux was uninstalled";
			CompleteDetailText.Text = result.PreservedUserContent
				? "Redux application files were removed. Your unlisted user content remains in the installation folder."
				: "Redux application files and shortcuts were removed.";
			if (result.FilesThatCouldNotBeRemoved.Count > 0)
				CompleteDetailText.Text += " " + result.FilesThatCouldNotBeRemoved.Count
					+ " locked file(s) could not be removed; close Setup and remove them manually.";
			_installedApplicationPath = String.Empty;
			ShowView(SetupView.Complete);
		}
		catch (Exception ex)
		{
			ShowError("Setup could not safely complete the uninstall. No unlisted user content was targeted.\n\n"
				+ FriendlyMessage(ex), ErrorReturn.Uninstall);
		}
		finally
		{
			InstallProgressBar.IsIndeterminate = false;
			_busy = false;
		}
	}

	private async void Primary_Click(object sender, RoutedEventArgs e)
	{
		switch (_view)
		{
			case SetupView.Review:
				await InstallReduxAsync();
				break;
			case SetupView.Uninstall:
				UninstallRedux();
				break;
			case SetupView.Complete:
				if (!String.IsNullOrWhiteSpace(_installedApplicationPath))
				{
					try
					{
						Process.Start(new ProcessStartInfo(_installedApplicationPath) { UseShellExecute = true });
					}
					catch (Exception ex)
					{
						MessageBox.Show(this, "Redux is installed, but Windows could not launch it automatically.\n\n"
							+ FriendlyMessage(ex), "Redux Setup", MessageBoxButton.OK, MessageBoxImage.Information);
					}
				}
				Close();
				break;
			case SetupView.Error:
				if (_errorReturn == ErrorReturn.Reload) await LoadInstallReviewAsync();
				else if (_errorReturn == ErrorReturn.Review) ShowView(SetupView.Review);
				else if (_errorReturn == ErrorReturn.Uninstall) ShowView(SetupView.Uninstall);
				else Close();
				break;
		}
	}

	private void Secondary_Click(object sender, RoutedEventArgs e)
	{
		if (_view == SetupView.Complete && !String.IsNullOrWhiteSpace(_installedApplicationPath))
		{
			Close();
			return;
		}
		_closeAfterCancellation = true;
		_lifetimeCancellation.Cancel();
		Close();
	}

	private void BrowseInstallPath_Click(object sender, RoutedEventArgs e) => BrowseInto(InstallPathTextBox,
		"Choose an empty folder for BG3 Mod Manager Redux");

	private void BrowseGamePath_Click(object sender, RoutedEventArgs e) => BrowseInto(GamePathTextBox,
		"Choose the Baldur’s Gate 3 installation folder");

	private void BrowseInto(TextBox target, string description)
	{
		using var dialog = new Forms.FolderBrowserDialog
		{
			Description = description,
			ShowNewFolderButton = true,
			SelectedPath = Directory.Exists(target.Text) ? target.Text : String.Empty
		};
		if (dialog.ShowDialog() == Forms.DialogResult.OK) target.Text = dialog.SelectedPath;
	}

	private void ReleaseNotes_Click(object sender, RoutedEventArgs e)
	{
		if (_release != null) OpenOfficialPage(_release.ReleaseNotesUrl, "release notes");
	}

	private void RuntimePage_Click(object sender, RoutedEventArgs e) =>
		OpenOfficialPage(DesktopRuntimeService.RuntimeDownloadPageUrl, "Microsoft .NET download page");

	private void OpenOfficialPage(string url, string description)
	{
		try { Process.Start(new ProcessStartInfo(url) { UseShellExecute = true }); }
		catch (Exception ex)
		{
			MessageBox.Show(this, "Windows could not open the " + description + ".\n\n" + FriendlyMessage(ex),
				"Redux Setup", MessageBoxButton.OK, MessageBoxImage.Information);
		}
	}

	private Progress<InstallerProgress> CreateProgress() => new Progress<InstallerProgress>(update =>
	{
		ProgressStatusText.Text = update.Status;
		InstallProgressBar.IsIndeterminate = update.Fraction <= 0;
		if (update.Fraction > 0) InstallProgressBar.Value = Math.Max(0, Math.Min(1, update.Fraction));
	});

	private void RefreshRuntimeStatus()
	{
		_runtime = _runtime ?? DesktopRuntimeService.Detect();
		RuntimeStatusText.Text = _runtime.IsInstalled
			? ".NET " + _runtime.LatestVersion + " is installed and ready."
			: "Required before Redux can launch. Setup can download it directly from Microsoft.";
		RuntimeStatusText.Foreground = _runtime.IsInstalled
			? (System.Windows.Media.Brush)FindResource("SuccessBrush")
			: (System.Windows.Media.Brush)FindResource("WarningBrush");
		InstallRuntimeButton.Visibility = _runtime.IsInstalled ? Visibility.Collapsed : Visibility.Visible;
	}

	private void ShowError(string message, ErrorReturn errorReturn)
	{
		ErrorText.Text = message;
		_errorReturn = errorReturn;
		ShowView(SetupView.Error);
	}

	private void ShowView(SetupView view)
	{
		_view = view;
		LoadingPanel.Visibility = view == SetupView.Loading ? Visibility.Visible : Visibility.Collapsed;
		ReviewPanel.Visibility = view == SetupView.Review ? Visibility.Visible : Visibility.Collapsed;
		ProgressPanel.Visibility = view == SetupView.Progress ? Visibility.Visible : Visibility.Collapsed;
		CompletePanel.Visibility = view == SetupView.Complete ? Visibility.Visible : Visibility.Collapsed;
		ErrorPanel.Visibility = view == SetupView.Error ? Visibility.Visible : Visibility.Collapsed;
		UninstallPanel.Visibility = view == SetupView.Uninstall ? Visibility.Visible : Visibility.Collapsed;

		SecondaryButton.Visibility = Visibility.Visible;
		PrimaryButton.Visibility = Visibility.Visible;
		PrimaryButton.IsEnabled = !_busy;
		switch (view)
		{
			case SetupView.Loading:
				HeadingText.Text = "BG3 Mod Manager Redux Setup";
				SubheadingText.Text = "Preparing a verified public-alpha installation.";
				FooterText.Text = "No game files, mods, or saves will be changed.";
				PrimaryButton.Visibility = Visibility.Collapsed;
				SecondaryButton.Content = "Cancel";
				break;
			case SetupView.Review:
				HeadingText.Text = "Install BG3 Mod Manager Redux";
				SubheadingText.Text = "Review the release, prerequisite, and paths before downloading.";
				FooterText.Text = "Fresh install only • user content is never bundled or moved";
				PrimaryButton.Content = "Install Redux";
				PrimaryButton.IsEnabled = true;
				SecondaryButton.Content = "Cancel";
				break;
			case SetupView.Progress:
				HeadingText.Text = _uninstallMode ? "Uninstalling Redux" : "Installing BG3 Mod Manager Redux";
				SubheadingText.Text = _uninstallMode ? "Removing release-owned application files." : "Downloading, verifying, and installing the selected release.";
				FooterText.Text = _uninstallMode ? "Your unlisted user content remains in place." : "Closing Setup safely cancels work that has not been committed.";
				PrimaryButton.Visibility = Visibility.Collapsed;
				SecondaryButton.Content = "Cancel";
				break;
			case SetupView.Complete:
				HeadingText.Text = _uninstallMode ? "Uninstall complete" : "Installation complete";
				SubheadingText.Text = _uninstallMode ? "Redux application files were removed." : "Redux is installed and ready to open.";
				FooterText.Text = _uninstallMode ? "Thank you for trying Redux." : "You can also launch Redux from the Start Menu.";
				PrimaryButton.Content = _uninstallMode ? "Close" : "Launch Redux";
				PrimaryButton.IsEnabled = true;
				SecondaryButton.Visibility = _uninstallMode ? Visibility.Collapsed : Visibility.Visible;
				SecondaryButton.Content = "Close";
				break;
			case SetupView.Error:
				HeadingText.Text = "Redux Setup";
				SubheadingText.Text = "Nothing unsafe was installed.";
				FooterText.Text = "Setup keeps temporary downloads isolated and verifies every release file.";
				PrimaryButton.Content = _errorReturn == ErrorReturn.Reload ? "Retry"
					: _errorReturn == ErrorReturn.Review ? "Back"
					: _errorReturn == ErrorReturn.Uninstall ? "Back" : "Close";
				PrimaryButton.IsEnabled = true;
				SecondaryButton.Visibility = Visibility.Collapsed;
				break;
			case SetupView.Uninstall:
				FooterText.Text = "Mods, saves, settings, downloads, and archives are not release-owned application files.";
				PrimaryButton.Content = "Uninstall Redux";
				PrimaryButton.IsEnabled = true;
				SecondaryButton.Content = "Cancel";
				break;
		}
	}

	private static string FriendlyMessage(Exception exception)
	{
		var message = exception.Message?.Trim();
		return String.IsNullOrWhiteSpace(message) ? exception.GetType().Name : message!;
	}

	private static string ReleaseChannelFailureMessage(Exception exception)
	{
		if (exception is System.Net.Http.HttpRequestException
			&& exception.Message.IndexOf("404", StringComparison.OrdinalIgnoreCase) >= 0)
			return "The public-alpha release channel is not available right now. Setup did not download or install anything. Please try again later.";
		return "Setup could not load the verified public-alpha release. Check your connection and try again.\n\n"
			+ FriendlyMessage(exception);
	}

	private static bool IsReduxRunning()
	{
		try
		{
			foreach (var process in Process.GetProcessesByName("BG3ModManager"))
			{
				using (process)
					if (!process.HasExited) return true;
			}
		}
		catch
		{
			// A later file lock still fails safely without widening uninstall ownership.
		}
		return false;
	}

	private static string FormatBytes(long value)
	{
		if (value >= 1024L * 1024L * 1024L) return (value / (1024d * 1024d * 1024d)).ToString("0.##") + " GiB";
		if (value >= 1024L * 1024L) return (value / (1024d * 1024d)).ToString("0.##") + " MiB";
		return (value / 1024d).ToString("0.##") + " KiB";
	}
}
