using DivinityModManager.Models;
using DivinityModManager.Models.Modio;
using DivinityModManager.Models.NexusMods;
using DivinityModManager.ModUpdater;
using DivinityModManager.Util;

using DynamicData;
using DynamicData.Binding;

using Newtonsoft.Json;

namespace DivinityModManager.ViewModels;

/// <summary>
/// Restores the in-memory library and provider state if a staged NXM PAK commit
/// cannot finish registration after its files have been moved.
/// </summary>
public sealed class NxmPakInstallStateSnapshot
{
	private sealed record ModPlacement(DivinityModData Mod, int Index, bool IsActive);
	private readonly IReadOnlyList<DivinityModData> _models;
	private readonly IReadOnlyList<ModPlacement> _active;
	private readonly IReadOnlyList<ModPlacement> _inactive;
	private readonly IReadOnlyDictionary<DivinityLoadOrder, IReadOnlyList<DivinityLoadOrderEntry>> _orders;
	private readonly IReadOnlyList<KeyValuePair<string, NexusModsModData>> _nexusCache;
	private readonly IReadOnlyList<KeyValuePair<string, ModioModData>> _modioCache;
	private readonly IReadOnlySet<string> _affectedUuids;

	private NxmPakInstallStateSnapshot(
		IReadOnlyList<DivinityModData> models,
		IReadOnlyList<ModPlacement> active,
		IReadOnlyList<ModPlacement> inactive,
		IReadOnlyDictionary<DivinityLoadOrder, IReadOnlyList<DivinityLoadOrderEntry>> orders,
		IReadOnlyList<KeyValuePair<string, NexusModsModData>> nexusCache,
		IReadOnlyList<KeyValuePair<string, ModioModData>> modioCache,
		IReadOnlySet<string> affectedUuids)
	{
		_models = models;
		_active = active;
		_inactive = inactive;
		_orders = orders;
		_nexusCache = nexusCache;
		_modioCache = modioCache;
		_affectedUuids = affectedUuids;
	}

	public static NxmPakInstallStateSnapshot Capture(
		SourceCache<DivinityModData, string> models,
		ObservableCollectionExtended<DivinityModData> active,
		ObservableCollectionExtended<DivinityModData> inactive,
		IEnumerable<DivinityLoadOrder> orders,
		ModUpdateHandler updates,
		IEnumerable<string> affectedUuids)
	{
		ArgumentNullException.ThrowIfNull(models);
		ArgumentNullException.ThrowIfNull(active);
		ArgumentNullException.ThrowIfNull(inactive);
		ArgumentNullException.ThrowIfNull(updates);
		var affected = (affectedUuids ?? [])
			.Where(uuid => !String.IsNullOrWhiteSpace(uuid))
			.ToHashSet(StringComparer.OrdinalIgnoreCase);
		var orderSnapshots = (orders ?? [])
			.Where(order => order != null)
			.ToDictionary(order => order, order => (IReadOnlyList<DivinityLoadOrderEntry>)
				(order.Order ?? []).Select(entry => entry?.Clone()).Where(entry => entry != null).ToArray());
		return new NxmPakInstallStateSnapshot(
			models.Items.ToArray(),
			active.Select(mod => new ModPlacement(mod, mod.Index, mod.IsActive)).ToArray(),
			inactive.Select(mod => new ModPlacement(mod, mod.Index, mod.IsActive)).ToArray(),
			orderSnapshots,
			SnapshotEntries(updates.Nexus.CacheData.Mods, affected),
			SnapshotEntries(updates.Modio.CacheData.Mods, affected),
			affected);
	}

	public void Restore(
		SourceCache<DivinityModData, string> models,
		ObservableCollectionExtended<DivinityModData> active,
		ObservableCollectionExtended<DivinityModData> inactive,
		IEnumerable<DivinityLoadOrder> orders,
		ModUpdateHandler updates)
	{
		ArgumentNullException.ThrowIfNull(models);
		ArgumentNullException.ThrowIfNull(active);
		ArgumentNullException.ThrowIfNull(inactive);
		ArgumentNullException.ThrowIfNull(updates);
		models.Edit(updater =>
		{
			updater.Clear();
			updater.AddOrUpdate(_models);
		});
		ObservableCollectionSynchronizer.Synchronize(active, _active.Select(placement => placement.Mod).ToArray(), ReferenceEquals);
		ObservableCollectionSynchronizer.Synchronize(inactive, _inactive.Select(placement => placement.Mod).ToArray(), ReferenceEquals);
		foreach (var placement in _active.Concat(_inactive))
		{
			placement.Mod.Index = placement.Index;
			placement.Mod.IsActive = placement.IsActive;
		}
		foreach (var order in orders ?? [])
		{
			if (_orders.TryGetValue(order, out var entries))
				order.Order = entries.Select(entry => entry.Clone()).ToList();
		}
		RestoreEntries(updates.Nexus.CacheData.Mods, _nexusCache, _affectedUuids);
		RestoreEntries(updates.Modio.CacheData.Mods, _modioCache, _affectedUuids);
	}

	private static IReadOnlyList<KeyValuePair<string, T>> SnapshotEntries<T>(
		IReadOnlyDictionary<string, T> entries, IReadOnlySet<string> affected) where T : class =>
		(entries ?? new Dictionary<string, T>()).Where(entry => affected.Contains(entry.Key))
			.Select(entry => new KeyValuePair<string, T>(entry.Key, Clone(entry.Value))).ToArray();

	private static void RestoreEntries<T>(Dictionary<string, T> target,
		IReadOnlyList<KeyValuePair<string, T>> snapshot, IReadOnlySet<string> affected) where T : class
	{
		foreach (var key in target.Keys.Where(affected.Contains).ToArray()) target.Remove(key);
		foreach (var entry in snapshot) target[entry.Key] = Clone(entry.Value);
	}

	private static T Clone<T>(T value) where T : class
	{
		if (value == null) return null;
		var json = JsonConvert.SerializeObject(value, ModUpdateHandler.DefaultSerializerSettings);
		return JsonConvert.DeserializeObject<T>(json, ModUpdateHandler.DefaultSerializerSettings);
	}
}
