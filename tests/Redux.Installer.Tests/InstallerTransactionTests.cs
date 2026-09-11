using ReduxInstaller.Models;
using ReduxInstaller.Services;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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

			var result = service.Install(new InstallerInstallRequest
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

			RegressionAssert.Throws<InvalidOperationException>(() => service.Install(new InstallerInstallRequest
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

	public void ExistingInstallUpdatesOwnedFilesAndPreservesUserContent()
	{
		var root = TemporaryDirectory();
		try
		{
			var destination = Path.Combine(root, "Redux");
			var originalSetup = Path.Combine(root, "Setup-15.exe");
			var updateSetup = Path.Combine(root, "Setup-16-1.exe");
			File.WriteAllText(originalSetup, "original setup");
			File.WriteAllText(updateSetup, "updated setup");
			var integration = new FakeIntegration();
			var service = new InstallerInstallService(integration, InstalledRuntime);
			service.Install(new InstallerInstallRequest
			{
				Package = PreparedPackage(Path.Combine(root, "package-15"),
					ReleaseFiles.Concat(new[] { "Legacy.dll" }), "0.1.0-alpha.15"),
				DestinationDirectory = destination,
				SetupExecutablePath = originalSetup
			});
			var userFile = Path.Combine(destination, "Data", "Settings.json");
			Directory.CreateDirectory(Path.GetDirectoryName(userFile));
			File.WriteAllText(userFile, "keep me");

			var result = service.Install(new InstallerInstallRequest
			{
				Package = PreparedPackage(Path.Combine(root, "package-16-1"),
					ReleaseFiles.Concat(new[] { "New.dll" }), "0.1.0-alpha.16.1"),
				DestinationDirectory = destination,
				SetupExecutablePath = updateSetup,
				UpdateExisting = true
			});

			RegressionAssert.True(result.UpdatedExisting);
			RegressionAssert.Equal("0.1.0-alpha.16.1", integration.DisplayVersion);
			RegressionAssert.Equal("keep me", File.ReadAllText(userFile));
			RegressionAssert.False(File.Exists(Path.Combine(destination, "Legacy.dll")));
			RegressionAssert.Equal("fixture 0.1.0-alpha.16.1 New.dll",
				File.ReadAllText(Path.Combine(destination, "New.dll")));
			RegressionAssert.Equal("updated setup",
				File.ReadAllText(Path.Combine(destination, InstallerInstallService.UninstallerFileName)));
		}
		finally { Directory.Delete(root, true); }
	}

	public void FailedExistingUpdateRestoresOwnedFilesAndPreservesUserContent()
	{
		var root = TemporaryDirectory();
		try
		{
			var destination = Path.Combine(root, "Redux");
			var originalSetup = Path.Combine(root, "Setup-15.exe");
			var updateSetup = Path.Combine(root, "Setup-16-1.exe");
			File.WriteAllText(originalSetup, "original setup");
			File.WriteAllText(updateSetup, "updated setup");
			var integration = new FakeIntegration();
			var service = new InstallerInstallService(integration, InstalledRuntime);
			service.Install(new InstallerInstallRequest
			{
				Package = PreparedPackage(Path.Combine(root, "package-15"), ReleaseFiles, "0.1.0-alpha.15"),
				DestinationDirectory = destination,
				SetupExecutablePath = originalSetup
			});
			var originalRuntime = File.ReadAllText(Path.Combine(destination, "Redux.exe"));
			var originalInventory = File.ReadAllText(Path.Combine(destination, InstallerPackageService.InventoryFileName));
			var userFile = Path.Combine(destination, "Data", "Settings.json");
			Directory.CreateDirectory(Path.GetDirectoryName(userFile));
			File.WriteAllText(userFile, "keep me");
			integration.FailRegistration = true;

			RegressionAssert.Throws<InvalidOperationException>(() => service.Install(new InstallerInstallRequest
			{
				Package = PreparedPackage(Path.Combine(root, "package-16-1"),
					ReleaseFiles.Concat(new[] { "New.dll" }), "0.1.0-alpha.16.1"),
				DestinationDirectory = destination,
				SetupExecutablePath = updateSetup,
				UpdateExisting = true
			}));

			RegressionAssert.Equal(originalRuntime, File.ReadAllText(Path.Combine(destination, "Redux.exe")));
			RegressionAssert.Equal(originalInventory,
				File.ReadAllText(Path.Combine(destination, InstallerPackageService.InventoryFileName)));
			RegressionAssert.Equal("original setup",
				File.ReadAllText(Path.Combine(destination, InstallerInstallService.UninstallerFileName)));
			RegressionAssert.Equal("keep me", File.ReadAllText(userFile));
			RegressionAssert.False(File.Exists(Path.Combine(destination, "New.dll")));
		}
		finally { Directory.Delete(root, true); }
	}

	public void ExistingUpdateRefusesToReplaceAnUnownedCollision()
	{
		var root = TemporaryDirectory();
		try
		{
			var destination = Path.Combine(root, "Redux");
			var originalSetup = Path.Combine(root, "Setup-15.exe");
			var updateSetup = Path.Combine(root, "Setup-16-1.exe");
			File.WriteAllText(originalSetup, "original setup");
			File.WriteAllText(updateSetup, "updated setup");
			var integration = new FakeIntegration();
			var service = new InstallerInstallService(integration, InstalledRuntime);
			service.Install(new InstallerInstallRequest
			{
				Package = PreparedPackage(Path.Combine(root, "package-15"), ReleaseFiles, "0.1.0-alpha.15"),
				DestinationDirectory = destination,
				SetupExecutablePath = originalSetup
			});
			var collision = Path.Combine(destination, "New.dll");
			File.WriteAllText(collision, "user-owned");

			RegressionAssert.Throws<InvalidOperationException>(() => service.Install(new InstallerInstallRequest
			{
				Package = PreparedPackage(Path.Combine(root, "package-16-1"),
					ReleaseFiles.Concat(new[] { "New.dll" }), "0.1.0-alpha.16.1"),
				DestinationDirectory = destination,
				SetupExecutablePath = updateSetup,
				UpdateExisting = true
			}));

			RegressionAssert.Equal("user-owned", File.ReadAllText(collision));
			RegressionAssert.Equal("0.1.0-alpha.15", integration.DisplayVersion);
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
			new InstallerInstallService(integration, InstalledRuntime).Install(new InstallerInstallRequest
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
			new InstallerInstallService(integration, InstalledRuntime).Install(new InstallerInstallRequest
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

	private static PreparedInstallerPackage PreparedPackage(
		string root,
		IEnumerable<string>? releaseFiles = null,
		string displayVersion = "0.1.0-alpha.15")
	{
		Directory.CreateDirectory(root);
		var inventory = new List<string> { InstallerPackageService.InventoryFileName };
		foreach (var relative in releaseFiles ?? ReleaseFiles)
		{
			var file = Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar));
			Directory.CreateDirectory(Path.GetDirectoryName(file));
			File.WriteAllText(file, "fixture " + displayVersion + " " + relative);
			inventory.Add(relative);
		}
		File.WriteAllText(Path.Combine(root, InstallerPackageService.InventoryFileName),
			"{\"schemaVersion\":1,\"files\":[\"" + String.Join("\",\"", inventory) + "\"]}", Encoding.UTF8);
		return new PreparedInstallerPackage
		{
			PayloadDirectory = root,
			Manifest = new InstallerReleaseManifest { DisplayVersion = displayVersion }
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
		public string DisplayVersion { get; private set; } = String.Empty;
		public string RegisteredDirectory { get; set; } = String.Empty;
		public string GetRegisteredInstallationDirectory() => RegisteredDirectory;
		public string GetRegisteredDisplayVersion() => DisplayVersion;
		public void RegisterOrUpdate(string installationDirectory, string displayVersion, long applicationBytes, bool desktopShortcut)
		{
			Registered = true;
			DesktopShortcut = desktopShortcut;
			if (FailRegistration) throw new InvalidOperationException("fixture registration failure");
			RegisteredDirectory = installationDirectory;
			DisplayVersion = displayVersion;
		}
		public void Remove(string installationDirectory)
		{
			Removed = true;
			RegisteredDirectory = String.Empty;
		}
	}
}
