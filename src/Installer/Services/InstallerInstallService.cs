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

	public FreshInstallResult Install(FreshInstallRequest request)
	{
		if (request == null) throw new ArgumentNullException(nameof(request));
		if (request.Package == null) throw new ArgumentException("A prepared Redux package is required.", nameof(request));
		if (!File.Exists(request.SetupExecutablePath))
			throw new FileNotFoundException("The running Setup executable is unavailable.", request.SetupExecutablePath);
		var registeredInstallation = _integration.GetRegisteredInstallationDirectory();
		if (!String.IsNullOrWhiteSpace(registeredInstallation))
			throw new InvalidOperationException("Redux is already registered at '" + registeredInstallation
				+ "'. This Setup performs fresh installations only.");
		var destination = InstallDestinationService.Validate(request.DestinationDirectory, request.GameDirectory, true);
		if (!destination.IsValid) throw new InvalidOperationException(destination.Message);
		var runtime = _runtimeDetector();
		if (!runtime.IsInstalled) throw new InvalidOperationException("Install the x64 .NET 8 Desktop Runtime before installing Redux.");

		var inventory = InstallerPackageService.ReadAndValidateInventory(request.Package.PayloadDirectory, true);
		var target = destination.NormalizedPath;
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
			long applicationBytes = 0;
			foreach (var relative in inventory.Files)
			{
				var source = ContainedPath(request.Package.PayloadDirectory, relative);
				var output = ContainedPath(staging, relative);
				Directory.CreateDirectory(Path.GetDirectoryName(output));
				File.Copy(source, output, false);
				applicationBytes += new FileInfo(output).Length;
			}
			var uninstaller = Path.Combine(staging, UninstallerFileName);
			File.Copy(request.SetupExecutablePath, uninstaller, false);
			applicationBytes += new FileInfo(uninstaller).Length;

			if (destinationExisted) Directory.Delete(target);
			Directory.Move(staging, target);
			committed = true;
			integrationStarted = true;
			_integration.Register(target, request.Package.Manifest.DisplayVersion, applicationBytes, request.CreateDesktopShortcut);
			return new FreshInstallResult
			{
				DestinationDirectory = target,
				ApplicationPath = Path.Combine(target, "Redux.exe"),
				DisplayVersion = request.Package.Manifest.DisplayVersion
			};
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

	private static string ContainedPath(string root, string relative)
	{
		var rootPrefix = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
		var output = Path.GetFullPath(Path.Combine(rootPrefix,
			InstallerPackageService.NormalizeRelativePath(relative).Replace('/', Path.DirectorySeparatorChar)));
		if (!output.StartsWith(rootPrefix, StringComparison.OrdinalIgnoreCase))
			throw new InvalidDataException("An application file leaves its installation staging directory.");
		return output;
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
