using Microsoft.Win32;

using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;

namespace ReduxInstaller.Services;

internal interface IInstallerSystemIntegration
{
	string GetRegisteredInstallationDirectory();
	void Register(string installationDirectory, string displayVersion, long applicationBytes, bool desktopShortcut);
	void Remove(string installationDirectory);
}

internal sealed class WindowsInstallerSystemIntegration : IInstallerSystemIntegration
{
	private const string ProductKey = @"Software\Microsoft\Windows\CurrentVersion\Uninstall\BG3ModManager-Redux";
	private const string ShortcutName = "BG3 Mod Manager Redux.lnk";

	public string GetRegisteredInstallationDirectory()
	{
		try
		{
			using var key = Registry.CurrentUser.OpenSubKey(ProductKey, writable: false);
			return key?.GetValue("InstallLocation") as string ?? String.Empty;
		}
		catch { return String.Empty; }
	}

	public void Register(string installationDirectory, string displayVersion, long applicationBytes, bool desktopShortcut)
	{
		var existing = GetRegisteredInstallationDirectory();
		if (!String.IsNullOrWhiteSpace(existing))
			throw new InvalidOperationException("Redux is already registered at '" + existing
				+ "'. Setup performs fresh installations only.");
		var executable = Path.Combine(installationDirectory, "BG3ModManager.exe");
		var uninstaller = Path.Combine(installationDirectory, InstallerInstallService.UninstallerFileName);
		var startMenuFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Programs), "BG3 Mod Manager Redux");
		Directory.CreateDirectory(startMenuFolder);
		CreateShortcut(Path.Combine(startMenuFolder, ShortcutName), executable, installationDirectory);
		if (desktopShortcut)
			CreateShortcut(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), ShortcutName),
				executable, installationDirectory);

		using var key = Registry.CurrentUser.CreateSubKey(ProductKey, writable: true)
			?? throw new InvalidOperationException("Windows could not create Redux's uninstall entry.");
		key.SetValue("DisplayName", "BG3 Mod Manager Redux", RegistryValueKind.String);
		key.SetValue("DisplayVersion", displayVersion, RegistryValueKind.String);
		key.SetValue("Publisher", "circleainn", RegistryValueKind.String);
		key.SetValue("DisplayIcon", executable, RegistryValueKind.String);
		key.SetValue("InstallLocation", installationDirectory, RegistryValueKind.String);
		key.SetValue("UninstallString", "\"" + uninstaller + "\" --uninstall", RegistryValueKind.String);
		key.SetValue("URLInfoAbout", "https://github.com/circleainn/BG3ModManager-Redux", RegistryValueKind.String);
		key.SetValue("NoModify", 1, RegistryValueKind.DWord);
		key.SetValue("NoRepair", 1, RegistryValueKind.DWord);
		key.SetValue("EstimatedSize", Math.Max(1, Math.Min(Int32.MaxValue, applicationBytes / 1024)), RegistryValueKind.DWord);
	}

	public void Remove(string installationDirectory)
	{
		var registered = GetRegisteredInstallationDirectory();
		if (!String.IsNullOrWhiteSpace(registered))
		{
			string registeredPath;
			string requestedPath;
			try
			{
				registeredPath = Path.GetFullPath(registered);
				requestedPath = Path.GetFullPath(installationDirectory);
			}
			catch
			{
				return;
			}
			if (!String.Equals(registeredPath, requestedPath, StringComparison.OrdinalIgnoreCase)) return;
		}
		try { Registry.CurrentUser.DeleteSubKeyTree(ProductKey, throwOnMissingSubKey: false); } catch { }
		var startMenuFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Programs), "BG3 Mod Manager Redux");
		TryDelete(Path.Combine(startMenuFolder, ShortcutName));
		try
		{
			if (Directory.Exists(startMenuFolder) && !Directory.EnumerateFileSystemEntries(startMenuFolder).Any())
				Directory.Delete(startMenuFolder);
		}
		catch { }
		TryDelete(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), ShortcutName));
	}

	private static void CreateShortcut(string shortcutPath, string targetPath, string workingDirectory)
	{
		var shellType = Type.GetTypeFromProgID("WScript.Shell")
			?? throw new InvalidOperationException("Windows shortcut support is unavailable.");
		object? shell = null;
		object? shortcut = null;
		try
		{
			shell = Activator.CreateInstance(shellType);
			dynamic dynamicShell = shell!;
			shortcut = dynamicShell.CreateShortcut(shortcutPath);
			dynamic dynamicShortcut = shortcut;
			dynamicShortcut.TargetPath = targetPath;
			dynamicShortcut.WorkingDirectory = workingDirectory;
			dynamicShortcut.IconLocation = targetPath + ",0";
			dynamicShortcut.Description = "Launch BG3 Mod Manager Redux";
			dynamicShortcut.Save();
		}
		finally
		{
			if (shortcut != null && Marshal.IsComObject(shortcut)) Marshal.FinalReleaseComObject(shortcut);
			if (shell != null && Marshal.IsComObject(shell)) Marshal.FinalReleaseComObject(shell);
		}
	}

	private static void TryDelete(string path)
	{
		try { if (File.Exists(path)) File.Delete(path); } catch { }
	}
}
