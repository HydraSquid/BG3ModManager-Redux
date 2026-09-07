namespace DivinityModManager.AppServices;

public sealed record DownloadBatchSafetyCandidate(
	string Id,
	string ArchiveSha256,
	IReadOnlyList<string> ConflictKeys,
	IReadOnlyList<string> ProvidedModUuids,
	IReadOnlyList<string> RequiredModUuids);

public sealed record DownloadBatchSafetyPlan(
	IReadOnlyList<string> AcceptedIds,
	IReadOnlyDictionary<string, string> SkippedReasons);

/// <summary>
/// Produces a deterministic, read-only batch plan. Earlier inbox entries win
/// destination conflicts; dependency loss cascades conservatively.
/// </summary>
public static class DownloadBatchSafetyPlanner
{
	public static DownloadBatchSafetyPlan Create(
		IEnumerable<DownloadBatchSafetyCandidate> source,
		IEnumerable<string> installedModUuids)
	{
		var candidates = (source ?? []).Where(candidate => candidate != null).ToList();
		var installed = (installedModUuids ?? []).Where(uuid => !String.IsNullOrWhiteSpace(uuid))
			.ToHashSet(StringComparer.OrdinalIgnoreCase);
		var skipped = new Dictionary<string, string>(StringComparer.Ordinal);

		RemoveMissingDependencies(candidates, installed, skipped);

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

		RemoveMissingDependencies(accepted, installed, skipped);
		return new DownloadBatchSafetyPlan(accepted.Select(candidate => candidate.Id).ToArray(), skipped);
	}

	private static void RemoveMissingDependencies(
		IList<DownloadBatchSafetyCandidate> candidates,
		IReadOnlySet<string> installed,
		IDictionary<string, string> skipped)
	{
		bool removed;
		do
		{
			removed = false;
			var provided = candidates.SelectMany(candidate => candidate.ProvidedModUuids)
				.Where(uuid => !String.IsNullOrWhiteSpace(uuid)).ToHashSet(StringComparer.OrdinalIgnoreCase);
			for (var index = candidates.Count - 1; index >= 0; index--)
			{
				var candidate = candidates[index];
				var missing = candidate.RequiredModUuids.FirstOrDefault(uuid =>
					!String.IsNullOrWhiteSpace(uuid) && !installed.Contains(uuid) && !provided.Contains(uuid));
				if (missing == null) continue;
				candidates.RemoveAt(index);
				skipped[candidate.Id] = $"Missing required mod {missing}.";
				removed = true;
			}
		} while (removed);
	}
}
