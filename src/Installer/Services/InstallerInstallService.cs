using ReduxInstaller.Models;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace ReduxInstaller.Services;

internal sealed class InstallerInstallService
{
	public const string UninstallerFileName = "ReduxUninstall.exe";
	private readonly IInstallerSystemIntegration _integration;
	private readonly Func<DesktopRuntimeStatus> _runtimeDetector;

	public InstallerInstallService(
		IInstallerSystemIntegration? integration = null,
		Func<DesktopRuntimeStatus>? runtimeDetector = null)
	{
		_integration = integration ?? new WindowsInstallerSystemIntegration();
		_runtimeDetector = runtimeDetector ?? DesktopRuntimeService.Detect;
	}

	public InstallerInstallResult Install(InstallerInstallRequest request)
	{
		if (request == null) throw new ArgumentNullException(nameof(request));
		if (request.Package == null) throw new ArgumentException("A prepared Redux package is required.", nameof(request));
		if (!File.Exists(request.SetupExecutablePath))
			throw new FileNotFoundException("The running Setup executable is unavailable.", request.SetupExecutablePath);
		var runtime = _runtimeDetector();
		if (!runtime.IsInstalled) throw new InvalidOperationException("Install the x64 .NET 8 Desktop Runtime before installing Redux.");

		var registeredInstallation = _integration.GetRegisteredInstallationDirectory();
		var destination = InstallDestinationService.Validate(request.DestinationDirectory, request.GameDirectory,
			probeWriteAccess: true, allowExistingInstallation: request.UpdateExisting);
		if (!destination.IsValid) throw new InvalidOperationException(destination.Message);
		var inventory = InstallerPackageService.ReadAndValidateInventory(request.Package.PayloadDirectory, true);

		if (request.UpdateExisting)
		{
			if (String.IsNullOrWhiteSpace(registeredInstallation)
				|| !SamePath(registeredInstallation, destination.NormalizedPath))
			{
				throw new InvalidOperationException("Setup can update only the Redux installation registered with Windows.");
			}
			if (IsWithin(request.SetupExecutablePath, destination.NormalizedPath))
				throw new InvalidOperationException("Run the downloaded Redux Setup from outside the installed application folder.");
			var installedVersion = _integration.GetRegisteredDisplayVersion();
			if (!String.IsNullOrWhiteSpace(installedVersion)
				&& InstallerManifestService.CompareDisplayVersions(installedVersion,
					request.Package.Manifest.DisplayVersion) > 0)
			{
				throw new InvalidOperationException("Setup will not replace a newer Redux installation with an older release.");
			}
			return Update(request, inventory, destination.NormalizedPath);
		}

		if (!String.IsNullOrWhiteSpace(registeredInstallation))
			throw new InvalidOperationException("Redux is already registered at '" + registeredInstallation
				+ "'. Use Setup's update mode for that installation.");
		return InstallFresh(request, inventory, destination.NormalizedPath);
	}

	private InstallerInstallResult InstallFresh(
		InstallerInstallRequest request,
		InstallerReleaseInventory inventory,
		string target)
	{
		var parent = Path.GetDirectoryName(target)
			?? throw new InvalidOperationException("The installation folder has no parent directory.");
		var parentExisted = Directory.Exists(parent);
		var destinationExisted = Directory.Exists(target);
		Directory.CreateDirectory(parent);
		var staging = Path.Combine(parent, ".redux-installing-" + Guid.NewGuid().ToString("N"));
		var committed = false;
		var integrationStarted = false;
		try
		{
			Directory.CreateDirectory(staging);
			var applicationBytes = CopyReleaseToDirectory(request, inventory, staging);
			if (destinationExisted) Directory.Delete(target);
			Directory.Move(staging, target);
			committed = true;
			integrationStarted = true;
			_integration.RegisterOrUpdate(target, request.Package.Manifest.DisplayVersion,
				applicationBytes, request.CreateDesktopShortcut);
			return Result(request, target, updatedExisting: false);
		}
		catch
		{
			if (integrationStarted) _integration.Remove(target);
			TryDeleteDirectory(staging);
			if (committed) TryDeleteDirectory(target);
			if (destinationExisted && !Directory.Exists(target)) Directory.CreateDirectory(target);
			if (!parentExisted) TryDeleteEmptyDirectory(parent);
			throw;
		}
	}

	private InstallerInstallResult Update(
		InstallerInstallRequest request,
		InstallerReleaseInventory newInventory,
		string target)
	{
		var oldInventory = InstallerPackageService.ReadAndValidateInventory(target, false);
		var oldFiles = new HashSet<string>(oldInventory.Files, StringComparer.OrdinalIgnoreCase);
		var newFiles = new HashSet<string>(newInventory.Files, StringComparer.OrdinalIgnoreCase);
		foreach (var relative in newFiles)
		{
			EnsureNoReparsePoints(target, relative);
			var output = ContainedPath(target, relative);
			if (File.Exists(output) && !oldFiles.Contains(relative))
				throw new InvalidOperationException("The update would replace an unowned file: " + relative);
		}

		var parent = Path.GetDirectoryName(target)
			?? throw new InvalidOperationException("The installation folder has no parent directory.");
		var backup = Path.Combine(parent, ".redux-update-backup-" + Guid.NewGuid().ToString("N"));
		var backupCreated = false;
		var changesStarted = false;
		try
		{
			Directory.CreateDirectory(backup);
			backupCreated = true;
			foreach (var relative in oldFiles)
			{
				EnsureNoReparsePoints(target, relative);
				var existing = ContainedPath(target, relative);
				if (!File.Exists(existing)) continue;
				var saved = ContainedPath(backup, relative);
				Directory.CreateDirectory(Path.GetDirectoryName(saved));
				File.Copy(existing, saved, false);
			}
			var installedUninstaller = Path.Combine(target, UninstallerFileName);
			if (File.Exists(installedUninstaller))
				File.Copy(installedUninstaller, Path.Combine(backup, UninstallerFileName), false);

			long applicationBytes = 0;
			changesStarted = true;
			foreach (var relative in newInventory.Files)
			{
				var source = ContainedPath(request.Package.PayloadDirectory, relative);
				var output = ContainedPath(target, relative);
				ReplaceFromSource(source, output);
				applicationBytes += new FileInfo(output).Length;
			}
			ReplaceFromSource(request.SetupExecutablePath, installedUninstaller);
			applicationBytes += new FileInfo(installedUninstaller).Length;

			foreach (var relative in oldFiles.Except(newFiles, StringComparer.OrdinalIgnoreCase))
			{
				var obsolete = ContainedPath(target, relative);
				if (File.Exists(obsolete)) File.Delete(obsolete);
				DeleteEmptyParents(Path.GetDirectoryName(obsolete), target);
			}

			_integration.RegisterOrUpdate(target, request.Package.Manifest.DisplayVersion,
				applicationBytes, request.CreateDesktopShortcut);
			TryDeleteDirectory(backup);
			return Result(request, target, updatedExisting: true);
		}
		catch (Exception installError)
		{
			if (!backupCreated) throw;
			if (!changesStarted)
			{
				TryDeleteDirectory(backup);
				throw;
			}
			try
			{
				RollbackUpdate(target, backup, oldFiles, newFiles);
				TryDeleteDirectory(backup);
			}
			catch (Exception rollbackError)
			{
				throw new InvalidOperationException(
					"Redux Setup could not fully restore the previous application files. User content was not targeted.",
					new AggregateException(installError, rollbackError));
			}
			throw;
		}
	}

	private static long CopyReleaseToDirectory(
		InstallerInstallRequest request,
		InstallerReleaseInventory inventory,
		string destination)
	{
		long applicationBytes = 0;
		foreach (var relative in inventory.Files)
		{
			var source = ContainedPath(request.Package.PayloadDirectory, relative);
			var output = ContainedPath(destination, relative);
			Directory.CreateDirectory(Path.GetDirectoryName(output));
			File.Copy(source, output, false);
			applicationBytes += new FileInfo(output).Length;
		}
		var uninstaller = Path.Combine(destination, UninstallerFileName);
		File.Copy(request.SetupExecutablePath, uninstaller, false);
		return applicationBytes + new FileInfo(uninstaller).Length;
	}

	private static void RollbackUpdate(
		string target,
		string backup,
		IEnumerable<string> oldFiles,
		IEnumerable<string> newFiles)
	{
		foreach (var relative in newFiles.Concat(new[] { UninstallerFileName }))
		{
			var output = ContainedPath(target, relative);
			if (File.Exists(output)) File.Delete(output);
		}
		foreach (var relative in oldFiles.Concat(new[] { UninstallerFileName }))
		{
			var saved = ContainedPath(backup, relative);
			if (!File.Exists(saved)) continue;
			var output = ContainedPath(target, relative);
			Directory.CreateDirectory(Path.GetDirectoryName(output));
			File.Copy(saved, output, false);
		}
	}

	private static void ReplaceFromSource(string source, string destination)
	{
		Directory.CreateDirectory(Path.GetDirectoryName(destination));
		var temporary = destination + ".redux-setup-" + Guid.NewGuid().ToString("N") + ".tmp";
		try
		{
			File.Copy(source, temporary, false);
			if (File.Exists(destination)) File.Replace(temporary, destination, null, true);
			else File.Move(temporary, destination);
		}
		finally
		{
			try { if (File.Exists(temporary)) File.Delete(temporary); } catch { }
		}
	}

	private static InstallerInstallResult Result(
		InstallerInstallRequest request,
		string target,
		bool updatedExisting) => new InstallerInstallResult
	{
		DestinationDirectory = target,
		ApplicationPath = Path.Combine(target, "Redux.exe"),
		DisplayVersion = request.Package.Manifest.DisplayVersion,
		UpdatedExisting = updatedExisting
	};

	private static string ContainedPath(string root, string relative)
	{
		var rootPrefix = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
		var output = Path.GetFullPath(Path.Combine(rootPrefix,
			InstallerPackageService.NormalizeRelativePath(relative).Replace('/', Path.DirectorySeparatorChar)));
		if (!output.StartsWith(rootPrefix, StringComparison.OrdinalIgnoreCase))
			throw new InvalidDataException("An application file leaves its installation staging directory.");
		return output;
	}

	private static void EnsureNoReparsePoints(string root, string relative)
	{
		var parts = InstallerPackageService.NormalizeRelativePath(relative).Split('/');
		var current = new DirectoryInfo(root);
		for (var index = 0; index < parts.Length - 1; index++)
		{
			current = new DirectoryInfo(Path.Combine(current.FullName, parts[index]));
			if (current.Exists && (current.Attributes & FileAttributes.ReparsePoint) != 0)
				throw new InvalidOperationException("Setup cannot update through a linked application folder.");
		}
	}

	private static bool SamePath(string left, string right)
	{
		try
		{
			return String.Equals(Path.GetFullPath(left).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
				Path.GetFullPath(right).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
				StringComparison.OrdinalIgnoreCase);
		}
		catch { return false; }
	}

	private static bool IsWithin(string child, string parent)
	{
		try
		{
			var parentPrefix = Path.GetFullPath(parent).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
				+ Path.DirectorySeparatorChar;
			return Path.GetFullPath(child).StartsWith(parentPrefix, StringComparison.OrdinalIgnoreCase);
		}
		catch { return false; }
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

	private static void TryDeleteDirectory(string path)
	{
		try { if (Directory.Exists(path)) Directory.Delete(path, true); } catch { }
	}

	private static void TryDeleteEmptyDirectory(string path)
	{
		try { if (Directory.Exists(path) && !Directory.EnumerateFileSystemEntries(path).Any()) Directory.Delete(path); } catch { }
	}
}
