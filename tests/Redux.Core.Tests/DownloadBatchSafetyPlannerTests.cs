using DivinityModManager.AppServices;

using System.Collections.Generic;

namespace Redux.Core.Tests;

internal sealed class DownloadBatchSafetyPlannerTests
{
	public void DuplicateArchivesAndTargetsKeepTheFirstInboxEntry()
	{
		var plan = DownloadBatchSafetyPlanner.Create([
			Candidate("first", "hash-a", ["pak:one"]),
			Candidate("same-hash", "hash-a", ["pak:two"]),
			Candidate("same-target", "hash-b", ["pak:one"]),
			Candidate("independent", "hash-c", ["pak:three"])
		], []);

		RegressionAssert.SequenceEqual(["first", "independent"], plan.AcceptedIds);
		RegressionAssert.Contains(plan.SkippedReasons["same-hash"], "same mod or destination");
		RegressionAssert.Contains(plan.SkippedReasons["same-target"], "same mod or destination");
	}

	public void DependencyMayBeInstalledOrProvidedByTheSameBatch()
	{
		var plan = DownloadBatchSafetyPlanner.Create([
			Candidate("dependent", "hash-a", ["pak:dependent"], ["dependent"], ["library", "provider"]),
			Candidate("provider", "hash-b", ["pak:provider"], ["provider"])
		], ["library"]);

		RegressionAssert.SequenceEqual(["dependent", "provider"], plan.AcceptedIds);
		RegressionAssert.Equal(0, plan.SkippedReasons.Count);
	}

	public void MissingDependencySkipsOnlyItsDependentPackage()
	{
		var plan = DownloadBatchSafetyPlanner.Create([
			Candidate("blocked", "hash-a", ["pak:blocked"], ["blocked"], ["missing"]),
			Candidate("independent", "hash-b", ["pak:independent"], ["independent"])
		], []);

		RegressionAssert.SequenceEqual(["independent"], plan.AcceptedIds);
		RegressionAssert.Contains(plan.SkippedReasons["blocked"], "missing");
	}

	public void DependencyLossAfterAConflictCascadesSafely()
	{
		var plan = DownloadBatchSafetyPlanner.Create([
			Candidate("winner", "hash-a", ["native:shared"]),
			Candidate("provider", "hash-b", ["native:shared"], ["provider"]),
			Candidate("dependent", "hash-c", ["pak:dependent"], ["dependent"], ["provider"])
		], []);

		RegressionAssert.SequenceEqual(["winner"], plan.AcceptedIds);
		RegressionAssert.Contains(plan.SkippedReasons["provider"], "same mod or destination");
		RegressionAssert.Contains(plan.SkippedReasons["dependent"], "provider");
	}

	private static DownloadBatchSafetyCandidate Candidate(string id, string hash,
		IReadOnlyList<string> conflicts, IReadOnlyList<string>? provided = null,
		IReadOnlyList<string>? required = null) =>
		new(id, hash, conflicts, provided ?? [], required ?? []);
}
