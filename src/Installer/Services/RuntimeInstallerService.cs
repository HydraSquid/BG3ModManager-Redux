using ReduxInstaller.Models;

using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Security.Cryptography.X509Certificates;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace ReduxInstaller.Services;

internal sealed class RuntimeInstallerService : IDisposable
{
	private const long MinimumRuntimeInstallerBytes = 1024 * 1024;
	private const long MaximumRuntimeInstallerBytes = 300L * 1024 * 1024;
	private static readonly Regex RuntimeFileName = new Regex(
		@"^windowsdesktop-runtime-8\.0\.[0-9]+-win-x64\.exe$",
		RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
	private readonly HttpClient _client;
	private readonly bool _ownsClient;

	public RuntimeInstallerService(HttpClient? client = null)
	{
		_client = client ?? CreateClient();
		_ownsClient = client == null;
	}

	public async Task<RuntimeInstallResult> DownloadVerifyAndInstallAsync(
		IProgress<InstallerProgress>? progress,
		CancellationToken cancellationToken)
	{
		var path = Path.Combine(Path.GetTempPath(), "ReduxSetup-DotNet-" + Guid.NewGuid().ToString("N") + ".exe");
		try
		{
			progress?.Report(new InstallerProgress { Status = "Downloading .NET 8 Desktop Runtime...", Fraction = 0 });
			using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
			timeout.CancelAfter(TimeSpan.FromMinutes(10));
			using var response = await _client.GetAsync(DesktopRuntimeService.RuntimeInstallerUrl,
				HttpCompletionOption.ResponseHeadersRead, timeout.Token).ConfigureAwait(false);
			response.EnsureSuccessStatusCode();
			var finalUri = response.RequestMessage?.RequestUri;
			if (finalUri == null || !IsApprovedMicrosoftRuntimeUri(finalUri))
				throw new InvalidDataException("Microsoft redirected the runtime download to an unexpected location.");
			var declared = response.Content.Headers.ContentLength;
			if (declared.HasValue && (declared.Value < MinimumRuntimeInstallerBytes || declared.Value > MaximumRuntimeInstallerBytes))
				throw new InvalidDataException("Microsoft returned an unexpected runtime installer size.");

			using (var input = await response.Content.ReadAsStreamAsync().ConfigureAwait(false))
			using (var output = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None, 65536, true))
			{
				var buffer = new byte[65536];
				long received = 0;
				while (true)
				{
					var read = await input.ReadAsync(buffer, 0, buffer.Length, timeout.Token).ConfigureAwait(false);
					if (read == 0) break;
					received += read;
					if (received > MaximumRuntimeInstallerBytes)
						throw new InvalidDataException("The runtime download exceeded the supported size limit.");
					await output.WriteAsync(buffer, 0, read, timeout.Token).ConfigureAwait(false);
					progress?.Report(new InstallerProgress
					{
						Status = "Downloading .NET 8 Desktop Runtime...",
						Fraction = declared.HasValue && declared.Value > 0 ? Math.Min(0.75, received / (double)declared.Value * 0.75) : 0.35
					});
				}
				await output.FlushAsync(timeout.Token).ConfigureAwait(false);
				if (received < MinimumRuntimeInstallerBytes || (declared.HasValue && received != declared.Value))
					throw new InvalidDataException("The runtime installer download is incomplete.");
			}

			progress?.Report(new InstallerProgress { Status = "Verifying Microsoft's signature...", Fraction = 0.82 });
			if (!VerifyMicrosoftSignature(path))
				throw new InvalidDataException("The .NET runtime installer does not have a valid Microsoft Authenticode signature.");

			progress?.Report(new InstallerProgress { Status = "Waiting for .NET setup...", Fraction = 0.9 });
			using var process = Process.Start(new ProcessStartInfo(path, "/install")
			{
				UseShellExecute = true,
				WorkingDirectory = Path.GetDirectoryName(path)
			});
			if (process == null) throw new InvalidOperationException("Windows could not start the .NET runtime installer.");
			await Task.Run(() => process.WaitForExit()).ConfigureAwait(false);
			if (process.ExitCode == 1602) throw new OperationCanceledException(".NET setup was cancelled.");
			if (process.ExitCode != 0 && process.ExitCode != 3010)
				throw new Win32Exception(process.ExitCode, ".NET setup did not complete successfully.");

			var detected = DesktopRuntimeService.Detect();
			if (!detected.IsInstalled)
				throw new InvalidOperationException(".NET setup finished, but the required x64 Desktop Runtime is still unavailable.");
			progress?.Report(new InstallerProgress { Status = ".NET 8 Desktop Runtime is ready.", Fraction = 1 });
			return new RuntimeInstallResult
			{
				Installed = true,
				RestartRecommended = process.ExitCode == 3010,
				Version = detected.LatestVersion
			};
		}
		finally
		{
			try { if (File.Exists(path)) File.Delete(path); } catch { }
		}
	}

	internal static bool IsApprovedMicrosoftRuntimeUri(Uri uri)
	{
		if (uri == null || !uri.IsAbsoluteUri
			|| !String.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)
			|| !uri.IsDefaultPort || !String.IsNullOrEmpty(uri.UserInfo)
			|| !String.IsNullOrEmpty(uri.Fragment)) return false;
		if (!String.Equals(uri.Host, "builds.dotnet.microsoft.com", StringComparison.OrdinalIgnoreCase)
			&& !String.Equals(uri.Host, "download.visualstudio.microsoft.com", StringComparison.OrdinalIgnoreCase)) return false;
		var name = Path.GetFileName(uri.AbsolutePath);
		return RuntimeFileName.IsMatch(name);
	}

	internal static bool VerifyMicrosoftSignature(string filePath)
	{
		if (String.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath)) return false;
		var fileInfo = new WinTrustFileInfo(filePath);
		var data = new WinTrustData(fileInfo);
		try
		{
			var action = WinTrustActionGenericVerifyV2;
			if (WinVerifyTrust(IntPtr.Zero, ref action, data) != 0) return false;
			using var certificate = new X509Certificate2(X509Certificate.CreateFromSignedFile(filePath));
			var simpleName = certificate.GetNameInfo(X509NameType.SimpleName, false);
			return String.Equals(simpleName, "Microsoft Corporation", StringComparison.OrdinalIgnoreCase)
				|| certificate.Subject.IndexOf("O=Microsoft Corporation", StringComparison.OrdinalIgnoreCase) >= 0;
		}
		catch { return false; }
	}

	private static HttpClient CreateClient()
	{
		var client = new HttpClient(new HttpClientHandler { AllowAutoRedirect = true, MaxAutomaticRedirections = 5 });
		client.DefaultRequestHeaders.UserAgent.ParseAdd("BG3ModManager-Redux-Setup");
		return client;
	}

	public void Dispose()
	{
		if (_ownsClient) _client.Dispose();
	}

	private static readonly Guid WinTrustActionGenericVerifyV2 =
		new Guid("00AAC56B-CD44-11d0-8CC2-00C04FC295EE");

	[DllImport("wintrust.dll", ExactSpelling = true, SetLastError = false, CharSet = CharSet.Unicode)]
	private static extern uint WinVerifyTrust(IntPtr hwnd, ref Guid actionId, [In] WinTrustData data);

	private enum WinTrustDataUIChoice : uint { None = 2 }
	private enum WinTrustDataRevocationChecks : uint { None = 0 }
	private enum WinTrustDataChoice : uint { File = 1 }
	private enum WinTrustDataStateAction : uint { Ignore = 0 }
	private enum WinTrustDataUIContext : uint { Execute = 0 }

	[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
	private sealed class WinTrustFileInfo
	{
		private readonly uint cbStruct = (uint)Marshal.SizeOf(typeof(WinTrustFileInfo));
		[MarshalAs(UnmanagedType.LPWStr)] private readonly string pcwszFilePath;
		private readonly IntPtr hFile = IntPtr.Zero;
		private readonly IntPtr pgKnownSubject = IntPtr.Zero;
		public WinTrustFileInfo(string filePath) => pcwszFilePath = filePath;
	}

	[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
	private sealed class WinTrustData
	{
		private readonly uint cbStruct = (uint)Marshal.SizeOf(typeof(WinTrustData));
		private readonly IntPtr pPolicyCallbackData = IntPtr.Zero;
		private readonly IntPtr pSIPClientData = IntPtr.Zero;
		private readonly WinTrustDataUIChoice dwUIChoice = WinTrustDataUIChoice.None;
		private readonly WinTrustDataRevocationChecks fdwRevocationChecks = WinTrustDataRevocationChecks.None;
		private readonly WinTrustDataChoice dwUnionChoice = WinTrustDataChoice.File;
		[MarshalAs(UnmanagedType.Struct)] private readonly WinTrustFileInfo pFile;
		private readonly WinTrustDataStateAction dwStateAction = WinTrustDataStateAction.Ignore;
		private readonly IntPtr hWVTStateData = IntPtr.Zero;
		private readonly IntPtr pwszURLReference = IntPtr.Zero;
		private readonly uint dwProvFlags = 0x00001000;
		private readonly WinTrustDataUIContext dwUIContext = WinTrustDataUIContext.Execute;
		public WinTrustData(WinTrustFileInfo fileInfo) => pFile = fileInfo;
	}
}
