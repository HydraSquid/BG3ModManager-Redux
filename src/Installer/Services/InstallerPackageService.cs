using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

using ReduxInstaller.Models;

using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;

namespace ReduxInstaller.Services;

internal sealed class InstallerPackageService : IDisposable
{
	public const string InventoryFileName = "Redux-Release-Files.json";
	private const int MaximumEntries = 4096;
	private const long MaximumExpandedBytes = 2L * 1024 * 1024 * 1024;
	private static readonly HashSet<string> InventoryProperties = new HashSet<string>(StringComparer.Ordinal)
	{
		"schemaVersion", "files"
	};
	private static readonly HashSet<string> ProtectedRoots = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
	{
		"Data", "Orders", "CurrentOrders", "_Logs", "Logs", "Cache", "_Cache", "Backup", "_Backup",
		"GameDirectoryInstalls", "RestorePoints", "Temp"
	};
	private static readonly HashSet<string> ProtectedFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
	{
		"settings.json", "keybindings.json", "ScriptExtenderSettings.json", "LastExported.json",
		"mod-annotations.json", "hosts.yml", ".env", "debug"
	};
	private static readonly string[] RequiredFiles =
	{
		"Redux.exe", "Redux.dll", InventoryFileName,
		"Updater/ReduxUpdater.exe", "Updater/ReduxUpdater.dll",
		"Updater/ReduxUpdater.deps.json", "Updater/ReduxUpdater.runtimeconfig.json"
	};

	private readonly HttpClient _client;
	private readonly bool _ownsClient;

	public InstallerPackageService(HttpClient? client = null)
	{
		_client = client ?? CreateClient();
		_ownsClient = client == null;
	}

	public async Task<PreparedInstallerPackage> DownloadAndPrepareAsync(
		InstallerReleaseManifest manifest,
		IProgress<InstallerProgress>? progress,
		CancellationToken cancellationToken)
	{
		if (manifest == null) throw new ArgumentNullException(nameof(manifest));
		var working = Path.Combine(Path.GetTempPath(), "ReduxSetup-" + Guid.NewGuid().ToString("N"));
		var partialArchive = Path.Combine(working, "Redux.zip.partial");
		var archive = Path.Combine(working, "Redux.zip");
		var payload = Path.Combine(working, "payload");
		Directory.CreateDirectory(working);
		try
		{
			progress?.Report(new InstallerProgress { Status = "Downloading Redux...", Fraction = 0 });
			await DownloadAsync(manifest.Artifact, partialArchive, progress, cancellationToken).ConfigureAwait(false);
			File.Move(partialArchive, archive);
			progress?.Report(new InstallerProgress { Status = "Verifying the download...", Fraction = 0.82 });
			VerifyHashAndLength(archive, manifest.Artifact);
			Directory.CreateDirectory(payload);
			progress?.Report(new InstallerProgress { Status = "Inspecting the release...", Fraction = 0.86 });
			await ExtractSafelyAsync(archive, payload, progress, cancellationToken).ConfigureAwait(false);
			var inventory = ReadAndValidateInventory(payload, true);
			progress?.Report(new InstallerProgress { Status = "Redux is ready to install.", Fraction = 1 });
			return new PreparedInstallerPackage
			{
				WorkingDirectory = working,
				PayloadDirectory = payload,
				Manifest = manifest,
				Inventory = inventory
			};
		}
		catch
		{
			TryDeleteDirectory(working);
			throw;
		}
	}

	internal static InstallerReleaseInventory ReadAndValidateInventory(string releaseDirectory, bool exact)
	{
		var root = Path.GetFullPath(releaseDirectory);
		var path = Path.Combine(root, InventoryFileName);
		if (!File.Exists(path)) throw new InvalidDataException("The Redux release inventory is missing.");
		JObject json;
		using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
		using (var text = new StreamReader(stream))
		using (var reader = new JsonTextReader(text) { MaxDepth = 8, DateParseHandling = DateParseHandling.None })
		{
			json = JObject.Load(reader, new JsonLoadSettings
			{
				DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error
			});
			if (reader.Read()) throw new InvalidDataException("The Redux release inventory contains trailing content.");
		}
		var unknown = json.Properties().FirstOrDefault(property => !InventoryProperties.Contains(property.Name));
		if (unknown != null) throw new InvalidDataException("The Redux release inventory contains unsupported data.");
		var schemaToken = json["schemaVersion"];
		if (schemaToken == null || schemaToken.Type != JTokenType.Integer
			|| schemaToken.Value<int>() != 1)
			throw new InvalidDataException("The Redux release inventory schema is not supported.");
		var tokens = json["files"] as JArray;
		if (tokens == null || tokens.Count == 0 || tokens.Count > MaximumEntries)
			throw new InvalidDataException("The Redux release inventory has an invalid file list.");

		var files = new List<string>(tokens.Count);
		var unique = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		foreach (var token in tokens)
		{
			if (token.Type != JTokenType.String) throw new InvalidDataException("Every release entry must be a path string.");
			var relative = NormalizeRelativePath(token.Value<string>() ?? String.Empty);
			if (!unique.Add(relative)) throw new InvalidDataException("The release inventory contains duplicate paths.");
			files.Add(relative);
		}
		foreach (var required in RequiredFiles)
			if (!unique.Contains(required)) throw new InvalidDataException("The Redux release is incomplete: " + required);

		if (exact)
		{
			var prefix = root.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
			var actual = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
			foreach (var file in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories))
			{
				var full = Path.GetFullPath(file);
				if (!full.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
					throw new InvalidDataException("A release file leaves its staging directory.");
				actual.Add(NormalizeRelativePath(full.Substring(prefix.Length)));
			}
			if (!actual.SetEquals(unique)) throw new InvalidDataException("The release contents do not match their inventory.");
		}
		return new InstallerReleaseInventory { SchemaVersion = 1, Files = files.AsReadOnly() };
	}

	internal static string NormalizeRelativePath(string value)
	{
		if (String.IsNullOrWhiteSpace(value)) throw new InvalidDataException("Release paths cannot be empty.");
		var normalized = value.Trim().Replace('\\', '/');
		if (normalized.StartsWith("/", StringComparison.Ordinal) || normalized.EndsWith("/", StringComparison.Ordinal)
			|| Path.IsPathRooted(normalized)) throw new InvalidDataException("A release path is not relative.");
		var parts = normalized.Split('/');
		if (parts.Any(part => part.Length == 0 || part == "." || part == ".." || part.IndexOf(':') >= 0))
			throw new InvalidDataException("A release path is unsafe.");
		if (ProtectedRoots.Contains(parts[0]) || ProtectedFiles.Contains(parts[parts.Length - 1]))
			throw new InvalidDataException("A release path belongs to protected user state.");
		return String.Join("/", parts);
	}

	private async Task DownloadAsync(InstallerReleaseArtifact artifact, string destination,
		IProgress<InstallerProgress>? progress, CancellationToken cancellationToken)
	{
		using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
		timeout.CancelAfter(TimeSpan.FromMinutes(10));
		using var request = new HttpRequestMessage(HttpMethod.Get, artifact.Url);
		request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/zip"));
		using var response = await _client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeout.Token)
			.ConfigureAwait(false);
		response.EnsureSuccessStatusCode();
		if (response.Content.Headers.ContentLength.HasValue
			&& response.Content.Headers.ContentLength.Value != artifact.SizeBytes)
			throw new InvalidDataException("GitHub returned an unexpected release size.");
		using var input = await response.Content.ReadAsStreamAsync().ConfigureAwait(false);
		using var output = new FileStream(destination, FileMode.CreateNew, FileAccess.Write, FileShare.None, 65536, true);
		var buffer = new byte[65536];
		long received = 0;
		while (true)
		{
			var read = await input.ReadAsync(buffer, 0, buffer.Length, timeout.Token).ConfigureAwait(false);
			if (read == 0) break;
			received += read;
			if (received > artifact.SizeBytes) throw new InvalidDataException("The release download exceeded its declared size.");
			await output.WriteAsync(buffer, 0, read, timeout.Token).ConfigureAwait(false);
			progress?.Report(new InstallerProgress
			{
				Status = "Downloading Redux...",
				Fraction = Math.Min(0.8, received / (double)artifact.SizeBytes * 0.8)
			});
		}
		await output.FlushAsync(timeout.Token).ConfigureAwait(false);
		if (received != artifact.SizeBytes) throw new InvalidDataException("The release download is incomplete.");
	}

	private static void VerifyHashAndLength(string archive, InstallerReleaseArtifact artifact)
	{
		var info = new FileInfo(archive);
		if (!info.Exists || info.Length != artifact.SizeBytes) throw new InvalidDataException("The release size check failed.");
		string hash;
		using (var algorithm = SHA256.Create())
		using (var stream = File.OpenRead(archive))
			hash = BitConverter.ToString(algorithm.ComputeHash(stream)).Replace("-", String.Empty).ToLowerInvariant();
		if (!String.Equals(hash, artifact.Sha256, StringComparison.OrdinalIgnoreCase))
			throw new InvalidDataException("The release failed SHA-256 verification.");
	}

	private static async Task ExtractSafelyAsync(string archivePath, string destination,
		IProgress<InstallerProgress>? progress, CancellationToken cancellationToken)
	{
		using var archive = ZipFile.OpenRead(archivePath);
		if (archive.Entries.Count == 0 || archive.Entries.Count > MaximumEntries)
			throw new InvalidDataException("The release archive has an invalid number of entries.");
		var files = archive.Entries.Where(entry => !String.IsNullOrEmpty(entry.Name)).ToList();
		long expanded = 0;
		foreach (var file in files)
		{
			checked { expanded += file.Length; }
			if (expanded > MaximumExpandedBytes) throw new InvalidDataException("The expanded release is too large.");
		}
		if (expanded <= 0) throw new InvalidDataException("The release archive is empty.");

		var root = Path.GetFullPath(destination).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
		var unique = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		long extracted = 0;
		foreach (var entry in archive.Entries)
		{
			cancellationToken.ThrowIfCancellationRequested();
			var unixType = (entry.ExternalAttributes >> 16) & 0xF000;
			if (unixType == 0xA000 || (((FileAttributes)entry.ExternalAttributes) & FileAttributes.ReparsePoint) != 0)
				throw new InvalidDataException("The release contains an unsupported linked entry.");
			if (String.IsNullOrEmpty(entry.Name)) continue;
			var relative = NormalizeRelativePath(entry.FullName);
			if (!unique.Add(relative)) throw new InvalidDataException("The release archive contains duplicate paths.");
			var target = Path.GetFullPath(Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar)));
			if (!target.StartsWith(root, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("A release path leaves staging.");
			Directory.CreateDirectory(Path.GetDirectoryName(target));
			using (var input = entry.Open())
			using (var output = new FileStream(target, FileMode.CreateNew, FileAccess.Write, FileShare.None, 65536, true))
				await input.CopyToAsync(output, 65536, cancellationToken).ConfigureAwait(false);
			extracted += entry.Length;
			progress?.Report(new InstallerProgress
			{
				Status = "Inspecting the release...",
				Fraction = 0.86 + extracted / (double)expanded * 0.12
			});
		}
	}

	private static HttpClient CreateClient()
	{
		var client = new HttpClient();
		client.DefaultRequestHeaders.UserAgent.ParseAdd("BG3ModManager-Redux-Setup");
		return client;
	}

	private static void TryDeleteDirectory(string path)
	{
		try { if (Directory.Exists(path)) Directory.Delete(path, true); }
		catch { }
	}

	public void Dispose()
	{
		if (_ownsClient) _client.Dispose();
	}
}
