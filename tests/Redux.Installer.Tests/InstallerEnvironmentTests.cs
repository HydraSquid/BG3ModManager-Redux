using ReduxInstaller.Models;
using ReduxInstaller.Services;

using System;
using System.IO;

namespace Redux.Installer.Tests;

internal sealed class InstallerEnvironmentTests
{
	public void DesktopRuntimeDetectionRequiresTheX64VersionEightFamily()
	{
		var missing = DesktopRuntimeService.EvaluateVersions(new[] { new Version(7, 0, 20), new Version(9, 0, 1) });
		RegressionAssert.False(missing.IsInstalled);
		var installed = DesktopRuntimeService.EvaluateVersions(new[] { new Version(8, 0, 8), new Version(8, 0, 22) });
		RegressionAssert.True(installed.IsInstalled);
		RegressionAssert.Equal("8.0.22", installed.LatestVersion);
	}

	public void RecommendedPerUserDestinationIsWritableAndOutsideTheGame()
	{
		var detected = InstallerPathService.Detect();
		var result = InstallDestinationService.Validate(detected.RecommendedInstallDirectory,
			detected.GameDirectory, probeWriteAccess: true);
		RegressionAssert.True(result.IsValid || result.Problem == InstallDestinationProblem.ExistingInstallation,
			"The recommended destination should be writable or already contain Redux.");
	}

	public void GameDirectoryAndNonemptyFoldersAreRejected()
	{
		var root = Path.Combine(Path.GetTempPath(), "redux-installer-destination-" + Guid.NewGuid().ToString("N"));
		try
		{
			var game = Path.Combine(root, "game");
			Directory.CreateDirectory(Path.Combine(game, "bin"));
			File.WriteAllText(Path.Combine(game, "bin", "bg3_dx11.exe"), "fixture");
			var insideGame = InstallDestinationService.Validate(Path.Combine(game, "Redux"), game, false);
			RegressionAssert.Equal(InstallDestinationProblem.InsideGameDirectory, insideGame.Problem);

			var occupied = Path.Combine(root, "occupied");
			Directory.CreateDirectory(occupied);
			File.WriteAllText(Path.Combine(occupied, "mine.txt"), "preserve");
			var nonempty = InstallDestinationService.Validate(occupied, game, false);
			RegressionAssert.Equal(InstallDestinationProblem.NotEmpty, nonempty.Problem);
			RegressionAssert.Equal("preserve", File.ReadAllText(Path.Combine(occupied, "mine.txt")));
		}
		finally
		{
			if (Directory.Exists(root)) Directory.Delete(root, true);
		}
	}

	public void CurrentAndLegacyRuntimeNamesAreRecognizedAsExistingInstalls()
	{
		var root = Path.Combine(Path.GetTempPath(), "redux-installer-runtime-name-" + Guid.NewGuid().ToString("N"));
		try
		{
			Directory.CreateDirectory(root);
			foreach (var runtimeName in new[] { "Redux.exe", "BG3ModManager.exe" })
			{
				var runtimePath = Path.Combine(root, runtimeName);
				File.WriteAllText(runtimePath, "fixture");
				var result = InstallDestinationService.Validate(root, String.Empty, false);
				RegressionAssert.Equal(InstallDestinationProblem.ExistingInstallation, result.Problem);
				File.Delete(runtimePath);
			}
		}
		finally
		{
			if (Directory.Exists(root)) Directory.Delete(root, true);
		}
	}

	public void RuntimeDownloadAcceptsOnlyMicrosoftX64DesktopRuntimeAssets()
	{
		RegressionAssert.True(RuntimeInstallerService.IsApprovedMicrosoftRuntimeUri(new Uri(
			"https://builds.dotnet.microsoft.com/dotnet/WindowsDesktop/8.0.31/windowsdesktop-runtime-8.0.31-win-x64.exe")));
		RegressionAssert.True(RuntimeInstallerService.IsApprovedMicrosoftRuntimeUri(new Uri(
			"https://download.visualstudio.microsoft.com/download/pr/example/windowsdesktop-runtime-8.0.31-win-x64.exe?fixture=1")));
		RegressionAssert.False(RuntimeInstallerService.IsApprovedMicrosoftRuntimeUri(new Uri(
			"https://example.com/windowsdesktop-runtime-8.0.31-win-x64.exe")));
		RegressionAssert.False(RuntimeInstallerService.IsApprovedMicrosoftRuntimeUri(new Uri(
			"https://builds.dotnet.microsoft.com/dotnet/Runtime/8.0.31/dotnet-runtime-8.0.31-win-x64.exe")));
	}
}
