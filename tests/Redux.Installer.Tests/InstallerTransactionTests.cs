using ReduxInstaller.Models;
using ReduxInstaller.Services;

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Redux.Installer.Tests;

internal sealed class InstallerTransactionTests
{
	private static readonly string[] ReleaseFiles =
	{
		"Redux.exe", "Redux.dll",
		"Updater/ReduxUpdater.exe", "Updater/ReduxUpdater.dll",
		"Updater/ReduxUpdater.deps.json", "Updater/ReduxUpdater.runtimeconfig.json"
	};

	public void FreshInstallCommitsReviewedFilesAndAnIndependentUninstaller()
	{
		var root = TemporaryDirectory();
		try
		{
			var package = PreparedPackage(Path.Combine(root, "package"));
			var setup = Path.Combine(root, "Setup.exe");
			File.WriteAllText(setup, "setup fixture");
			var destination = Path.Combine(root, "installed", "Redux");
			var integration = new FakeIntegration();
			var service = new InstallerInstallService(integration, InstalledRuntime);

			var result = service.Install(new FreshInstallRequest
			{
				Package = package,
				DestinationDirectory = destination,
				SetupExecutablePath = setup,
				CreateDesktopShortcut = true
			});

			RegressionAssert.Equal(Path.Combine(destination, "Redux.exe"), result.ApplicationPath);
			RegressionAssert.True(File.Exists(result.ApplicationPath));
			RegressionAssert.True(File.Exists(Path.Combine(destination, InstallerInstallService.UninstallerFileName)));
			RegressionAssert.True(integration.Registered);
			RegressionAssert.True(integration.DesktopShortcut);
		}
		finally { Directory.Delete(root, true); }
	}

	public void IntegrationFailureRollsBackTheFreshApplicationDirectory()
	{
		var root = TemporaryDirectory();
		try
		{
			var package = PreparedPackage(Path.Combine(root, "package"));
			var setup = Path.Combine(root, "Setup.exe");
			File.WriteAllText(setup, "setup fixture");
			var destination = Path.Combine(root, "destination");
			Directory.CreateDirectory(destination);
			var integration = new FakeIntegration { FailRegistration = true };
			var service = new InstallerInstallService(integration, InstalledRuntime);

			RegressionAssert.Throws<InvalidOperationException>(() => service.Install(new FreshInstallRequest
			{
				Package = package,
				DestinationDirectory = destination,
				SetupExecutablePath = setup
			}));

			RegressionAssert.True(Directory.Exists(destination));
			RegressionAssert.False(Directory.EnumerateFileSystemEntries(destination).GetEnumerator().MoveNext());
			RegressionAssert.True(File.Exists(setup));
			RegressionAssert.True(integration.Removed);
		}
		finally { Directory.Delete(root, true); }
	}

	public void UninstallRemovesOnlyReleaseFilesAndPreservesUserContent()
	{
		var root = TemporaryDirectory();
		try
		{
			var package = PreparedPackage(Path.Combine(root, "package"));
			var setup = Path.Combine(root, "Setup.exe");
			File.WriteAllText(setup, "setup fixture");
			var destination = Path.Combine(root, "Redux");
			var integration = new FakeIntegration();
			new InstallerInstallService(integration, InstalledRuntime).Install(new FreshInstallRequest
			{
				Package = package,
				DestinationDirectory = destination,
				SetupExecutablePath = setup
			});
			var userFile = Path.Combine(destination, "Data", "Settings.json");
			Directory.CreateDirectory(Path.GetDirectoryName(userFile));
			File.WriteAllText(userFile, "keep me");

			var uninstall = new InstallerUninstallService(integration, _ => true).Uninstall(
				destination, Path.Combine(destination, InstallerInstallService.UninstallerFileName));

			RegressionAssert.True(uninstall.PreservedUserContent);
			RegressionAssert.Equal("keep me", File.ReadAllText(userFile));
			RegressionAssert.False(File.Exists(Path.Combine(destination, "Redux.exe")));
			RegressionAssert.True(integration.Removed);
			RegressionAssert.Equal(ReleaseFiles.Length + 1, uninstall.RemovedApplicationFiles);
		}
		finally { Directory.Delete(root, true); }
	}

	public void CleanUninstallDoesNotMistakeTheRunningUninstallerForUserContent()
	{
		var root = TemporaryDirectory();
		try
		{
			var package = PreparedPackage(Path.Combine(root, "package"));
			var setup = Path.Combine(root, "Setup.exe");
			File.WriteAllText(setup, "setup fixture");
			var destination = Path.Combine(root, "Redux");
			var integration = new FakeIntegration();
			new InstallerInstallService(integration, InstalledRuntime).Install(new FreshInstallRequest
			{
				Package = package,
				DestinationDirectory = destination,
				SetupExecutablePath = setup
			});

			var uninstall = new InstallerUninstallService(integration, _ => true).Uninstall(
				destination, Path.Combine(destination, InstallerInstallService.UninstallerFileName));

			RegressionAssert.False(uninstall.PreservedUserContent);
			RegressionAssert.True(File.Exists(Path.Combine(destination, InstallerInstallService.UninstallerFileName)));
		}
		finally { Directory.Delete(root, true); }
	}

	private static DesktopRuntimeStatus InstalledRuntime() =>
		new DesktopRuntimeStatus { IsInstalled = true, LatestVersion = "8.0.31" };

	private static PreparedInstallerPackage PreparedPackage(string root)
	{
		Directory.CreateDirectory(root);
		var inventory = new List<string> { InstallerPackageService.InventoryFileName };
		foreach (var relative in ReleaseFiles)
		{
			var file = Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar));
			Directory.CreateDirectory(Path.GetDirectoryName(file));
			File.WriteAllText(file, "fixture " + relative);
			inventory.Add(relative);
		}
		File.WriteAllText(Path.Combine(root, InstallerPackageService.InventoryFileName),
			"{\"schemaVersion\":1,\"files\":[\"" + String.Join("\",\"", inventory) + "\"]}", Encoding.UTF8);
		return new PreparedInstallerPackage
		{
			PayloadDirectory = root,
			Manifest = new InstallerReleaseManifest { DisplayVersion = "0.1.0-alpha.15" }
		};
	}

	private static string TemporaryDirectory()
	{
		var path = Path.Combine(Path.GetTempPath(), "redux-installer-transaction-" + Guid.NewGuid().ToString("N"));
		Directory.CreateDirectory(path);
		return path;
	}

	private sealed class FakeIntegration : IInstallerSystemIntegration
	{
		public bool FailRegistration { get; set; }
		public bool Registered { get; private set; }
		public bool Removed { get; private set; }
		public bool DesktopShortcut { get; private set; }
		public string RegisteredDirectory { get; set; } = String.Empty;
		public string GetRegisteredInstallationDirectory() => RegisteredDirectory;
		public void Register(string installationDirectory, string displayVersion, long applicationBytes, bool desktopShortcut)
		{
			Registered = true;
			DesktopShortcut = desktopShortcut;
			if (FailRegistration) throw new InvalidOperationException("fixture registration failure");
		}
		public void Remove(string installationDirectory) => Removed = true;
	}
}
