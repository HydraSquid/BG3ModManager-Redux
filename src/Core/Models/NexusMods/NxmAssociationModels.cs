using Microsoft.Win32;

namespace DivinityModManager.AppServices;

public enum NxmAssociationStatus
{
	Available,
	Owned,
	NeedsRepair,
	OwnedByAnotherHandler
}

public sealed record NxmAssociationResult(bool Success, NxmAssociationStatus Status, string Message, string CurrentHandler = null);

public sealed record NxmRegistryValue(object Value, RegistryValueKind Kind)
{
	public NxmRegistryValue DeepCopy() => new(CloneValue(Value), Kind);

	private static object CloneValue(object value) => value switch
	{
		byte[] bytes => bytes.ToArray(),
		string[] strings => strings.ToArray(),
		_ => value
	};
}

public sealed class NxmRegistryKeySnapshot
{
	public Dictionary<string, NxmRegistryValue> Values { get; } = new(StringComparer.OrdinalIgnoreCase);
	public Dictionary<string, NxmRegistryKeySnapshot> Subkeys { get; } = new(StringComparer.OrdinalIgnoreCase);

	public void SetValue(string subkeyPath, string name, object value, RegistryValueKind kind)
	{
		GetOrCreateSubkey(subkeyPath).Values[name ?? String.Empty] = new NxmRegistryValue(value, kind);
	}

	public NxmRegistryValue GetValue(string subkeyPath, string name)
	{
		var key = GetSubkey(subkeyPath);
		return key != null && key.Values.TryGetValue(name ?? String.Empty, out var value) ? value : null;
	}

	public string GetString(string subkeyPath, string name) => GetValue(subkeyPath, name)?.Value as string;

	public NxmRegistryKeySnapshot Clone()
	{
		var clone = new NxmRegistryKeySnapshot();
		foreach (var (name, value) in Values) clone.Values[name] = value.DeepCopy();
		foreach (var (name, subkey) in Subkeys) clone.Subkeys[name] = subkey.Clone();
		return clone;
	}

	private NxmRegistryKeySnapshot GetOrCreateSubkey(string path)
	{
		var current = this;
		foreach (var part in SplitPath(path))
		{
			if (!current.Subkeys.TryGetValue(part, out var next))
			{
				next = new NxmRegistryKeySnapshot();
				current.Subkeys[part] = next;
			}
			current = next;
		}
		return current;
	}

	private NxmRegistryKeySnapshot GetSubkey(string path)
	{
		var current = this;
		foreach (var part in SplitPath(path))
		{
			if (!current.Subkeys.TryGetValue(part, out current)) return null;
		}
		return current;
	}

	private static IEnumerable<string> SplitPath(string path) =>
		String.IsNullOrWhiteSpace(path)
			? Array.Empty<string>()
			: path.Split(new[] { '\\', '/' }, StringSplitOptions.RemoveEmptyEntries);
}

public sealed record NxmAssociationBackup(bool HadUserKey, NxmRegistryKeySnapshot UserKey, string PreviousCommand)
{
	public NxmAssociationBackup DeepCopy() => new(HadUserKey, UserKey?.Clone(), PreviousCommand);
}

public interface INxmRegistryStore
{
	NxmRegistryKeySnapshot ReadUserKey();
	NxmRegistryKeySnapshot ReadMachineKey();
	void WriteUserKey(NxmRegistryKeySnapshot key);
	void DeleteUserKey();
	NxmAssociationBackup ReadBackup(string ownerId);
	void WriteBackup(string ownerId, NxmAssociationBackup backup);
	void DeleteBackup(string ownerId);
}
