using DivinityModManager.AppServices;
using DivinityModManager.Util;
using DivinityModManager.Models.NexusMods;
using DivinityModManager.Views;
using System.Diagnostics;
using System.IO;
using System.Windows;

namespace DivinityModManager.ViewModels;

public partial class MainWindowViewModel
{
	private NativeModInstaller CreateNativeModInstaller()
	{
		var executable = Environment.ExpandEnvironmentVariables(Settings.GameExecutablePath ?? String.Empty);
		var name = Path.GetFileName(executable);
		if ((!name.Equals("bg3.exe", StringComparison.OrdinalIgnoreCase) && !name.Equals("bg3_dx11.exe", StringComparison.OrdinalIgnoreCase)) || !File.Exists(executable))
			throw new InvalidOperationException("Set the game's bg3.exe or bg3_dx11.exe path in Preferences first.");
		return new NativeModInstaller(Path.GetDirectoryName(Path.GetFullPath(executable)), DivinityApp.GetAppDirectory("Data", "NativeInstalls"), GetNativeGameVersion());
	}

	public System.Version GetNativeGameVersion()
	{
		try
		{
			var info = FileVersionInfo.GetVersionInfo(Environment.ExpandEnvironmentVariables(Settings.GameExecutablePath));
			// BG3's build component exceeds a 16-bit Win32 version word. Use the full
			// version string rather than truncating it through FilePrivatePart.
			return System.Version.TryParse(info.FileVersion, out var version) ? version : null;
		}
		catch { return null; }
	}

	public NativeLoaderStatus GetNativeLoaderStatus()
	{
		try { return CreateNativeModInstaller().DetectLoader(); }
		catch { return new(false, false, "Native Mod Loader could not be checked. Verify the game path, file permissions, and native installation records in Native Mods tools."); }
	}

	public string GetNativeGameDirectory()
	{
		try { return CreateNativeModInstaller().GameBin; }
		catch { return "Not configured or unavailable"; }
	}

	public void RefreshNativeDownloadBadges()
	{
		var items = NxmDownloads?.Where(item => NativeModCatalog.Find(item.ModId) != null).ToArray() ?? [];
		if (items.Length == 0) return;
		var status = GetNativeLoaderStatus();
		foreach (var item in items)
		{
			var prefix = NativeModCatalog.Find(item.ModId).RequiresLoader ? "Requires Native Mod Loader: " : "Native Mod Loader: ";
			item.NativeRequirementWarning = !status.IsVerified;
			item.NativeRequirementLabel = status.IsVerified ? "Loader verified" : status.IsPresent ? "Loader unverified" : "Loader missing / blocked";
			item.NativeRequirementStatus = prefix + status.Description;
		}
	}

	public void ShowNativeMods(long projectId = 944)
	{
		if (_nxmShuttingDown) return;
		new NativeModsWindow(Window, this, projectId).ShowDialog();
		RefreshNativeDownloadBadges();
	}

	public async Task InstallNativeArchiveAsync(long projectId, string archivePath, Window owner)
	{
		await _nxmInstallLock.WaitAsync();
		try
		{
			if (_nxmShuttingDown) return;
			await using var installation = await CreateNativeModInstaller().StageAsync(projectId, archivePath, CancellationToken.None);
			if (ReduxMessageBox.Show(owner, installation.ReviewText + "\n\nOnly install native code from an author you trust. Keep the game closed. Install these files now?",
				"Review Native Installation", MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No) != MessageBoxResult.Yes) return;
			await installation.CommitAsync(CancellationToken.None);
			ShowAlert($"Installed {NativeModCatalog.Find(projectId).Name}. Native plugins do not appear in the PAK load order.", AlertType.Success, 20);
		}
		finally { _nxmInstallLock.Release(); RefreshNativeDownloadBadges(); }
	}

	public async Task RestoreNativeModAsync(long projectId, Window owner)
	{
		var installer = CreateNativeModInstaller();
		if (ReduxMessageBox.Show(owner, $"Restore the files that preceded Redux's installation of {NativeModCatalog.Find(projectId).Name}?\n\nGame directory: {installer.GameBin}\n\n"
			+ "Only unchanged Redux-owned files can be restored. Modified files or plugin DLLs still requiring the loader will block restoration. Keep the game closed.",
			"Restore Native Files", MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No) != MessageBoxResult.Yes) return;
		await _nxmInstallLock.WaitAsync();
		try
		{
			if (_nxmShuttingDown) return;
			await installer.RestoreAsync(projectId, CancellationToken.None);
			foreach (var item in NxmDownloads?.Where(item => item.ModId == projectId && item.State == NxmDownloadState.Installed).ToArray() ?? [])
				await _nxmDownloadManager.SetStateAsync(item.Id, NxmDownloadState.NeedsReview);
			ShowAlert("Previous native files restored. Other native plugins and PAK load orders were left alone.", AlertType.Success, 20);
		}
		finally { _nxmInstallLock.Release(); RefreshNativeDownloadBadges(); }
	}

	private async Task InstallNativeDownloadCoreAsync(NxmDownloadItem item, string archivePath)
	{
		var committed = false;
		try
		{
			await using var installation = await CreateNativeModInstaller().StageAsync(item.ModId, archivePath, CancellationToken.None);
			if (ReduxMessageBox.Show(Window, installation.ReviewText + "\n\nThis is native executable code, not a PAK mod. Keep the game closed. Install now?",
				"Review Native Installation", MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No) != MessageBoxResult.Yes) return;
			await _nxmDownloadManager.SetStateAsync(item.Id, NxmDownloadState.Installing);
			await installation.CommitAsync(CancellationToken.None);
			committed = true;
			await _nxmDownloadManager.SetStateAsync(item.Id, NxmDownloadState.Installed);
			ShowAlert($"Installed {NativeModCatalog.Find(item.ModId).Name} into the game directory. No load-order export is needed.", AlertType.Success, 20);
		}
		catch (Exception ex)
		{
			var description = committed ? "Native files were installed, but the download queue could not record completion. Check Native Mods tools before retrying."
				: "Native installation did not complete. Check Native Mods tools for loader, game-version, conflict, or recovery requirements.";
			if (!committed) await _nxmDownloadManager.SetStateAsync(item.Id, NxmDownloadState.InstallFailed,
				ex is NativeModRecoveryException ? "rollback-failed" : "native-install", description);
			ReduxMessageBox.Show(Window, description + "\n\n" + NativeInstallError(ex), "Native Installation", MessageBoxButton.OK, MessageBoxImage.Warning);
		}
		finally { RefreshNativeDownloadBadges(); }
	}

	public static string NativeInstallError(Exception ex) => ex is InvalidOperationException or InvalidDataException or NativeModRecoveryException
		? ex.Message
		: "A file operation failed. Check permissions and available disk space. If recovery was interrupted, do not launch the game until the native installer journals and backups have been reviewed.";
}
