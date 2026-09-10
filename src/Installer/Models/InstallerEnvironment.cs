using System;
using System.Collections.Generic;

namespace ReduxInstaller.Models;

internal sealed class DesktopRuntimeStatus
{
	public bool IsInstalled { get; set; }
	public string LatestVersion { get; set; } = String.Empty;
}

internal sealed class RuntimeInstallResult
{
	public bool Installed { get; set; }
	public bool RestartRecommended { get; set; }
	public string Version { get; set; } = String.Empty;
}

internal sealed class DetectedInstallerPaths
{
	public string RecommendedInstallDirectory { get; set; } = String.Empty;
	public string GameDirectory { get; set; } = String.Empty;
	public string LarianDataDirectory { get; set; } = String.Empty;
	public string ModsDirectory { get; set; } = String.Empty;
}

internal enum InstallDestinationProblem
{
	None,
	Missing,
	FilesystemRoot,
	InsideGameDirectory,
	ExistingInstallation,
	NotEmpty,
	ReparsePoint,
	NotWritable
}

internal sealed class InstallDestinationValidation
{
	public bool IsValid => Problem == InstallDestinationProblem.None;
	public InstallDestinationProblem Problem { get; set; }
	public string Message { get; set; } = String.Empty;
	public string NormalizedPath { get; set; } = String.Empty;
}

internal sealed class InstallerInstallRequest
{
	public PreparedInstallerPackage Package { get; set; } = new PreparedInstallerPackage();
	public string DestinationDirectory { get; set; } = String.Empty;
	public string GameDirectory { get; set; } = String.Empty;
	public string SetupExecutablePath { get; set; } = String.Empty;
	public bool CreateDesktopShortcut { get; set; }
	public bool UpdateExisting { get; set; }
}

internal sealed class InstallerInstallResult
{
	public string DestinationDirectory { get; set; } = String.Empty;
	public string ApplicationPath { get; set; } = String.Empty;
	public string DisplayVersion { get; set; } = String.Empty;
	public bool UpdatedExisting { get; set; }
}

internal sealed class UninstallResult
{
	public int RemovedApplicationFiles { get; set; }
	public IReadOnlyList<string> FilesThatCouldNotBeRemoved { get; set; } = Array.Empty<string>();
	public bool PreservedUserContent { get; set; }
}
