using DivinityModManager.Models;
using DivinityModManager.Models.NexusMods;
using DivinityModManager.Models.Modio;
using DivinityModManager.Util;

using Newtonsoft.Json;

using System.Buffers.Binary;
using System.IO.Hashing;
using System.Security.Cryptography;
using System.Text;

namespace DivinityModManager.AppServices;

/// <summary>
/// Resolves Nexus project identity from Redux's bundled mod database.
/// Installed pak and downloaded archive fingerprints are deliberately indexed
/// separately because they represent different bytes and hash algorithms.
/// </summary>
public static class ReduxModDatabaseService
{
	private const string DATABASE_FILE_NAME = "ReduxModDatabase.json";
	private static readonly Lazy<ReduxModDatabaseIndex> _index = new(LoadIndex, true);
	private static readonly Lazy<ReduxLoadOrderAdvisorKnowledge> _loadOrderAdvisorKnowledge = new(LoadLoadOrderAdvisorKnowledge, true);

	public static int ExactPakCount => _index.Value.ExactPakCount;
	public static int ExactArchiveCount => _index.Value.ExactArchiveCount;
	public static ReduxLoadOrderAdvisorKnowledge LoadOrderAdvisorKnowledge => _loadOrderAdvisorKnowledge.Value;
	public static ReduxModDatabaseMatch TryResolveProject(long modId) => CreateMatch(modId, -1, ReduxOfflineMatchKind.Unknown);
	public static ReduxModDatabaseMatch TryResolveFile(long modId, long fileId)
	{
		return modId > 0 && fileId > 0 && _index.Value.FingerprintsByFile.TryGetValue((modId, fileId), out var fingerprint)
			? CreateMatch(modId, fileId, ReduxOfflineMatchKind.ExactArchive, fingerprint)
			: null;
	}
	public static ReduxModDatabaseMatch TryResolveModuleUuid(string uuid)
	{
		if (String.IsNullOrWhiteSpace(uuid)) return null;
		var key = uuid.Trim();
		if (_index.Value.ModulesByUuid.TryGetValue(key, out var reviewed))
			return CreateMatch(reviewed.ModId, -1, ReduxOfflineMatchKind.ModuleIdentity);
		return _index.Value.CommunityModulesByUuid.TryGetValue(key, out var community)
			? CreateMatch(community.ModId, -1, ReduxOfflineMatchKind.CommunityIdentity)
			: null;
	}

	public static bool CouldMatchPak(string filePath) => CouldMatchBySize(filePath, ".pak", _index.Value.PaksBySize);
	public static bool CouldMatchArchive(string filePath) => CouldMatchBySize(filePath, null, _index.Value.ArchivesBySize);

	public static async Task<ReduxModDatabaseMatch> TryResolvePakAsync(string filePath, CancellationToken cancellationToken)
	{
		if (!CouldMatchPak(filePath)) return null;
		var file = new FileInfo(filePath);
		var originalLength = file.Length;
		if (!_index.Value.PaksBySize.TryGetValue(originalLength, out var candidates)) return null;

		var hash = await ComputeExactPakHashAsync(filePath, cancellationToken);
		file.Refresh();
		if (!file.Exists || file.Length != originalLength)
			throw new IOException($"The pak changed while Redux was identifying it: {filePath}");

		return candidates.TryGetValue(hash, out var fingerprint)
			? CreateMatch(fingerprint.ModId, fingerprint.FileId, ReduxOfflineMatchKind.ExactPak, fingerprint)
			: null;
	}

	public static async Task<ReduxModDatabaseMatch> TryResolveArchiveAsync(string filePath, CancellationToken cancellationToken)
	{
		if (!CouldMatchArchive(filePath)) return null;
		var file = new FileInfo(filePath);
		var originalLength = file.Length;
		if (!_index.Value.ArchivesBySize.TryGetValue(originalLength, out var candidates)) return null;

		var md5 = await ComputeArchiveMd5Async(filePath, cancellationToken);
		file.Refresh();
		if (!file.Exists || file.Length != originalLength)
			throw new IOException($"The archive changed while Redux was identifying it: {filePath}");

		return candidates.TryGetValue(md5, out var fingerprint)
			? CreateMatch(fingerprint.ModId, fingerprint.FileId, ReduxOfflineMatchKind.ExactArchive, fingerprint)
			: null;
	}

	/// <summary>
	/// Returns a conservative project-level association. Reviewed UUID identities are
	/// authoritative. Community identities also require an installed package name match;
	/// otherwise normalized name and author must converge on one reviewed project.
	/// </summary>
	public static ReduxModDatabaseMatch TryResolveIdentity(DivinityModData mod)
	{
		if (mod == null) return null;
		if (!String.IsNullOrWhiteSpace(mod.UUID)
			&& _index.Value.ModulesByUuid.TryGetValue(mod.UUID.Trim(), out var reviewed))
			return CreateMatch(reviewed.ModId, -1, ReduxOfflineMatchKind.ModuleIdentity);
		if (!String.IsNullOrWhiteSpace(mod.UUID)
			&& _index.Value.CommunityModulesByUuid.TryGetValue(mod.UUID.Trim(), out var community)
			&& CommunityIdentityMatches(mod, community))
			return CreateMatch(community.ModId, -1, ReduxOfflineMatchKind.CommunityIdentity);

		var author = Normalize(mod.Author);
		if (String.IsNullOrWhiteSpace(author)) return null;

		var candidateIds = new HashSet<long>();
		foreach (var value in new[] { mod.Name, mod.DisplayName, mod.Folder, Path.GetFileNameWithoutExtension(mod.FileName) })
		{
			var normalized = Normalize(value);
			if (!String.IsNullOrWhiteSpace(normalized)
				&& _index.Value.ProjectIdsByAlias.TryGetValue(normalized, out var ids))
			{
				candidateIds.UnionWith(ids);
			}
		}

		var agreed = candidateIds
			.Where(id => _index.Value.ProjectAuthors.TryGetValue(id, out var authors) && authors.Contains(author))
			.Distinct()
			.ToList();
		return agreed.Count == 1
			? CreateMatch(agreed[0], -1, ReduxOfflineMatchKind.NameAndAuthor)
			: null;
	}

	/// <summary>
	/// Resolves a conservative mod.io candidate. These records are deliberately
	/// separate from Nexus records, and the catalog sync omits names present on
	/// both providers so Redux never guesses between storefronts.
	/// </summary>
	public static ReduxModioDatabaseMatch TryResolveModioIdentity(DivinityModData mod)
	{
		if (mod == null || String.IsNullOrWhiteSpace(mod.UUID)) return null;
		if (!_index.Value.CommunityModioModulesByUuid.TryGetValue(mod.UUID.Trim(), out var identity)
			|| !CommunityModioIdentityMatches(mod, identity)
			|| !_index.Value.ModioProjectsById.TryGetValue(identity.ModId, out var project))
			return null;
		return new ReduxModioDatabaseMatch(project);
	}

	private static bool CommunityIdentityMatches(DivinityModData mod, ReduxModuleIdentity identity)
	{
		var knownNames = new[] { identity.Name, identity.Folder }
			.Concat(identity.Aliases ?? new List<string>())
			.Select(Normalize)
			.Where(value => value.Length >= 4)
			.ToHashSet(StringComparer.Ordinal);
		var localNames = new[] { mod.Name, mod.DisplayName, mod.Folder, Path.GetFileNameWithoutExtension(mod.FileName) }
			.Select(Normalize)
			.Where(value => value.Length >= 4);
		if (!localNames.Any(knownNames.Contains)) return false;

		var knownAuthors = (identity.Authors ?? new List<string>())
			.Select(Normalize)
			.Where(value => value.Length > 0)
			.ToHashSet(StringComparer.Ordinal);
		var localAuthor = Normalize(mod.Author);
		return localAuthor.Length == 0 || knownAuthors.Count == 0 || knownAuthors.Contains(localAuthor);
	}

	private static bool CommunityModioIdentityMatches(DivinityModData mod, ReduxModioModuleIdentity identity)
	{
		var knownNames = new[] { identity.Name, identity.Folder }
			.Concat(identity.Aliases ?? new List<string>())
			.Select(Normalize).Where(value => value.Length >= 4).ToHashSet(StringComparer.Ordinal);
		var localNames = new[] { mod.Name, mod.DisplayName, mod.Folder, Path.GetFileNameWithoutExtension(mod.FileName) }
			.Select(Normalize).Where(value => value.Length >= 4);
		if (!localNames.Any(knownNames.Contains)) return false;
		var knownAuthors = (identity.Authors ?? new List<string>()).Select(Normalize)
			.Where(value => value.Length > 0).ToHashSet(StringComparer.Ordinal);
		var localAuthor = Normalize(mod.Author);
		return localAuthor.Length == 0 || knownAuthors.Count == 0 || knownAuthors.Contains(localAuthor);
	}

	internal static async Task<string> ComputeExactPakHashAsync(string filePath, CancellationToken cancellationToken)
	{
		await using var stream = OpenSequentialRead(filePath);
		var hasher = new XxHash64();
		await hasher.AppendAsync(stream, cancellationToken);
		var littleEndianHash = new byte[sizeof(ulong)];
		BinaryPrimitives.WriteUInt64LittleEndian(littleEndianHash, hasher.GetCurrentHashAsUInt64());
		return Convert.ToBase64String(littleEndianHash);
	}

	internal static async Task<string> ComputeArchiveMd5Async(string filePath, CancellationToken cancellationToken)
	{
		await using var stream = OpenSequentialRead(filePath);
		var hash = await MD5.HashDataAsync(stream, cancellationToken);
		return Convert.ToHexString(hash).ToLowerInvariant();
	}

	private static FileStream OpenSequentialRead(string filePath) => new(
		filePath, FileMode.Open, FileAccess.Read, FileShare.Read, 1024 * 1024,
		FileOptions.Asynchronous | FileOptions.SequentialScan);

	private static bool CouldMatchBySize<T>(string filePath, string requiredExtension, Dictionary<long, Dictionary<string, T>> index)
	{
		if (String.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath)) return false;
		if (requiredExtension != null && !filePath.EndsWith(requiredExtension, StringComparison.OrdinalIgnoreCase)) return false;
		try { return index.ContainsKey(new FileInfo(filePath).Length); }
		catch { return false; }
	}

	private static ReduxModDatabaseMatch CreateMatch(long modId, long fileId, ReduxOfflineMatchKind kind, IReduxFingerprint fingerprint = null)
	{
		if (!_index.Value.ProjectsById.TryGetValue(modId, out var project)) return null;
		return new ReduxModDatabaseMatch(project, fileId, kind, fingerprint);
	}

	private static string Normalize(string value)
	{
		if (String.IsNullOrWhiteSpace(value)) return String.Empty;
		var builder = new StringBuilder(value.Length);
		foreach (var character in value.Normalize(NormalizationForm.FormD))
		{
			if (Char.IsLetterOrDigit(character)) builder.Append(Char.ToLowerInvariant(character));
		}
		return builder.ToString();
	}

	private static ReduxModDatabaseIndex LoadIndex()
	{
		try
		{
			var path = Path.Combine(AppContext.BaseDirectory, "Resources", DATABASE_FILE_NAME);
			if (!File.Exists(path))
			{
				DivinityApp.Log($"Bundled Redux mod database was not found at '{path}'.");
				return ReduxModDatabaseIndex.Empty;
			}
			var database = JsonConvert.DeserializeObject<ReduxModDatabase>(File.ReadAllText(path));
			if (database?.SchemaVersion != 1) return ReduxModDatabaseIndex.Empty;
			var index = new ReduxModDatabaseIndex(database);
			DivinityApp.Log($"Loaded Redux mod database: {index.ExactPakCount} exact paks, {index.ExactArchiveCount} exact archives, {index.ProjectsById.Count} Nexus projects.");
			return index;
		}
		catch (Exception ex)
		{
			DivinityApp.Log($"Failed to load the bundled Redux mod database:\n{ex}");
			return ReduxModDatabaseIndex.Empty;
		}
	}

	private static ReduxLoadOrderAdvisorKnowledge LoadLoadOrderAdvisorKnowledge()
	{
		try
		{
			var path = Path.Combine(AppContext.BaseDirectory, "Resources", DATABASE_FILE_NAME);
			if (!File.Exists(path))
			{
				DivinityApp.Log($"Bundled Redux mod database was not found at '{path}'. Load Order Advisor database guidance is unavailable.");
				return ReduxLoadOrderAdvisorKnowledge.Empty;
			}

			var database = JsonConvert.DeserializeObject<ReduxLoadOrderAdvisorDatabase>(File.ReadAllText(path));
			if (database?.SchemaVersion != 1) return ReduxLoadOrderAdvisorKnowledge.Empty;
			var knowledge = ReduxLoadOrderAdvisorKnowledge.Create(
				database.OrderingGroups,
				database.DependencyNameAliases,
				database.DependencySubstitutes,
				database.LoadOrderEntries);
			DivinityApp.Log(
				$"Loaded Redux Load Order Advisor knowledge: {knowledge.EntryCount} mod entries, " +
				$"{knowledge.GroupCount} ordering groups, {knowledge.DependencyAliasCount} dependency aliases, " +
				$"and {knowledge.DependencySubstituteCount} dependency substitutions.");
			return knowledge;
		}
		catch (Exception ex)
		{
			DivinityApp.Log($"Failed to load Redux Load Order Advisor knowledge:\n{ex}");
			return ReduxLoadOrderAdvisorKnowledge.Empty;
		}
	}

	private sealed class ReduxModDatabaseIndex
	{
		public static ReduxModDatabaseIndex Empty { get; } = new(new ReduxModDatabase());
		public Dictionary<long, ReduxProjectRecord> ProjectsById { get; }
		public Dictionary<long, ReduxModioProjectRecord> ModioProjectsById { get; }
		public Dictionary<long, Dictionary<string, ReduxPakFingerprint>> PaksBySize { get; }
		public Dictionary<long, Dictionary<string, ReduxArchiveFingerprint>> ArchivesBySize { get; }
		public Dictionary<string, ReduxModuleIdentity> ModulesByUuid { get; }
		public Dictionary<string, ReduxModuleIdentity> CommunityModulesByUuid { get; }
		public Dictionary<string, ReduxModioModuleIdentity> CommunityModioModulesByUuid { get; }
		public Dictionary<string, HashSet<long>> ProjectIdsByAlias { get; } = new(StringComparer.Ordinal);
		public Dictionary<long, HashSet<string>> ProjectAuthors { get; } = new();
		public Dictionary<(long ModId, long FileId), IReduxFingerprint> FingerprintsByFile { get; }
		public int ExactPakCount => PaksBySize.Values.Sum(group => group.Count);
		public int ExactArchiveCount => ArchivesBySize.Values.Sum(group => group.Count);

		public ReduxModDatabaseIndex(ReduxModDatabase database)
		{
			ProjectsById = (database.Projects ?? new()).Where(p => p.ModId > 0).GroupBy(p => p.ModId).ToDictionary(g => g.Key, g => g.First());
			ModioProjectsById = (database.ModioProjects ?? new()).Where(p => p.ModId > 0).GroupBy(p => p.ModId).ToDictionary(g => g.Key, g => g.First());
			PaksBySize = UniqueFingerprintIndex(database.ExactPakFingerprints, item => item.Size, item => item.Hash);
			ArchivesBySize = UniqueFingerprintIndex(database.ExactArchiveFingerprints, item => item.Size, item => item.Md5?.ToLowerInvariant());
			FingerprintsByFile = (database.ExactArchiveFingerprints ?? new List<ReduxArchiveFingerprint>())
				.Cast<IReduxFingerprint>()
				.Concat((database.ExactPakFingerprints ?? new List<ReduxPakFingerprint>()).Cast<IReduxFingerprint>())
				.Where(item => item.ModId > 0 && item.FileId > 0)
				.GroupBy(item => (item.ModId, item.FileId))
				.ToDictionary(group => group.Key, group => group.First());
			ModulesByUuid = (database.ModuleIdentities ?? new()).Where(m => !String.IsNullOrWhiteSpace(m.Uuid)).GroupBy(m => m.Uuid, StringComparer.OrdinalIgnoreCase).Where(g => g.Select(m => m.ModId).Distinct().Count() == 1).ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);
			CommunityModulesByUuid = (database.CommunityModuleIdentities ?? new())
				.Where(m => !String.IsNullOrWhiteSpace(m.Uuid) && String.Equals(m.MatchBasis, "community-exact-name", StringComparison.Ordinal))
				.GroupBy(m => m.Uuid, StringComparer.OrdinalIgnoreCase)
				.Where(group => group.Select(m => m.ModId).Distinct().Count() == 1 && !ModulesByUuid.ContainsKey(group.Key))
				.ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);
			CommunityModioModulesByUuid = (database.CommunityModioIdentities ?? new())
				.Where(m => !String.IsNullOrWhiteSpace(m.Uuid) && String.Equals(m.MatchBasis, "community-exact-name", StringComparison.Ordinal))
				.GroupBy(m => m.Uuid, StringComparer.OrdinalIgnoreCase)
				.Where(group => group.Select(m => m.ModId).Distinct().Count() == 1)
				.ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);
			var reviewedProjectIds = (database.ExactPakFingerprints ?? new()).Select(item => item.ModId)
				.Concat((database.ExactArchiveFingerprints ?? new()).Select(item => item.ModId))
				.Concat((database.ModuleIdentities ?? new()).Select(item => item.ModId))
				.ToHashSet();
			foreach (var project in ProjectsById.Values.Where(project => reviewedProjectIds.Contains(project.ModId)))
			{
				foreach (var alias in new[] { project.Name }.Concat(project.Aliases ?? new()))
				{
					var key = Normalize(alias);
					if (key.Length < 4) continue;
					if (!ProjectIdsByAlias.TryGetValue(key, out var ids)) ProjectIdsByAlias[key] = ids = new();
					ids.Add(project.ModId);
				}
				ProjectAuthors[project.ModId] = (project.Authors ?? new()).Select(Normalize).Where(a => a.Length > 0).ToHashSet(StringComparer.Ordinal);
			}
		}

		private static Dictionary<long, Dictionary<string, T>> UniqueFingerprintIndex<T>(IEnumerable<T> values, Func<T, long> size, Func<T, string> hash)
		{
			return (values ?? Enumerable.Empty<T>()).Where(v => size(v) > 0 && !String.IsNullOrWhiteSpace(hash(v)))
				.GroupBy(size).ToDictionary(g => g.Key, g => g.GroupBy(hash, StringComparer.OrdinalIgnoreCase)
				.Where(matches => matches.Count() == 1).ToDictionary(matches => matches.Key, matches => matches.Single(), StringComparer.OrdinalIgnoreCase));
		}
	}
}

public enum ReduxOfflineMatchKind { Unknown = 0, ExactPak = 1, ExactArchive = 2, ModuleIdentity = 3, NameAndAuthor = 4, CommunityIdentity = 5 }

public sealed class ReduxModDatabaseMatch
{
	public ReduxProjectRecord Project { get; }
	public long FileId { get; }
	public ReduxOfflineMatchKind Kind { get; }
	private readonly IReduxFingerprint _fingerprint;
	public long ModId => Project.ModId;

	internal ReduxModDatabaseMatch(ReduxProjectRecord project, long fileId, ReduxOfflineMatchKind kind, IReduxFingerprint fingerprint)
	{ Project = project; FileId = fileId; Kind = kind; _fingerprint = fingerprint; }

	public NexusModsModData CreateMetadata(string uuid)
	{
		Uri.TryCreate(_fingerprint?.PictureUrl ?? Project.PictureUrl, UriKind.Absolute, out var picture);
		var categoryId = (Project.Categories ?? new List<string>())
			.Select(category => AutomaticModCategoryClassifier.TryGetNexusCategoryId(category, out var id) ? id : 0)
			.FirstOrDefault(id => id > 0);
		return new NexusModsModData
		{
			UUID = uuid, ModId = ModId, LastFileId = FileId,
			CategoryId = categoryId,
			Name = Project.Name,
			FileDisplayName = !String.IsNullOrWhiteSpace(_fingerprint?.LogicalFileName)
				? _fingerprint.LogicalFileName
				: _fingerprint?.Name,
			Author = !String.IsNullOrWhiteSpace(_fingerprint?.Author) ? _fingerprint.Author : Project.Authors?.FirstOrDefault(),
			UploadedBy = Project.UploadedBy,
			Version = _fingerprint?.Version, PictureUrl = picture, Available = true,
			MetadataOrigin = NexusMetadataOrigin.BundledProvenance,
			OfflineMatchKind = Kind
		};
	}
}

public sealed class ReduxModioDatabaseMatch
{
	private const long Bg3ModioGameId = 6715;
	public ReduxModioProjectRecord Project { get; }
	public long ModId => Project.ModId;
	internal ReduxModioDatabaseMatch(ReduxModioProjectRecord project) => Project = project;

	public ModioModData CreateMetadata(string uuid) => new()
	{
		UUID = uuid,
		ModId = Project.ModId,
		GameId = Bg3ModioGameId,
		NameId = Project.NameId,
		Name = Project.Name,
		ProfileUrl = $"https://mod.io/g/baldursgate3/m/{Project.NameId}",
		DateUpdated = Project.DateUpdated,
		SubmittedBy = new ModioUserData { Username = Project.Author, DisplayName = Project.Author },
		Tags = (Project.Tags ?? new List<string>()).Select(tag => new ModioTagData { Name = tag, LocalizedName = tag }).ToList(),
		MetadataOrigin = ModioMetadataOrigin.BundledProvenance
	};
}

internal interface IReduxFingerprint { long ModId { get; } long FileId { get; } string LogicalFileName { get; } string Name { get; } string Author { get; } string Version { get; } string PictureUrl { get; } }
internal sealed class ReduxModDatabase { [JsonProperty("schemaVersion")] public int SchemaVersion { get; set; } [JsonProperty("projects")] public List<ReduxProjectRecord> Projects { get; set; } = new(); [JsonProperty("modioProjects")] public List<ReduxModioProjectRecord> ModioProjects { get; set; } = new(); [JsonProperty("exactPakFingerprints")] public List<ReduxPakFingerprint> ExactPakFingerprints { get; set; } = new(); [JsonProperty("exactArchiveFingerprints")] public List<ReduxArchiveFingerprint> ExactArchiveFingerprints { get; set; } = new(); [JsonProperty("moduleIdentities")] public List<ReduxModuleIdentity> ModuleIdentities { get; set; } = new(); [JsonProperty("communityModuleIdentities")] public List<ReduxModuleIdentity> CommunityModuleIdentities { get; set; } = new(); [JsonProperty("communityModioIdentities")] public List<ReduxModioModuleIdentity> CommunityModioIdentities { get; set; } = new(); }
public sealed class ReduxProjectRecord { [JsonProperty("modId")] public long ModId { get; set; } [JsonProperty("name")] public string Name { get; set; } [JsonProperty("authors")] public List<string> Authors { get; set; } = new(); [JsonProperty("uploadedBy")] public string UploadedBy { get; set; } [JsonProperty("aliases")] public List<string> Aliases { get; set; } = new(); [JsonProperty("categories")] public List<string> Categories { get; set; } = new(); [JsonProperty("pictureUrl")] public string PictureUrl { get; set; } }
public sealed class ReduxModioProjectRecord { [JsonProperty("modId")] public long ModId { get; set; } [JsonProperty("nameId")] public string NameId { get; set; } [JsonProperty("name")] public string Name { get; set; } [JsonProperty("author")] public string Author { get; set; } [JsonProperty("aliases")] public List<string> Aliases { get; set; } = new(); [JsonProperty("tags")] public List<string> Tags { get; set; } = new(); [JsonProperty("dateUpdated")] public long DateUpdated { get; set; } }
internal sealed class ReduxPakFingerprint : IReduxFingerprint { [JsonProperty("hash")] public string Hash { get; set; } [JsonProperty("size")] public long Size { get; set; } [JsonProperty("modId")] public long ModId { get; set; } [JsonProperty("fileId")] public long FileId { get; set; } [JsonProperty("logicalFileName")] public string LogicalFileName { get; set; } [JsonProperty("name")] public string Name { get; set; } [JsonProperty("author")] public string Author { get; set; } [JsonProperty("version")] public string Version { get; set; } [JsonProperty("pictureUrl")] public string PictureUrl { get; set; } }
internal sealed class ReduxArchiveFingerprint : IReduxFingerprint { [JsonProperty("md5")] public string Md5 { get; set; } [JsonProperty("size")] public long Size { get; set; } [JsonProperty("modId")] public long ModId { get; set; } [JsonProperty("fileId")] public long FileId { get; set; } [JsonProperty("logicalFileName")] public string LogicalFileName { get; set; } [JsonProperty("name")] public string Name { get; set; } [JsonProperty("author")] public string Author { get; set; } [JsonProperty("version")] public string Version { get; set; } public string PictureUrl => null; }
internal sealed class ReduxModuleIdentity { [JsonProperty("uuid")] public string Uuid { get; set; } [JsonProperty("modId")] public long ModId { get; set; } [JsonProperty("name")] public string Name { get; set; } [JsonProperty("folder")] public string Folder { get; set; } [JsonProperty("aliases")] public List<string> Aliases { get; set; } = new(); [JsonProperty("authors")] public List<string> Authors { get; set; } = new(); [JsonProperty("matchBasis")] public string MatchBasis { get; set; } }
internal sealed class ReduxModioModuleIdentity { [JsonProperty("uuid")] public string Uuid { get; set; } [JsonProperty("modId")] public long ModId { get; set; } [JsonProperty("name")] public string Name { get; set; } [JsonProperty("folder")] public string Folder { get; set; } [JsonProperty("aliases")] public List<string> Aliases { get; set; } = new(); [JsonProperty("authors")] public List<string> Authors { get; set; } = new(); [JsonProperty("matchBasis")] public string MatchBasis { get; set; } }
