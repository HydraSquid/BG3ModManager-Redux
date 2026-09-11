using ReduxInstaller.Models;
using ReduxInstaller.Services;

using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Redux.Installer.Tests;

internal sealed class InstallerPackageTests
{
	private static readonly string[] RequiredFiles =
	{
		"Redux.exe", "Redux.dll",
		"Updater/ReduxUpdater.exe", "Updater/ReduxUpdater.dll",
		"Updater/ReduxUpdater.deps.json", "Updater/ReduxUpdater.runtimeconfig.json"
	};

	public void VerifiedReleaseIsPreparedWithoutChangingAnInstallLocation()
	{
		var bytes = ReleaseArchive();
		using var client = ClientReturning(bytes);
		using var service = new InstallerPackageService(client);
		using var package = service.DownloadAndPrepareAsync(Manifest(bytes), null, CancellationToken.None)
			.GetAwaiter().GetResult();

		RegressionAssert.True(File.Exists(Path.Combine(package.PayloadDirectory, "Redux.exe")));
		RegressionAssert.Equal(RequiredFiles.Length + 1, package.Inventory.Files.Count);
	}

	public void ChecksumMismatchLeavesNoPreparedPackage()
	{
		var bytes = ReleaseArchive();
		var manifest = Manifest(bytes);
		manifest.Artifact.Sha256 = new string('a', 64);
		using var client = ClientReturning(bytes);
		using var service = new InstallerPackageService(client);

		RegressionAssert.Throws<InvalidDataException>(() =>
			service.DownloadAndPrepareAsync(manifest, null, CancellationToken.None).GetAwaiter().GetResult());
	}

	public void ReleaseInventoryCannotClaimReduxUserState()
	{
		RegressionAssert.Throws<InvalidDataException>(() =>
			InstallerPackageService.NormalizeRelativePath("Data/Downloads/private.zip"));
		RegressionAssert.Throws<InvalidDataException>(() =>
			InstallerPackageService.NormalizeRelativePath("Settings.json"));
	}

	private static InstallerReleaseManifest Manifest(byte[] bytes)
	{
		string hash;
		using (var sha = SHA256.Create())
			hash = BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", String.Empty).ToLowerInvariant();
		return new InstallerReleaseManifest
		{
			DisplayVersion = "0.1.0-alpha.15",
			InternalVersion = "0.1.0.15",
			Artifact = new InstallerReleaseArtifact
			{
				Kind = "portable",
				Url = "https://github.com/circleainn/BG3ModManager-Redux/releases/download/v0.1.0-alpha.15/Redux.zip",
				SizeBytes = bytes.Length,
				Sha256 = hash
			}
		};
	}

	private static byte[] ReleaseArchive()
	{
		using var output = new MemoryStream();
		using (var archive = new ZipArchive(output, ZipArchiveMode.Create, true))
		{
			var files = new List<string>(RequiredFiles) { InstallerPackageService.InventoryFileName };
			foreach (var path in RequiredFiles) WriteEntry(archive, path, "fixture");
			WriteEntry(archive, InstallerPackageService.InventoryFileName,
				"{\"schemaVersion\":1,\"files\":[" + String.Join(",", files.Select(path => "\"" + path + "\"")) + "]}");
		}
		return output.ToArray();
	}

	private static void WriteEntry(ZipArchive archive, string path, string value)
	{
		var entry = archive.CreateEntry(path, CompressionLevel.NoCompression);
		using var writer = new StreamWriter(entry.Open(), Encoding.UTF8);
		writer.Write(value);
	}

	private static HttpClient ClientReturning(byte[] bytes) => new HttpClient(new StubHandler(bytes));

	private sealed class StubHandler : HttpMessageHandler
	{
		private readonly byte[] _bytes;
		public StubHandler(byte[] bytes) => _bytes = bytes;
		protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
		{
			var response = new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(_bytes) };
			response.Content.Headers.ContentLength = _bytes.Length;
			return Task.FromResult(response);
		}
	}
}
