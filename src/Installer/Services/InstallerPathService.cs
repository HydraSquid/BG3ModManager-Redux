using Microsoft.Win32;

using ReduxInstaller.Models;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace ReduxInstaller.Services;

internal static class InstallerPathService
{
	private static readonly string[] GameExecutables =
	{
		Path.Combine("bin", "bg3_dx11.exe"),
		Path.Combine("bin", "bg3.exe")
	};

	public static DetectedInstallerPaths Detect()
	{
		var larian = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
			"Larian Studios", "Baldur's Gate 3");
		return new DetectedInstallerPaths
		{
			RecommendedInstallDirectory = Path.Combine(
				Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
				"Programs", "BG3ModManager-Redux"),
			GameDirectory = FindGameDirectory(),
			LarianDataDirectory = larian,
			ModsDirectory = Path.Combine(larian, "Mods")
		};
	}

	internal static bool IsGameDirectory(string path)
	{
		if (String.IsNullOrWhiteSpace(path)) return false;
		try
		{
			var root = Path.GetFullPath(path);
			return GameExecutables.Any(relative => File.Exists(Path.Combine(root, relative)));
		}
		catch { return false; }
	}

	private static string FindGameDirectory()
	{
		foreach (var candidate in EnumerateCandidates().Where(path => !String.IsNullOrWhiteSpace(path)))
		{
			try
			{
				var full = Path.GetFullPath(candidate.Trim().Trim('"'));
				if (IsGameDirectory(full)) return full;
			}
			catch { }
		}
		return String.Empty;
	}

	private static IEnumerable<string> EnumerateCandidates()
	{
		foreach (var view in new[] { RegistryView.Registry64, RegistryView.Registry32 })
		{
			string steam = ReadRegistryString(RegistryHive.LocalMachine, view,
				@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\Steam App 1086940", "InstallLocation");
			if (!String.IsNullOrWhiteSpace(steam)) yield return steam;
			foreach (var gog in FindGogInstallations(view)) yield return gog;
		}

		var steamPath = ReadRegistryString(RegistryHive.CurrentUser, RegistryView.Default,
			@"SOFTWARE\Valve\Steam", "SteamPath");
		if (!String.IsNullOrWhiteSpace(steamPath))
		{
			yield return Path.Combine(steamPath, "steamapps", "common", "Baldurs Gate 3");
			var libraries = Path.Combine(steamPath, "steamapps", "libraryfolders.vdf");
			foreach (var library in ReadSteamLibraryPaths(libraries))
				yield return Path.Combine(library, "steamapps", "common", "Baldurs Gate 3");
		}
	}

	private static IEnumerable<string> FindGogInstallations(RegistryView view)
	{
		var results = new List<string>();
		try
		{
			using var baseKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, view);
			using var uninstall = baseKey.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall", false);
			if (uninstall == null) return results;
			foreach (var name in uninstall.GetSubKeyNames())
			{
				using var product = uninstall.OpenSubKey(name, false);
				var display = product?.GetValue("DisplayName") as string;
				if (display == null || String.IsNullOrWhiteSpace(display)
					|| display.IndexOf("Baldur's Gate 3", StringComparison.OrdinalIgnoreCase) < 0) continue;
				var location = product?.GetValue("InstallLocation") as string;
				if (location != null && !String.IsNullOrWhiteSpace(location)) results.Add(location);
			}
		}
		catch { }
		return results;
	}

	private static IEnumerable<string> ReadSteamLibraryPaths(string file)
	{
		if (!File.Exists(file)) yield break;
		string[] lines;
		try { lines = File.ReadAllLines(file); }
		catch { yield break; }
		foreach (var line in lines)
		{
			var marker = "\"path\"";
			var index = line.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
			if (index < 0) continue;
			var remainder = line.Substring(index + marker.Length).Trim();
			if (remainder.Length < 2 || remainder[0] != '"') continue;
			var end = remainder.IndexOf('"', 1);
			if (end <= 1) continue;
			yield return remainder.Substring(1, end - 1).Replace("\\\\", "\\");
		}
	}

	private static string ReadRegistryString(RegistryHive hive, RegistryView view, string path, string value)
	{
		try
		{
			using var baseKey = RegistryKey.OpenBaseKey(hive, view);
			using var key = baseKey.OpenSubKey(path, false);
			return key?.GetValue(value) as string ?? String.Empty;
		}
		catch { return String.Empty; }
	}
}
