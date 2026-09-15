using DivinityModManager.Util;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace DivinityModManager.AppServices;

public sealed record NexusUpdateAcknowledgement(string Uuid, long ModId, long FileId,
	string PackageSha256, string FileName, string Version, DateTimeOffset ChosenUtc);

/// <summary>User references are separate from installed provenance and disposable API cache.</summary>
public sealed class NexusUpdateAcknowledgements
{
	private readonly string _path;
	public string Warning { get; private set; }
	public NexusUpdateAcknowledgements(string path) => _path = Path.GetFullPath(path);
	public static bool IsHash(string value) => value?.Length == 64 && value.All(Uri.IsHexDigit);

	public IReadOnlyList<NexusUpdateAcknowledgement> Read()
	{
		try { var entries = Load(); Warning = null; return entries; }
		catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException or Newtonsoft.Json.JsonException)
		{
			Warning = "Saved reference releases could not be read. They have not been overwritten; comparisons below do not use them.";
			return Array.Empty<NexusUpdateAcknowledgement>();
		}
	}

	public static NexusUpdateAcknowledgement Find(IEnumerable<NexusUpdateAcknowledgement> entries, NexusUpdateInstalledFile file) =>
		IsHash(file.PackageSha256) ? entries.FirstOrDefault(entry => entry.ModId == file.ModId
			&& String.Equals(entry.Uuid, file.Uuid, StringComparison.OrdinalIgnoreCase)
			&& String.Equals(entry.PackageSha256, file.PackageSha256, StringComparison.OrdinalIgnoreCase)) : null;

	public void Set(NexusUpdateInstalledFile file, NexusRemoteFile reference)
	{
		if (!Guid.TryParse(file.Uuid, out _) || file.ModId <= 0 || reference.FileId <= 0 || !IsHash(file.PackageSha256)
			|| reference.Name?.Length > 1024 || reference.Version?.Length > 500)
			throw new InvalidDataException("A reference requires an identified local package and a Nexus file.");
		Change(file, new(file.Uuid, file.ModId, reference.FileId, file.PackageSha256,
			reference.Name, reference.Version, DateTimeOffset.UtcNow));
	}

	public void Reset(NexusUpdateInstalledFile file) => Change(file, null);

	private void Change(NexusUpdateInstalledFile file, NexusUpdateAcknowledgement replacement)
	{
		Directory.CreateDirectory(Path.GetDirectoryName(_path));
		using var lease = new FileStream(_path + ".lock", FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
		var entries = Load(); // Refuse to replace unreadable preferences with an empty store.
		entries.RemoveAll(entry => entry.ModId == file.ModId && String.Equals(entry.Uuid, file.Uuid, StringComparison.OrdinalIgnoreCase));
		if (replacement != null) entries.Add(replacement);
		if (entries.Count > 4096) throw new InvalidDataException("Too many saved reference releases.");
		var json = JsonConvert.SerializeObject(new Store { Entries = entries }, Formatting.Indented);
		if (System.Text.Encoding.UTF8.GetByteCount(json) > 2 * 1024 * 1024) throw new InvalidDataException("Reference store is too large.");
		AtomicFileWriter.WriteAllText(_path, json, _path + ".bak");
		Warning = null;
	}

	private List<NexusUpdateAcknowledgement> Load()
	{
		if (!File.Exists(_path)) return new();
		if (new FileInfo(_path).Length > 2 * 1024 * 1024) throw new InvalidDataException();
		var root = JObject.Parse(File.ReadAllText(_path), new JsonLoadSettings { DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error });
		var store = root.ToObject<Store>();
		if (store?.SchemaVersion != 1 || store.Entries == null || store.Entries.Count > 4096) throw new InvalidDataException();
		var keys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		foreach (var entry in store.Entries)
		{
			if (entry == null || !Guid.TryParse(entry.Uuid, out _) || entry.ModId <= 0 || entry.FileId <= 0
				|| !IsHash(entry.PackageSha256) || entry.FileName?.Length > 1024 || entry.Version?.Length > 500
				|| entry.ChosenUtc < DateTimeOffset.UnixEpoch || !keys.Add(entry.Uuid + ":" + entry.ModId)) throw new InvalidDataException();
		}
		return store.Entries;
	}

	private sealed class Store
	{
		public int SchemaVersion { get; set; } = 1;
		public List<NexusUpdateAcknowledgement> Entries { get; set; } = new();
	}
}
