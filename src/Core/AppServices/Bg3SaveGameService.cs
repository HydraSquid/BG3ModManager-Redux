using LSLib.LS;

using SharpCompress.Archives;
using SharpCompress.Common;
using SharpCompress.Readers;

namespace DivinityModManager.AppServices;

public enum Bg3SaveDifficulty
{
	Unknown,
	Explorer,
	Balanced,
	Tactician,
	Honour,
	Custom
}

public sealed record Bg3SaveGameEntry(
	string FolderPath,
	string FolderName,
	string DisplayName,
	string CampaignName,
	string SaveFilePath,
	string ThumbnailPath,
	DateTime ModifiedUtc,
	long SizeBytes,
	Bg3SaveDifficulty Difficulty);

/// <summary>
/// Discovers and imports BG3 story saves. Discovery reads the small SaveInfo.json
/// package entry for display-only metadata and never modifies the LSV payload.
/// Imported folders are staged beside the destination and moved into place only after
/// every selected file has been written successfully.
/// </summary>
public static class Bg3SaveGameService
{
	private const int MaximumSaveGroupsPerArchive = 32;
	private const long MaximumEntryBytes = 256L * 1024L * 1024L;
	private const long MaximumArchiveBytes = 1024L * 1024L * 1024L;
	private static readonly HashSet<string> SupportedExtensions = new(StringComparer.OrdinalIgnoreCase)
	{
		".lsv", ".webp"
	};
	private static readonly HashSet<string> SupportedArchiveExtensions = new(StringComparer.OrdinalIgnoreCase)
	{
		".7z", ".7zip", ".gz", ".gzip", ".rar", ".tar", ".tgz", ".zip"
	};
	private static readonly object MetadataCacheLock = new();
	private static readonly Dictionary<string, CachedSaveMetadata> MetadataCache = new(StringComparer.OrdinalIgnoreCase);

	public static IReadOnlyList<Bg3SaveGameEntry> Discover(string storyFolder)
	{
		if (String.IsNullOrWhiteSpace(storyFolder) || !Directory.Exists(storyFolder)) return [];
		var saves = new List<Bg3SaveGameEntry>();
		foreach (var folder in Directory.EnumerateDirectories(storyFolder))
		{
			if (Path.GetFileName(folder).StartsWith(".redux-", StringComparison.OrdinalIgnoreCase)) continue;
			try
			{
				var saveFile = Directory.EnumerateFiles(folder, "*.lsv", SearchOption.TopDirectoryOnly)
					.OrderByDescending(File.GetLastWriteTimeUtc)
					.FirstOrDefault();
				if (saveFile == null) continue;
				var saveStem = Path.GetFileNameWithoutExtension(saveFile);
				var thumbnail = Directory.EnumerateFiles(folder, "*.webp", SearchOption.TopDirectoryOnly)
					.FirstOrDefault(path => Path.GetFileNameWithoutExtension(path).Equals(saveStem, StringComparison.OrdinalIgnoreCase))
					?? Directory.EnumerateFiles(folder, "*.webp", SearchOption.TopDirectoryOnly).FirstOrDefault();
				var files = Directory.EnumerateFiles(folder, "*", SearchOption.TopDirectoryOnly).Select(path => new FileInfo(path)).ToArray();
				var saveInfo = new FileInfo(saveFile);
				var folderName = Path.GetFileName(folder);
				var separator = folderName.IndexOf("__", StringComparison.Ordinal);
				var campaign = separator > 0 ? folderName[..separator] : String.Empty;
				var displayName = separator >= 0 && separator + 2 < folderName.Length
					? folderName[(separator + 2)..]
					: saveStem;
				saves.Add(new Bg3SaveGameEntry(
					folder,
					folderName,
					displayName.Replace('_', ' '),
					campaign,
					saveFile,
					thumbnail,
					files.Max(file => file.LastWriteTimeUtc),
					files.Sum(file => file.Length),
					ReadDifficulty(saveInfo)));
			}
			catch (IOException)
			{
				// A save still being written by the game is omitted until the next refresh.
			}
			catch (UnauthorizedAccessException)
			{
				// Keep the rest of the profile usable if one folder is inaccessible.
			}
		}
		return saves.OrderByDescending(save => save.ModifiedUtc).ToArray();
	}

	public static Bg3SaveDifficulty ClassifyDifficulty(IEnumerable<string> values)
	{
		var entries = (values ?? []).Where(value => !String.IsNullOrWhiteSpace(value)).ToArray();
		if (entries.Any(value => value.Equals("RulesetHonour", StringComparison.OrdinalIgnoreCase))) return Bg3SaveDifficulty.Honour;
		if (entries.Any(value => value.Equals("RulesetCustom", StringComparison.OrdinalIgnoreCase))) return Bg3SaveDifficulty.Custom;
		if (entries.Any(value => value.Equals("DifficultyHard", StringComparison.OrdinalIgnoreCase))) return Bg3SaveDifficulty.Tactician;
		if (entries.Any(value => value.Equals("DifficultyMedium", StringComparison.OrdinalIgnoreCase))) return Bg3SaveDifficulty.Balanced;
		if (entries.Any(value => value.Equals("DifficultyEasy", StringComparison.OrdinalIgnoreCase))) return Bg3SaveDifficulty.Explorer;
		return Bg3SaveDifficulty.Unknown;
	}

	private static Bg3SaveDifficulty ReadDifficulty(FileInfo saveFile)
	{
		if (saveFile.Length < 64) return Bg3SaveDifficulty.Unknown;
		lock (MetadataCacheLock)
		{
			if (MetadataCache.TryGetValue(saveFile.FullName, out var cached)
				&& cached.Length == saveFile.Length
				&& cached.ModifiedUtc == saveFile.LastWriteTimeUtc)
				return cached.Difficulty;
		}

		var difficulty = Bg3SaveDifficulty.Unknown;
		try
		{
			var reader = new PackageReader();
			using var package = reader.Read(saveFile.FullName);
			var info = package.Files.FirstOrDefault(file =>
				file.Name.Equals("SaveInfo.json", StringComparison.OrdinalIgnoreCase));
			if (info != null)
			{
				using var stream = info.CreateContentReader();
				using var document = JsonDocument.Parse(stream);
				if (document.RootElement.TryGetProperty("Difficulty", out var values)
					&& values.ValueKind == JsonValueKind.Array)
				{
					difficulty = ClassifyDifficulty(values.EnumerateArray()
						.Where(value => value.ValueKind == JsonValueKind.String)
						.Select(value => value.GetString()));
				}
			}
		}
		catch (Exception ex) when (ex is IOException or InvalidDataException or NotAPackageException or UnauthorizedAccessException or JsonException)
		{
			// Save metadata is optional presentation data. A damaged or temporarily locked
			// package remains visible in the manager without a difficulty badge.
		}

		lock (MetadataCacheLock)
			MetadataCache[saveFile.FullName] = new CachedSaveMetadata(saveFile.Length, saveFile.LastWriteTimeUtc, difficulty);
		return difficulty;
	}

	public static IReadOnlyList<string> GetImportFolderNames(string sourcePath)
	{
		if (Directory.Exists(sourcePath))
		{
			return Directory.EnumerateFiles(sourcePath, "*.lsv", SearchOption.TopDirectoryOnly).Any()
				? [NormalizeFolderName(Path.GetFileName(sourcePath), Directory.EnumerateFiles(sourcePath, "*.lsv").First())]
				: [];
		}
		if (File.Exists(sourcePath) && Path.GetExtension(sourcePath).Equals(".lsv", StringComparison.OrdinalIgnoreCase))
			return [NormalizeFolderName(String.Empty, sourcePath)];
		if (!File.Exists(sourcePath) || !IsSupportedSaveArchive(sourcePath))
			return [];

		using var input = OpenArchiveStream(sourcePath);
		using var archive = ArchiveFactory.OpenArchive(input, new ReaderOptions());
		return BuildArchiveGroups(archive.Entries.Where(entry => !entry.IsDirectory).ToArray())
			.Select(group => group.DestinationName)
			.ToArray();
	}

	public static bool IsSupportedSaveInput(string sourcePath)
	{
		if (String.IsNullOrWhiteSpace(sourcePath)) return false;
		if (Directory.Exists(sourcePath)) return true;
		if (!File.Exists(sourcePath)) return false;
		return Path.GetExtension(sourcePath).Equals(".lsv", StringComparison.OrdinalIgnoreCase)
			|| IsSupportedSaveArchive(sourcePath);
	}

	public static bool IsSupportedSaveArchive(string sourcePath)
	{
		if (String.IsNullOrWhiteSpace(sourcePath)) return false;
		var lowerPath = sourcePath.ToLowerInvariant();
		return lowerPath.EndsWith(".tar.gz", StringComparison.Ordinal)
			|| SupportedArchiveExtensions.Contains(Path.GetExtension(lowerPath));
	}

	public static IReadOnlyList<string> Import(string sourcePath, string storyFolder, bool replaceExisting)
	{
		if (String.IsNullOrWhiteSpace(storyFolder)) throw new ArgumentException("A story save folder is required.", nameof(storyFolder));
		Directory.CreateDirectory(storyFolder);
		if (Directory.Exists(sourcePath)) return ImportFolder(sourcePath, storyFolder, replaceExisting);
		if (File.Exists(sourcePath) && Path.GetExtension(sourcePath).Equals(".lsv", StringComparison.OrdinalIgnoreCase))
			return ImportLooseSave(sourcePath, storyFolder, replaceExisting);
		if (!File.Exists(sourcePath) || !IsSupportedSaveArchive(sourcePath))
			throw new InvalidDataException("Choose a BG3 save folder, .lsv file, or supported archive.");

		using var input = OpenArchiveStream(sourcePath);
		using var archive = ArchiveFactory.OpenArchive(input, new ReaderOptions());
		var groups = BuildArchiveGroups(archive.Entries.Where(entry => !entry.IsDirectory).ToArray());
		if (groups.Count == 0) throw new InvalidDataException("The archive does not contain a BG3 .lsv save.");
		var imported = new List<string>();
		foreach (var group in groups)
		{
			ImportAtomically(storyFolder, group.DestinationName, replaceExisting, stagingFolder =>
			{
				foreach (var entry in group.Entries)
				{
					var entryName = Path.GetFileName(entry.Key.Replace('/', Path.DirectorySeparatorChar));
					var extension = Path.GetExtension(entryName);
					if (!SupportedExtensions.Contains(extension)) continue;
					var destination = Path.Combine(stagingFolder, entryName);
					using var input = entry.OpenEntryStream();
					using var output = new FileStream(destination, FileMode.CreateNew, FileAccess.Write, FileShare.None);
					input.CopyTo(output);
				}
			});
			imported.Add(group.DestinationName);
		}
		return imported;
	}

	private static IReadOnlyList<string> ImportFolder(string sourceFolder, string storyFolder, bool replaceExisting)
	{
		var files = Directory.EnumerateFiles(sourceFolder, "*", SearchOption.TopDirectoryOnly)
			.Where(path => SupportedExtensions.Contains(Path.GetExtension(path))).ToArray();
		var save = files.FirstOrDefault(path => Path.GetExtension(path).Equals(".lsv", StringComparison.OrdinalIgnoreCase));
		if (save == null) throw new InvalidDataException("The folder does not contain a BG3 .lsv save.");
		var name = NormalizeFolderName(Path.GetFileName(sourceFolder), save);
		ImportAtomically(storyFolder, name, replaceExisting, stagingFolder =>
		{
			foreach (var file in files) File.Copy(file, Path.Combine(stagingFolder, Path.GetFileName(file)), false);
		});
		return [name];
	}

	private static IReadOnlyList<string> ImportLooseSave(string savePath, string storyFolder, bool replaceExisting)
	{
		var name = NormalizeFolderName(String.Empty, savePath);
		ImportAtomically(storyFolder, name, replaceExisting, stagingFolder =>
		{
			File.Copy(savePath, Path.Combine(stagingFolder, Path.GetFileName(savePath)), false);
			var thumbnail = Path.ChangeExtension(savePath, ".WebP");
			if (!File.Exists(thumbnail)) thumbnail = Path.ChangeExtension(savePath, ".webp");
			if (File.Exists(thumbnail)) File.Copy(thumbnail, Path.Combine(stagingFolder, Path.GetFileName(thumbnail)), false);
		});
		return [name];
	}

	private static void ImportAtomically(string storyFolder, string folderName, bool replaceExisting, Action<string> write)
	{
		var destination = Path.Combine(storyFolder, folderName);
		if (Directory.Exists(destination) && !replaceExisting)
			throw new IOException($"A save named '{folderName}' is already installed.");
		var staging = Path.Combine(storyFolder, $".redux-import-{Guid.NewGuid():N}");
		var backup = Path.Combine(storyFolder, $".redux-replaced-{Guid.NewGuid():N}");
		Directory.CreateDirectory(staging);
		var movedExisting = false;
		try
		{
			write(staging);
			if (!Directory.EnumerateFiles(staging, "*.lsv", SearchOption.TopDirectoryOnly).Any())
				throw new InvalidDataException("The imported save did not produce an .lsv file.");
			if (Directory.Exists(destination))
			{
				Directory.Move(destination, backup);
				movedExisting = true;
			}
			Directory.Move(staging, destination);
			if (movedExisting)
			{
				try { Directory.Delete(backup, true); }
				catch (IOException) { /* The replacement is committed; stale backup cleanup is non-fatal. */ }
				catch (UnauthorizedAccessException) { /* The replacement is committed; stale backup cleanup is non-fatal. */ }
			}
		}
		catch
		{
			if (Directory.Exists(staging)) Directory.Delete(staging, true);
			if (movedExisting && !Directory.Exists(destination) && Directory.Exists(backup)) Directory.Move(backup, destination);
			throw;
		}
	}

	private static List<ArchiveSaveGroup> BuildArchiveGroups(IReadOnlyList<IArchiveEntry> archiveEntries)
	{
		var candidateEntries = archiveEntries.Where(entry => SupportedExtensions.Contains(Path.GetExtension(entry.Key))).ToArray();
		var totalBytes = 0L;
		foreach (var entry in candidateEntries)
		{
			if (entry.Size > MaximumEntryBytes || totalBytes > MaximumArchiveBytes - entry.Size)
				throw new InvalidDataException("The save archive is too large to install safely.");
			totalBytes += entry.Size;
		}

		foreach (var entry in archiveEntries)
		{
			var path = entry.Key.Replace('\\', '/');
			if (path.StartsWith('/') || path.Split('/').Any(segment => segment == ".."))
				throw new InvalidDataException("The archive contains an unsafe path.");
		}

		var lsvEntries = archiveEntries.Where(entry =>
			Path.GetExtension(entry.Key).Equals(".lsv", StringComparison.OrdinalIgnoreCase)).ToArray();
		if (lsvEntries.Length > MaximumSaveGroupsPerArchive)
			throw new InvalidDataException($"The archive contains more than {MaximumSaveGroupsPerArchive} save files.");
		var groups = new List<ArchiveSaveGroup>();
		var sourceFolders = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
		foreach (var lsv in lsvEntries)
		{
			var normalized = lsv.Key.Replace('\\', '/');
			var parent = normalized.Contains('/') ? normalized[..normalized.LastIndexOf('/')] : String.Empty;
			var parentName = parent.Contains('/') ? parent[(parent.LastIndexOf('/') + 1)..] : parent;
			var lsvName = Path.GetFileName(normalized);
			var destinationName = NormalizeFolderName(parentName, lsvName);
			if (sourceFolders.TryGetValue(destinationName, out var existingParent))
			{
				if (existingParent.Equals(parent, StringComparison.OrdinalIgnoreCase)) continue;
				throw new InvalidDataException($"The archive contains multiple save folders named '{destinationName}'.");
			}
			sourceFolders[destinationName] = parent;
			var entries = archiveEntries.Where(entry =>
			{
				var entryPath = entry.Key.Replace('\\', '/');
				var entryParent = entryPath.Contains('/') ? entryPath[..entryPath.LastIndexOf('/')] : String.Empty;
				return entryParent.Equals(parent, StringComparison.OrdinalIgnoreCase)
					&& SupportedExtensions.Contains(Path.GetExtension(entry.Key))
					&& (!String.IsNullOrEmpty(parentName) && !parentName.Equals("Story", StringComparison.OrdinalIgnoreCase)
						|| Path.GetFileNameWithoutExtension(entryPath).Equals(Path.GetFileNameWithoutExtension(lsvName), StringComparison.OrdinalIgnoreCase));
			}).ToArray();
			groups.Add(new ArchiveSaveGroup(destinationName, entries));
		}
		return groups;
	}

	private static FileStream OpenArchiveStream(string sourcePath) => new(
		sourcePath,
		FileMode.Open,
		FileAccess.Read,
		FileShare.Read,
		4096,
		FileOptions.SequentialScan);

	private static string NormalizeFolderName(string preferredName, string savePath)
	{
		var stem = Path.GetFileNameWithoutExtension(savePath);
		var candidate = String.IsNullOrWhiteSpace(preferredName) || preferredName.Equals("Story", StringComparison.OrdinalIgnoreCase)
			? $"Imported__{stem}"
			: preferredName;
		foreach (var invalid in Path.GetInvalidFileNameChars()) candidate = candidate.Replace(invalid, '_');
		candidate = candidate.Trim().TrimEnd('.');
		return String.IsNullOrWhiteSpace(candidate) ? $"Imported__{Guid.NewGuid():N}" : candidate;
	}

	private sealed record ArchiveSaveGroup(string DestinationName, IReadOnlyList<IArchiveEntry> Entries);
	private sealed record CachedSaveMetadata(long Length, DateTime ModifiedUtc, Bg3SaveDifficulty Difficulty);
}
