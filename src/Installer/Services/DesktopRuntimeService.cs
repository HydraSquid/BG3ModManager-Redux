using Microsoft.Win32;

using ReduxInstaller.Models;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace ReduxInstaller.Services;

internal static class DesktopRuntimeService
{
	public const int RequiredMajorVersion = 8;
	public const string RuntimeInstallerUrl = "https://aka.ms/dotnet/8.0/windowsdesktop-runtime-win-x64.exe";
	public const string RuntimeDownloadPageUrl = "https://dotnet.microsoft.com/en-us/download/dotnet/8.0";
	private const string RegistryPath = @"SOFTWARE\dotnet\Setup\InstalledVersions\x64\sharedfx\Microsoft.WindowsDesktop.App";

	public static DesktopRuntimeStatus Detect()
	{
		var versions = new List<Version>();
		ReadRegistryVersions(RegistryView.Registry64, versions);
		ReadRegistryVersions(RegistryView.Registry32, versions);
		var sharedFramework = Path.Combine(
			Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
			"dotnet", "shared", "Microsoft.WindowsDesktop.App");
		try
		{
			if (Directory.Exists(sharedFramework))
			{
				foreach (var directory in Directory.EnumerateDirectories(sharedFramework))
				{
					Version version;
					if (Version.TryParse(Path.GetFileName(directory), out version)) versions.Add(version);
				}
			}
		}
		catch { }

		return EvaluateVersions(versions);
	}

	internal static DesktopRuntimeStatus EvaluateVersions(IEnumerable<Version> versions)
	{
		var latest = versions.Where(version => version.Major == RequiredMajorVersion)
			.OrderByDescending(version => version)
			.FirstOrDefault();
		return new DesktopRuntimeStatus
		{
			IsInstalled = latest != null,
			LatestVersion = latest?.ToString() ?? String.Empty
		};
	}

	private static void ReadRegistryVersions(RegistryView view, ICollection<Version> versions)
	{
		try
		{
			using var baseKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, view);
			using var key = baseKey.OpenSubKey(RegistryPath, writable: false);
			if (key == null) return;
			foreach (var name in key.GetValueNames())
			{
				Version version;
				if (Version.TryParse(name, out version)) versions.Add(version);
			}
		}
		catch { }
	}
}
