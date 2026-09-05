using System;
using System.Collections.Generic;
using System.Linq;
using DynamicData;
using DivinityModManager.AppServices;
using DivinityModManager.Models;
using DivinityModManager.Models.NexusMods;

namespace Redux.Core.Tests;

internal sealed class NxmBatchInstallPlannerTests
{
	public void ReverseDownloadedChainInstallsPrerequisitesBeforeAllDependents()
	{
		var fix = Mod("Music performance fix");
		var library = Mod("Goon library", fix);
		var goons = Enumerable.Range(0, 10).Select(index => Candidate(Mod($"Goon {index}", library))).ToArray();
		var selected = goons.Concat([Candidate(library), Candidate(fix)]).ToArray();
		var plan = NxmBatchInstallPlanner.Build(selected, []);
		RegressionAssert.Equal(0, plan.Blocked.Count);
		RegressionAssert.Equal(selected[11], plan.Ordered[0]);
		RegressionAssert.Equal(selected[10], plan.Ordered[1]);
		RegressionAssert.True(plan.Ordered.Skip(2).SequenceEqual(goons));
		RegressionAssert.Equal(12, plan.Ordered.Select(candidate => candidate.Download).Distinct().Count());
	}

	public void CyclesAndTheirDependentsAreBlockedWithoutBlockingUnrelatedArchives()
	{
		var a = Mod("A");
		var b = Mod("B", a);
		a.Dependencies.AddOrUpdate(ModuleShortDesc.FromModData(b));
		var c = Mod("Depends on cycle", a);
		var independent = Candidate(Mod("Independent"));
		var plan = NxmBatchInstallPlanner.Build([Candidate(a), Candidate(b), Candidate(c), independent], []);
		RegressionAssert.Equal(3, plan.Blocked.Count);
		RegressionAssert.True(plan.Blocked.Values.All(reason => reason.Contains("Circular")));
		RegressionAssert.Equal(independent, plan.Ordered.Single());
		var self = Mod("Self");
		self.Dependencies.AddOrUpdate(ModuleShortDesc.FromModData(self));
		RegressionAssert.Equal(1, NxmBatchInstallPlanner.Build([Candidate(self)], []).Blocked.Count);
	}

	public void MissingAndUnselectedPrerequisitesBlockTheirWholeChain()
	{
		var missing = Mod("Unselected library");
		var middle = Mod("Middle", missing);
		var top = Mod("Top", middle);
		var plan = NxmBatchInstallPlanner.Build([Candidate(top), Candidate(middle)], []);
		RegressionAssert.Equal(2, plan.Blocked.Count);
		RegressionAssert.Equal(0, plan.Ordered.Count);
		RegressionAssert.True(plan.Blocked.Values.Any(reason => reason.Contains("Unselected library")));
	}

	public void InstalledAndBundledPrerequisitesRespectRequiredVersions()
	{
		var library = Mod("Library");
		library.Version = DivinityModVersion2.FromInt(10);
		var mod = Mod("Consumer", library);
		var old = Mod("Old library");
		old.UUID = library.UUID.ToUpperInvariant();
		old.Version = DivinityModVersion2.FromInt(2);
		RegressionAssert.Equal(1, NxmBatchInstallPlanner.Build([Candidate(mod)], [old]).Blocked.Count);
		RegressionAssert.Equal(0, NxmBatchInstallPlanner.Build([Candidate(mod)], [library]).Blocked.Count);
		RegressionAssert.Equal(1, NxmBatchInstallPlanner.Build([Candidate(mod), Candidate(old)], [library]).Blocked.Count);
		var bundled = Candidate(mod, library);
		RegressionAssert.Equal(bundled, NxmBatchInstallPlanner.Build([bundled], []).Ordered.Single());
		var plan = NxmBatchInstallPlanner.Build([Candidate(Mod("Outer", mod)), bundled], []);
		RegressionAssert.Equal(bundled, plan.Ordered[0]);
	}

	public void DuplicateUuidAndDestinationChoicesAreNotGuessed()
	{
		var first = Mod("Library");
		var other = Mod("Alternative");
		other.UUID = first.UUID.ToUpperInvariant();
		var dependent = Mod("Consumer", first);
		var plan = NxmBatchInstallPlanner.Build([Candidate(first), Candidate(other), Candidate(dependent)], []);
		RegressionAssert.Equal(3, plan.Blocked.Count);
		RegressionAssert.Equal(0, plan.Ordered.Count);
		other.UUID = Guid.NewGuid().ToString();
		other.FilePath = first.FilePath;
		RegressionAssert.Equal(2, NxmBatchInstallPlanner.Build([Candidate(first), Candidate(other)], []).Blocked.Count);
	}

	public void SkippedOrFailedPrerequisitesBlockDependentsAndLiveStateIsRechecked()
	{
		var library = Mod("Library");
		var provider = Candidate(library);
		var dependent = Candidate(Mod("Consumer", library));
		var independent = Candidate(Mod("Unrelated"));
		var plan = NxmBatchInstallPlanner.Build([dependent, provider, independent], []);
		var successful = new HashSet<NxmDownloadItem>();
		RegressionAssert.True(NxmBatchInstallPlanner.GetExecutionBlockReason(plan, dependent, successful, []) != null);
		RegressionAssert.True(NxmBatchInstallPlanner.GetExecutionBlockReason(plan, independent, successful, []) == null);
		successful.Add(provider.Download);
		RegressionAssert.True(NxmBatchInstallPlanner.GetExecutionBlockReason(plan, dependent, successful, []) != null);
		RegressionAssert.True(NxmBatchInstallPlanner.GetExecutionBlockReason(plan, dependent, successful, [library]) == null);
	}

	public void LongDependencyChainsDoNotUseTheCallStack()
	{
		var candidates = new List<NxmInstallCandidate>();
		var previous = Mod("Root");
		candidates.Add(Candidate(previous));
		for (var i = 0; i < 1500; i++)
		{
			previous = Mod($"Level {i}", previous);
			candidates.Insert(0, Candidate(previous));
		}
		var plan = NxmBatchInstallPlanner.Build(candidates, []);
		RegressionAssert.Equal(0, plan.Blocked.Count);
		RegressionAssert.True(plan.Ordered.SequenceEqual(candidates.AsEnumerable().Reverse()));
	}

	private static RegressionModData Mod(string name, params DivinityModData[] dependencies)
	{
		var mod = new RegressionModData { UUID = Guid.NewGuid().ToString(), Name = name, FilePath = name + ".pak", Version = DivinityModVersion2.FromInt(1) };
		foreach (var dependency in dependencies) mod.Dependencies.AddOrUpdate(ModuleShortDesc.FromModData(dependency));
		return mod;
	}
	private static NxmInstallCandidate Candidate(params DivinityModData[] modules) =>
		new(new NxmDownloadItem { FileDisplayName = modules[0].Name, State = NxmDownloadState.Downloaded }, modules);
}
