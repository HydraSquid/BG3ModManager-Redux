using DivinityModManager.Models;
using DivinityModManager.Models.NexusMods;

namespace DivinityModManager.AppServices;

public sealed record NxmInstallCandidate(NxmDownloadItem Download, IReadOnlyList<DivinityModData> Modules);

public sealed record NxmBatchInstallPlan(
	IReadOnlyList<NxmInstallCandidate> Ordered,
	IReadOnlyDictionary<NxmDownloadItem, string> Blocked,
	IReadOnlyDictionary<NxmDownloadItem, IReadOnlyList<NxmDownloadItem>> Prerequisites);

public static class NxmBatchInstallPlanner
{
	public static NxmBatchInstallPlan Build(IReadOnlyList<NxmInstallCandidate> candidates, IEnumerable<DivinityModData> installedMods, bool nativeLoaderPresent = false)
	{
		var installed = installedMods.Where(mod => mod != null && !mod.IsVisualDivider).ToArray();
		var blocked = new Dictionary<NxmDownloadItem, string>();
		var prerequisites = candidates.ToDictionary(candidate => candidate.Download, _ => new HashSet<NxmDownloadItem>());
		var modules = candidates.SelectMany(candidate => candidate.Modules.Select(mod => (Candidate: candidate, Mod: mod))).ToArray();
		var providers = modules.Where(entry => Uuid(entry.Mod.UUID) != null).ToLookup(entry => Uuid(entry.Mod.UUID));
		var native = candidates.Where(candidate => NativeModCatalog.Find(candidate.Download.ModId) != null).ToArray();
		foreach (var duplicates in native.GroupBy(candidate => candidate.Download.ModId).Where(group => group.Count() > 1))
			foreach (var candidate in duplicates)
				blocked[candidate.Download] = "Multiple archives for the same supported native mod are selected. Select only the intended version.";
		foreach (var group in providers.Where(group => group.Count() > 1))
			foreach (var entry in group)
				blocked[entry.Candidate.Download] = $"Multiple selected packages provide UUID {group.Key}. Select one version or variant, then retry.";
		foreach (var group in modules.Where(entry => !String.IsNullOrWhiteSpace(entry.Mod.FileName))
			.GroupBy(entry => entry.Mod.FileName, StringComparer.OrdinalIgnoreCase)
			.Where(group => group.Select(entry => entry.Candidate.Download).Distinct().Count() > 1))
			foreach (var entry in group)
				blocked[entry.Candidate.Download] = "Selected archives contain the same package filename. Review the conflicting files and select only the intended package.";

		foreach (var candidate in candidates)
		{
			var definition = NativeModCatalog.Find(candidate.Download.ModId);
			if (definition != null)
			{
				if (definition.RequiresLoader)
				{
					var loaders = native.Where(entry => entry.Download.ModId == 944).ToArray();
					if (loaders.Length == 1) prerequisites[candidate.Download].Add(loaders[0].Download);
					else if (loaders.Length > 1 || !nativeLoaderPresent)
						blocked[candidate.Download] = "Native Mod Loader is missing, changed, or ambiguous. Select one supported loader archive or install it through Native Mods tools first.";
				}
				continue;
			}
			if (candidate.Modules.Count == 0) blocked[candidate.Download] = "No readable packages were identified in this archive.";
			foreach (var mod in candidate.Modules)
			{
				foreach (var dependency in mod.Dependencies.Items)
				{
					var uuid = Uuid(dependency.UUID);
					var label = String.IsNullOrWhiteSpace(dependency.Name) ? uuid : $"{dependency.Name} ({uuid})";
					if (uuid == null || uuid == Uuid(mod.UUID))
					{
						blocked[candidate.Download] = "A package declares an invalid or self-referencing dependency UUID. Review its metadata.";
						continue;
					}
					var matches = providers[uuid].ToArray();
					if (matches.Length > 1)
						blocked[candidate.Download] = $"Prerequisite {label} has multiple selected providers. Select one version or variant.";
					else if (matches.Length == 1)
					{
						var provider = matches[0];
						if (!Satisfies(provider.Mod, dependency))
							blocked[candidate.Download] = $"Selected prerequisite {label} is older than required version {dependency.Version}.";
						else if (provider.Candidate.Download != candidate.Download)
							prerequisites[candidate.Download].Add(provider.Candidate.Download);
					}
					else if (!installed.Any(mod => Uuid(mod.UUID) == uuid && Satisfies(mod, dependency)))
						blocked[candidate.Download] = $"Prerequisite {label} is missing or too old, and no selected archive supplies it. Inspect and select its download, or install the required version first.";
				}
			}
		}

		// Remove known blockers and their dependents, then take ready archives in
		// selection order. Every iteration removes an item or stops: no recursion.
		var pending = candidates.ToList();
		var ordered = new List<NxmInstallCandidate>();
		var ready = new HashSet<NxmDownloadItem>();
		while (pending.Count > 0)
		{
			var excluded = pending.FirstOrDefault(candidate => blocked.ContainsKey(candidate.Download)
				|| prerequisites[candidate.Download].Any(blocked.ContainsKey));
			if (excluded != null)
			{
				blocked.TryAdd(excluded.Download, "A selected prerequisite is blocked. Resolve its reported issue before installing this archive.");
				pending.Remove(excluded);
				continue;
			}
			var next = pending.FirstOrDefault(candidate => prerequisites[candidate.Download].All(ready.Contains));
			if (next == null)
			{
				foreach (var candidate in pending)
					blocked[candidate.Download] = "Circular dependencies between selected archives, or a dependency on such a cycle, prevent a prerequisite-first installation. Review the authors' instructions.";
				break;
			}
			pending.Remove(next);
			ready.Add(next.Download);
			ordered.Add(next);
		}
		return new(ordered, blocked, prerequisites.ToDictionary(pair => pair.Key,
			pair => (IReadOnlyList<NxmDownloadItem>)pair.Value.ToArray()));
	}

	public static string GetExecutionBlockReason(NxmBatchInstallPlan plan, NxmInstallCandidate candidate,
		ISet<NxmDownloadItem> successful, IEnumerable<DivinityModData> installedMods, bool nativeLoaderPresent = false)
	{
		if (plan.Prerequisites[candidate.Download].Any(prerequisite => !successful.Contains(prerequisite)))
			return "A selected prerequisite was skipped or failed. Its dependent archives were not installed.";
		return Build([candidate], installedMods, nativeLoaderPresent).Blocked.GetValueOrDefault(candidate.Download);
	}

	private static string Uuid(string value) => Guid.TryParse(value, out var uuid) && uuid != Guid.Empty ? uuid.ToString("D") : null;
	private static bool Satisfies(DivinityModData mod, ModuleShortDesc dependency) =>
		(mod.Version?.VersionInt ?? 0) >= (dependency.Version?.VersionInt ?? 0);
}
