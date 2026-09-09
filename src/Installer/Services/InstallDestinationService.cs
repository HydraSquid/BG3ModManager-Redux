using ReduxInstaller.Models;

using System;
using System.IO;
using System.Linq;

namespace ReduxInstaller.Services;

internal static class InstallDestinationService
{
	public static InstallDestinationValidation Validate(string destination, string gameDirectory, bool probeWriteAccess)
	{
		if (String.IsNullOrWhiteSpace(destination)) return Problem(InstallDestinationProblem.Missing,
			"Choose where Redux should be installed.");
		string full;
		try { full = Path.GetFullPath(destination.Trim().Trim('"')).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar); }
		catch { return Problem(InstallDestinationProblem.Missing, "The installation path is not valid."); }

		var root = Path.GetPathRoot(full)?.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
		if (String.Equals(full, root, StringComparison.OrdinalIgnoreCase))
			return Problem(InstallDestinationProblem.FilesystemRoot, "Choose a folder instead of an entire drive.", full);
		if (IsWithin(full, gameDirectory))
			return Problem(InstallDestinationProblem.InsideGameDirectory,
				"Redux must be installed outside the Baldur's Gate 3 game directory.", full);
		if (File.Exists(Path.Combine(full, "BG3ModManager.exe")) || File.Exists(Path.Combine(full, InstallerPackageService.InventoryFileName)))
			return Problem(InstallDestinationProblem.ExistingInstallation,
				"Redux is already installed here. Use its updater or uninstall it before running fresh setup.", full);
		if (Directory.Exists(full) && Directory.EnumerateFileSystemEntries(full).Any())
			return Problem(InstallDestinationProblem.NotEmpty, "Choose an empty folder for a fresh Redux installation.", full);

		for (var current = new DirectoryInfo(FindExistingAncestor(full)); current != null; current = current.Parent)
		{
			if ((current.Attributes & FileAttributes.ReparsePoint) != 0)
				return Problem(InstallDestinationProblem.ReparsePoint,
					"Setup cannot install through a linked or redirected folder.", full);
		}
		if (probeWriteAccess && !CanWriteToParent(full))
			return Problem(InstallDestinationProblem.NotWritable,
				"Setup cannot write to this location. Choose a per-user folder or another writable path.", full);
		return new InstallDestinationValidation { Problem = InstallDestinationProblem.None, NormalizedPath = full };
	}

	private static bool CanWriteToParent(string destination)
	{
		var existing = FindExistingAncestor(destination);
		var probe = Path.Combine(existing, ".redux-setup-write-" + Guid.NewGuid().ToString("N"));
		try
		{
			using (File.Create(probe, 1, FileOptions.DeleteOnClose)) { }
			return true;
		}
		catch { return false; }
		finally { try { if (File.Exists(probe)) File.Delete(probe); } catch { } }
	}

	private static string FindExistingAncestor(string path)
	{
		var current = path;
		while (!Directory.Exists(current))
		{
			var parent = Path.GetDirectoryName(current);
			if (String.IsNullOrWhiteSpace(parent) || String.Equals(parent, current, StringComparison.OrdinalIgnoreCase)) break;
			current = parent;
		}
		return current;
	}

	private static bool IsWithin(string child, string parent)
	{
		if (String.IsNullOrWhiteSpace(parent)) return false;
		try
		{
			var parentRoot = Path.GetFullPath(parent).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
				+ Path.DirectorySeparatorChar;
			var childRoot = Path.GetFullPath(child).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
				+ Path.DirectorySeparatorChar;
			return childRoot.StartsWith(parentRoot, StringComparison.OrdinalIgnoreCase);
		}
		catch { return false; }
	}

	private static InstallDestinationValidation Problem(InstallDestinationProblem problem, string message, string path = "") =>
		new InstallDestinationValidation { Problem = problem, Message = message, NormalizedPath = path };
}
