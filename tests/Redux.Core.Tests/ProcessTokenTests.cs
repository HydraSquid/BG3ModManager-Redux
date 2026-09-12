using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using DivinityModManager.Util;
using Microsoft.Win32.SafeHandles;

namespace Redux.Core.Tests;

public sealed class ProcessTokenTests
{
	[DllImport("advapi32.dll", SetLastError = true)]
	[return: MarshalAs(UnmanagedType.Bool)]
	private static extern bool OpenProcessToken(IntPtr process, uint access, out SafeAccessTokenHandle token);
	[DllImport("advapi32.dll", SetLastError = true)]
	[return: MarshalAs(UnmanagedType.Bool)]
	private static extern bool GetTokenInformation(SafeAccessTokenHandle token, int kind, out int value, int size, out int returned);
	[DllImport("advapi32.dll", SetLastError = true)]
	[return: MarshalAs(UnmanagedType.Bool)]
	private static extern bool ImpersonateAnonymousToken(IntPtr thread);
	[DllImport("advapi32.dll", SetLastError = true)]
	[return: MarshalAs(UnmanagedType.Bool)]
	private static extern bool RevertToSelf();

	public void ElevationMatchesExplicitProcessHandleEvenDuringImpersonation()
	{
		using var process = Process.GetCurrentProcess();
		RegressionAssert.True(OpenProcessToken(process.Handle, 0x0008, out var token)); // TOKEN_QUERY
		ProcessElevationState expected;
		using (token)
		{
			RegressionAssert.True(GetTokenInformation(token, 20, out var elevated, sizeof(int), out var size));
			RegressionAssert.Equal(sizeof(int), size);
			expected = elevated != 0 ? ProcessElevationState.Elevated : ProcessElevationState.Standard;
		}
		RegressionAssert.Equal(expected, ProcessHelper.GetCurrentProcessElevation().State);
		// The anonymous identity is confined to this thread and requires no credentials.
		RegressionAssert.True(ImpersonateAnonymousToken(new IntPtr(-2))); // GetCurrentThread()
		ProcessElevationInfo actual;
		try { actual = ProcessHelper.GetCurrentProcessElevation(); }
		finally
		{
			if (!RevertToSelf()) throw new InvalidOperationException("Could not restore the regression-test thread identity.");
		}
		RegressionAssert.Equal(expected, actual.State);
		RegressionAssert.Equal(expected == ProcessElevationState.Elevated,
			ProcessElevationWarningPolicy.ShouldShow(actual, false));
	}
}
