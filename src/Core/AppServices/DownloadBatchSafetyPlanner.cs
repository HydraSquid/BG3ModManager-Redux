namespace DivinityModManager.AppServices;

public sealed record DownloadBatchSafetyCandidate(
	string Id,
	string ArchiveSha256,
	IReadOnlyList<string> ConflictKeys,
	IReadOnlyList<string> ProvidedModUuids,
	IReadOnlyList<string> RequiredModUuids)
{
	public IReadOnlyDictionary<string, ulong> ProvidedModVersions { get; init; } =
		new Dictionary<string, ulong>(StringComparer.OrdinalIgnoreCase);
	public IReadOnlyDictionary<string, ulong> RequiredModVersions { get; init; } =
		new Dictionary<string, ulong>(StringComparer.OrdinalIgnoreCase);
}

public sealed record DownloadBatchSafetyPlan(
	IReadOnlyList<string> AcceptedIds,
	IReadOnlyDictionary<string, string> SkippedReasons)
{
	public IReadOnlyDictionary<string, IReadOnlyList<string>> PrerequisiteIds { get; init; } =
		new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal);

	public string GetExecutionBlockReason(string candidateId, ISet<string> successfulIds)
	{
		if (!PrerequisiteIds.TryGetValue(candidateId, out var prerequisites)) return null;
		return prerequisites.Any(prerequisite => successfulIds == null || !successfulIds.Contains(prerequisite))
			? "A selected prerequisite was skipped or failed. Its dependent package was not installed."
			: null;
	}
}

/// <summary>
/// Produces a deterministic, read-only batch plan. Earlier inbox entries win
/// destination conflicts; dependency loss cascades conservatively.
/// </summary>
public static class DownloadBatchSafetyPlanner
{
	public static DownloadBatchSafetyPlan Create(
		IEnumerable<DownloadBatchSafetyCandidate> source,
		IEnumerable<string> installedModUuids,
		IReadOnlyDictionary<string, ulong> installedModVersions = null)
	{
		var candidates = (source ?? []).Where(candidate => candidate != null).ToList();
		var installed = (installedModUuids ?? []).Where(uuid => !String.IsNullOrWhiteSpace(uuid))
			.ToHashSet(StringComparer.OrdinalIgnoreCase);
		foreach (var uuid in installedModVersions?.Keys ?? [])
			if (!String.IsNullOrWhiteSpace(uuid)) installed.Add(uuid);
		var skipped = new Dictionary<string, string>(StringComparer.Ordinal);

		RemoveUnsatisfiedDependencies(candidates, installed, installedModVersions, skipped);

		var accepted = new List<DownloadBatchSafetyCandidate>();
		var hashes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		var targets = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		foreach (var candidate in candidates)
		{
			var duplicateHash = !String.IsNullOrWhiteSpace(candidate.ArchiveSha256)
				&& hashes.Contains(candidate.ArchiveSha256);
			var overlappingTarget = candidate.ConflictKeys.Any(targets.Contains);
			if (duplicateHash || overlappingTarget)
			{
				skipped[candidate.Id] = "Another queued package targets the same mod or destination.";
				continue;
			}
			accepted.Add(candidate);
			if (!String.IsNullOrWhiteSpace(candidate.ArchiveSha256)) hashes.Add(candidate.ArchiveSha256);
			foreach (var key in candidate.ConflictKeys.Where(key => !String.IsNullOrWhiteSpace(key))) targets.Add(key);
		}

		RemoveUnsatisfiedDependencies(accepted, installed, installedModVersions, skipped);
		var prerequisites = BuildPrerequisites(accepted, installed, installedModVersions);
		var ordered = OrderPrerequisitesFirst(accepted, prerequisites, skipped);
		var acceptedIds = ordered.Select(candidate => candidate.Id).ToArray();
		return new DownloadBatchSafetyPlan(acceptedIds, skipped)
		{
			PrerequisiteIds = prerequisites
				.Where(pair => acceptedIds.Contains(pair.Key, StringComparer.Ordinal))
				.ToDictionary(pair => pair.Key, pair => (IReadOnlyList<string>)pair.Value.ToArray(), StringComparer.Ordinal)
		};
	}

	private static void RemoveUnsatisfiedDependencies(
		IList<DownloadBatchSafetyCandidate> candidates,
		IReadOnlySet<string> installed,
		IReadOnlyDictionary<string, ulong> installedVersions,
		IDictionary<string, string> skipped)
	{
		bool removed;
		do
		{
			removed = false;
			for (var index = candidates.Count - 1; index >= 0; index--)
			{
				var candidate = candidates[index];
				var failure = FindUnsatisfiedDependency(candidate, candidates, installed, installedVersions);
				if (failure == null) continue;
				candidates.RemoveAt(index);
				skipped[candidate.Id] = failure;
				removed = true;
			}
		} while (removed);
	}

	private static string FindUnsatisfiedDependency(
		DownloadBatchSafetyCandidate candidate,
		IEnumerable<DownloadBatchSafetyCandidate> candidates,
		IReadOnlySet<string> installed,
		IReadOnlyDictionary<string, ulong> installedVersions)
	{
		foreach (var uuid in RequiredUuids(candidate))
		{
			var requiredVersion = VersionFor(candidate.RequiredModVersions, uuid);
			if (InstalledSatisfies(uuid, requiredVersion, installed, installedVersions)) continue;
			var providers = candidates.Where(provider => Provides(provider, uuid)).ToArray();
			if (providers.Any(provider => VersionFor(provider.ProvidedModVersions, uuid) >= requiredVersion)) continue;
			return providers.Length > 0 && requiredVersion > 0
				? $"Required mod {uuid} is older than required version {requiredVersion}."
				: $"Missing required mod {uuid}.";
		}
		return null;
	}

	private static Dictionary<string, List<string>> BuildPrerequisites(
		IReadOnlyList<DownloadBatchSafetyCandidate> candidates,
		IReadOnlySet<string> installed,
		IReadOnlyDictionary<string, ulong> installedVersions)
	{
		var prerequisites = candidates.ToDictionary(candidate => candidate.Id, _ => new List<string>(), StringComparer.Ordinal);
		foreach (var candidate in candidates)
		{
			foreach (var uuid in RequiredUuids(candidate))
			{
				var requiredVersion = VersionFor(candidate.RequiredModVersions, uuid);
				if (InstalledSatisfies(uuid, requiredVersion, installed, installedVersions)) continue;
				var provider = candidates.FirstOrDefault(other => Provides(other, uuid)
					&& VersionFor(other.ProvidedModVersions, uuid) >= requiredVersion);
				if (provider != null && provider.Id != candidate.Id) prerequisites[candidate.Id].Add(provider.Id);
			}
		}
		return prerequisites;
	}

	private static IReadOnlyList<DownloadBatchSafetyCandidate> OrderPrerequisitesFirst(
		IReadOnlyList<DownloadBatchSafetyCandidate> candidates,
		IReadOnlyDictionary<string, List<string>> prerequisites,
		IDictionary<string, string> skipped)
	{
		var pending = candidates.ToList();
		var successful = new HashSet<string>(StringComparer.Ordinal);
		var ordered = new List<DownloadBatchSafetyCandidate>();
		while (pending.Count > 0)
		{
			var next = pending.FirstOrDefault(candidate => prerequisites[candidate.Id].All(successful.Contains));
			if (next == null)
			{
				foreach (var candidate in pending)
					skipped[candidate.Id] = "Circular dependencies between queued packages, or a dependency on such a cycle, prevent a prerequisite-first installation.";
				break;
			}
			pending.Remove(next);
			successful.Add(next.Id);
			ordered.Add(next);
		}
		return ordered;
	}

	private static IEnumerable<string> ProvidedUuids(DownloadBatchSafetyCandidate candidate) =>
		(candidate.ProvidedModUuids ?? []).Concat(candidate.ProvidedModVersions?.Keys ?? [])
			.Where(uuid => !String.IsNullOrWhiteSpace(uuid)).Distinct(StringComparer.OrdinalIgnoreCase);

	private static IEnumerable<string> RequiredUuids(DownloadBatchSafetyCandidate candidate) =>
		(candidate.RequiredModUuids ?? []).Concat(candidate.RequiredModVersions?.Keys ?? [])
			.Where(uuid => !String.IsNullOrWhiteSpace(uuid)).Distinct(StringComparer.OrdinalIgnoreCase);

	private static bool Provides(DownloadBatchSafetyCandidate candidate, string uuid) =>
		ProvidedUuids(candidate).Contains(uuid, StringComparer.OrdinalIgnoreCase);

	private static bool InstalledSatisfies(string uuid, ulong requiredVersion, IReadOnlySet<string> installed,
		IReadOnlyDictionary<string, ulong> installedVersions) => installed.Contains(uuid)
		&& (requiredVersion <= 0 || !TryGetVersion(installedVersions, uuid, out var installedVersion)
			|| installedVersion >= requiredVersion);

	private static ulong VersionFor(IReadOnlyDictionary<string, ulong> versions, string uuid) =>
		TryGetVersion(versions, uuid, out var version) ? version : 0;

	private static bool TryGetVersion(IReadOnlyDictionary<string, ulong> versions, string uuid, out ulong version)
	{
		if (versions != null && versions.TryGetValue(uuid, out version)) return true;
		if (versions != null)
		{
			foreach (var pair in versions)
				if (String.Equals(pair.Key, uuid, StringComparison.OrdinalIgnoreCase))
				{
					version = pair.Value;
					return true;
				}
		}
		version = 0;
		return false;
	}
}
