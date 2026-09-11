using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Text;
using System.Threading.Tasks;

using Microsoft.Win32.SafeHandles;

namespace DivinityModManager.Util;

public enum ProcessElevationState
{
	Unknown,
	Standard,
	Elevated
}

public readonly record struct ProcessElevationInfo(ProcessElevationState State, int Win32Error = 0)
{
	public bool IsElevated => State == ProcessElevationState.Elevated;
}

public static partial class ProcessHelper
{
	private enum TokenInformationClass
	{
		TokenElevation = 20
	}

	[StructLayout(LayoutKind.Sequential)]
	private struct TokenElevation
	{
		public int TokenIsElevated;
	}

	[LibraryImport("advapi32.dll", SetLastError = true)]
	[return: MarshalAs(UnmanagedType.Bool)]
	private static partial bool GetTokenInformation(
		SafeAccessTokenHandle tokenHandle,
		TokenInformationClass tokenInformationClass,
		out TokenElevation tokenInformation,
		uint tokenInformationLength,
		out uint returnLength);

	/// <summary>
	/// Suppresses PlatformNotSupportedException
	/// </summary>
	private static void TrySetUseShellExecute(ProcessStartInfo info)
	{
		try
		{
			info.UseShellExecute = true;
		}
		catch (PlatformNotSupportedException) { }
	}

	public static bool TryRunCommand(string path, string args = "", string workingDirectory = null)
	{
		args ??= string.Empty;

		try
		{
			path = Environment.ExpandEnvironmentVariables(path);
			var info = new ProcessStartInfo(path, args);
			TrySetUseShellExecute(info);
			if (workingDirectory != null) info.WorkingDirectory = workingDirectory;
			Process.Start(info);
			return true;
		}
		catch (Exception ex)
		{
			DivinityApp.Log($"Error running command:\n{ex}");
		}
		return false;
	}

	public static bool TryOpenPath(string path, Func<string, bool> existsCheck = null, string args = "", string workingDirectory = null)
	{
		args ??= string.Empty;

		try
		{
			if (!string.IsNullOrEmpty(path))
			{
				//Support using %LOCALAPPDATA% etc.
				path = Environment.ExpandEnvironmentVariables(path);
				if (!Path.IsPathRooted(path)) path = DivinityApp.GetAppDirectory(path);
				if(existsCheck != null && existsCheck.Invoke(path) == false)
				{
					return false;
				}
				if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
				{
					var info = new ProcessStartInfo(path, args);
					TrySetUseShellExecute(info);
					if (workingDirectory != null) info.WorkingDirectory = workingDirectory;
					Process.Start(info);
					return true;
				}
				else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
				{
					Process.Start("xdg-open", path);
					return true;
				}
				else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
				{
					Process.Start("open", path);
					return true;
				}
			}
		}
		catch (Exception ex)
		{
			DivinityApp.Log($"Error opening path:\n{ex}");
		}
		return false;
	}

	//Source: https://stackoverflow.com/a/43232486
	public static void TryOpenUrl(string url, string args = "")
	{
		try
		{
			Process.Start(url, args);
		}
		catch
		{
			// Older runtimes require shell execution to open URLs on Windows.
			// See https://github.com/dotnet/corefx/issues/10361.
			if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
			{
				url = url.Replace("&", "^&");
				var info = new ProcessStartInfo(url, args);
				TrySetUseShellExecute(info);
				Process.Start(info);
			}
			else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
			{
				Process.Start("xdg-open", url);
			}
			else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
			{
				Process.Start("open", url);
			}
			else
			{
				throw;
			}
		}
	}

	/// <summary>
	/// Reads the elevation flag from the current process token. This distinguishes
	/// actual process elevation from membership in the local Administrators group.
	/// </summary>
	public static ProcessElevationInfo GetCurrentProcessElevation()
	{
		if (!OperatingSystem.IsWindows()) return new ProcessElevationInfo(ProcessElevationState.Unknown);
		try
		{
			using var identity = WindowsIdentity.GetCurrent(TokenAccessLevels.Query);
			var size = (uint)Marshal.SizeOf<TokenElevation>();
			if (!GetTokenInformation(identity.AccessToken, TokenInformationClass.TokenElevation,
				out var elevation, size, out _))
			{
				return new ProcessElevationInfo(ProcessElevationState.Unknown, Marshal.GetLastWin32Error());
			}

			return new ProcessElevationInfo(elevation.TokenIsElevated != 0
				? ProcessElevationState.Elevated
				: ProcessElevationState.Standard);
		}
		catch (Exception ex)
		{
			DivinityApp.Log($"Windows process-token elevation detection failed:\n{ex}");
			return new ProcessElevationInfo(ProcessElevationState.Unknown);
		}
	}

	public static bool IsCurrentProcessAdmin() => GetCurrentProcessElevation().IsElevated;
}
