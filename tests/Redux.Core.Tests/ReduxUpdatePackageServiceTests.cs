using DivinityModManager.AppServices;
using DivinityModManager.Models.Updates;

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

namespace Redux.Core.Tests;

public sealed class ReduxUpdatePackageServiceTests
{
	private static readonly string[] RequiredFiles =
	{
		"Redux.exe",
		"Redux.dll",
		"Redux-Release-Files.json",
		"Updater/ReduxUpdater.exe",
		"Updater/ReduxUpdater.dll",
		"Updater/ReduxUpdater.deps.json",
		"Updater/ReduxUpdater.runtimeconfig.json"
	};

	public void VerifiedArchiveStagesWithoutChangingAnInstallation()
	{
		var archive = CreateReleaseArchive();
		var root = TemporaryDirectory();
		try
		{
			using var client = ClientReturning(archive);
			var service = new ReduxUpdatePackageService(client, root);

			var prepared = service.DownloadAndStageAsync(DecisionFor(archive)).GetAwaiter().GetResult();

			RegressionAssert.True(Directory.Exists(prepared.StagedDirectory));
			RegressionAssert.True(File.Exists(Path.Combine(prepared.StagedDirectory, "Redux.exe")));
			RegressionAssert.Equal(RequiredFiles.Length, prepared.Inventory.Files.Count);
		}
		finally
		{
			Directory.Delete(root, recursive: true);
		}
	}

	public void TraversalEntryIsRejectedAndStagingIsRemoved()
	{
		var archive = CreateReleaseArchive(("../escape.txt", "nope"));
		var root = TemporaryDirectory();
		try
		{
			using var client = ClientReturning(archive);
			var service = new ReduxUpdatePackageService(client, root);

			RegressionAssert.Throws<InvalidDataException>(() =>
				service.DownloadAndStageAsync(DecisionFor(archive)).GetAwaiter().GetResult());
			RegressionAssert.False(Directory.EnumerateFileSystemEntries(root).Any());
			RegressionAssert.False(File.Exists(Path.Combine(Path.GetDirectoryName(root)!, "escape.txt")));
		}
		finally
		{
			Directory.Delete(root, recursive: true);
		}
	}

	public void UnlistedArchiveContentIsRejected()
	{
		var archive = CreateReleaseArchive(("unexpected.dll", "not inventoried"));
		var root = TemporaryDirectory();
		try
		{
			using var client = ClientReturning(archive);
			var service = new ReduxUpdatePackageService(client, root);

			RegressionAssert.Throws<InvalidDataException>(() =>
				service.DownloadAndStageAsync(DecisionFor(archive)).GetAwaiter().GetResult());
		}
		finally
		{
			Directory.Delete(root, recursive: true);
		}
	}

	public void CleanupRemovesOnlyOldReduxTransactionDirectories()
	{
		var root = TemporaryDirectory();
		try
		{
			var oldTransaction = Path.Combine(root, "0.1.0-alpha.15-aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa");
			var maintenance = new[] { "16.4", "16.3.4" }.Select(version =>
				Path.Combine(root, "0.1.0-alpha." + version + "-aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa")).ToArray();
			foreach (var path in maintenance)
			{
				Directory.CreateDirectory(path);
				Directory.SetLastWriteTimeUtc(path, DateTime.UtcNow.AddDays(-8));
			}
			var malformed = Path.Combine(root, "0.1.0-alpha.16.3.4.5-aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa");
			Directory.CreateDirectory(malformed);
			Directory.SetLastWriteTimeUtc(malformed, DateTime.UtcNow.AddDays(-8));
			var freshTransaction = Path.Combine(root, "0.1.0-alpha.16.3.4-bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb");
			var unrelated = Path.Combine(root, "user-folder");
			Directory.CreateDirectory(oldTransaction);
			Directory.CreateDirectory(freshTransaction);
			Directory.CreateDirectory(unrelated);
			Directory.SetLastWriteTimeUtc(oldTransaction, DateTime.UtcNow.AddDays(-8));
			using var client = ClientReturning(Array.Empty<byte>());

			_ = new ReduxUpdatePackageService(client, root);

			RegressionAssert.False(Directory.Exists(oldTransaction));
			foreach (var path in maintenance) RegressionAssert.False(Directory.Exists(path));
			RegressionAssert.True(Directory.Exists(malformed));
			RegressionAssert.True(Directory.Exists(freshTransaction));
			RegressionAssert.True(Directory.Exists(unrelated));
		}
		finally
		{
			Directory.Delete(root, recursive: true);
		}
	}

	private static byte[] CreateReleaseArchive(params (string Path, string Content)[] additionalFiles)
	{
		using var buffer = new MemoryStream();
		using (var archive = new ZipArchive(buffer, ZipArchiveMode.Create, leaveOpen: true))
		{
			foreach (var path in RequiredFiles.Where(path => path != "Redux-Release-Files.json"))
				WriteEntry(archive, path, "release:" + path);
			var inventory = "{\n  \"schemaVersion\": 1,\n  \"files\": [\n"
				+ String.Join(",\n", RequiredFiles.Select(path => "    \"" + path + "\""))
				+ "\n  ]\n}\n";
			WriteEntry(archive, "Redux-Release-Files.json", inventory);
			foreach (var file in additionalFiles) WriteEntry(archive, file.Path, file.Content);
		}
		return buffer.ToArray();
	}

	private static void WriteEntry(ZipArchive archive, string path, string content)
	{
		var entry = archive.CreateEntry(path);
		using var writer = new StreamWriter(entry.Open(), Encoding.UTF8);
		writer.Write(content);
	}

	private static ReduxUpdateDecision DecisionFor(byte[] archive)
	{
		var artifact = new ReduxUpdateArtifact
		{
			Kind = ReduxUpdateArtifactKinds.Portable,
			Url = "https://github.com/circleainn/BG3ModManager-Redux/releases/download/v0.1.0-alpha.15/BG3ModManager-Redux_v0.1.0-alpha.15.zip",
			SizeBytes = archive.Length,
			Sha256 = Convert.ToHexString(SHA256.HashData(archive)).ToLowerInvariant()
		};
		return new ReduxUpdateDecision
		{
			Availability = ReduxUpdateAvailability.UpdateAvailable,
			Manifest = new ReduxUpdateManifest
			{
				DisplayVersion = "0.1.0-alpha.15",
				InternalVersion = "0.1.0.15",
				Artifacts = new[] { artifact }
			},
			Artifact = artifact
		};
	}

	private static HttpClient ClientReturning(byte[] content) => new(new StubHandler(_ =>
		new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(content) }));

	private static string TemporaryDirectory()
	{
		var path = Path.Combine(Path.GetTempPath(), "redux-update-package-tests-" + Guid.NewGuid().ToString("N"));
		Directory.CreateDirectory(path);
		return path;
	}

	private sealed class StubHandler : HttpMessageHandler
	{
		private readonly Func<HttpRequestMessage, HttpResponseMessage> _response;
		public StubHandler(Func<HttpRequestMessage, HttpResponseMessage> response) => _response = response;
		protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
			Task.FromResult(_response(request));
	}
}
