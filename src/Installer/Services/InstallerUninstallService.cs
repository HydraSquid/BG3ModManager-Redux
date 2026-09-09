using ReduxInstaller.Models;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;

namespace ReduxInstaller.Services;

internal sealed class InstallerUninstallService
{
	private const int MoveFileDelayUntilReboot = 0x4;
	private readonly IInstallerSystemIntegration _integration;
	private readonly Func<string, bool> _scheduleSelfDelete;

	public InstallerUninstallService(
		IInstallerSystemIntegration? integration = null,
		Func<string, bool>? scheduleSelfDelete = null)
	{
		_integration = integration ?? new WindowsInstallerSystemIntegration();
		_scheduleSelfDelete = scheduleSelfDelete ?? (path => MoveFileEx(path, null, MoveFileDelayUntilReboot));
	}

	public UninstallResult Uninstall(string installationDirectory, string runningUninstallerPath)
	{
		var root = Path.GetFullPath(installationDirectory);
		var uninstaller = Path.GetFullPath(runningUninstallerPath);
		var rootPrefix = root.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
		if (!uninstaller.StartsWith(rootPrefix, StringComparison.OrdinalIgnoreCase)
			|| !String.Equals(Path.GetFileName(uninstaller), InstallerInstallService.UninstallerFileName, StringComparison.OrdinalIgnoreCase))
			throw new InvalidOperationException("Setup cannot verify this Redux uninstaller location.");

		var inventory = InstallerPackageService.ReadAndValidateInventory(root, false);
		var failures = new List<string>();
		var removed = 0;
		foreach (var relative in inventory.Files.OrderByDescending(path => path.Count(character => character == '/')))
		{
			var file = Path.GetFullPath(Path.Combine(rootPrefix,
				InstallerPackageService.NormalizeRelativePath(relative).Replace('/', Path.DirectorySeparatorChar)));
			if (!file.StartsWith(rootPrefix, StringComparison.OrdinalIgnoreCase))
				throw new InvalidDataException("An uninstall path leaves the Redux installation.");
			try
			{
				if (File.Exists(file)) { File.Delete(file); removed++; }
				DeleteEmptyParents(Path.GetDirectoryName(file), root);
			}
			catch { failures.Add(relative); }
		}
		_integration.Remove(root);
		if (!_scheduleSelfDelete(uninstaller)) failures.Add(InstallerInstallService.UninstallerFileName);
		var remainingUserContent = Directory.Exists(root)
			&& Directory.EnumerateFileSystemEntries(root, "*", SearchOption.AllDirectories)
				.Any(path => !String.Equals(Path.GetFullPath(path), uninstaller, StringComparison.OrdinalIgnoreCase));
		return new UninstallResult
		{
			RemovedApplicationFiles = removed,
			FilesThatCouldNotBeRemoved = failures.AsReadOnly(),
			PreservedUserContent = remainingUserContent
		};
	}

	private static void DeleteEmptyParents(string? directory, string root)
	{
		while (!String.IsNullOrWhiteSpace(directory)
			&& !String.Equals(directory, root, StringComparison.OrdinalIgnoreCase)
			&& Directory.Exists(directory)
			&& !Directory.EnumerateFileSystemEntries(directory).Any())
		{
			Directory.Delete(directory);
			directory = Path.GetDirectoryName(directory);
		}
	}

	[DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
	[return: MarshalAs(UnmanagedType.Bool)]
	private static extern bool MoveFileEx(string existingFileName, string? newFileName, int flags);
}
