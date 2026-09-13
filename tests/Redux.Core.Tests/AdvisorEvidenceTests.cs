using System.Collections.Generic;
using System.Linq;
using DivinityModManager.AppServices;

namespace Redux.Core.Tests;

internal sealed class AdvisorEvidenceTests
{
	public void PlacementEvidenceDoesNotClaimCompatibility()
	{
		var early = new RegressionModData { UUID = "early", Name = "Early" };
		var late = new RegressionModData { UUID = "late", Name = "Late" };
		var knowledge = ReduxLoadOrderAdvisorKnowledge.Create(
			[new ReduxOrderingGroupKnowledge { Name = "Early" }, new ReduxOrderingGroupKnowledge { Name = "Late", After = ["Early"] }],
			new Dictionary<string, string>(), new Dictionary<string, List<string>>(),
			[new ReduxLoadOrderEntryKnowledge { Uuid = early.UUID, Name = early.Name, Group = "Early",
				Evidence = new ReduxPlacementEvidence { Basis = "inferred", Confidence = 0.99 } },
			 new ReduxLoadOrderEntryKnowledge { Uuid = late.UUID, Name = late.Name, Group = "Late" }]);
		var input = new[] { late, early };
		var plan = LoadOrderAdvisorOrganizer.CreatePlan(input, [], LoadOrderAdvisorSeparatorPolicy.RemoveSeparators, knowledge);
		RegressionAssert.SequenceEqual(new[] { early, late }, plan.OrderedMods);
		RegressionAssert.SequenceEqual(new[] { late, early }, input);
		RegressionAssert.True(plan.Moves.Any(move => move.Reason == "Early · Inferred placement"));
		RegressionAssert.Equal("Unverified placement", new ReduxPlacementEvidence { Basis = "future-source" }.Label);
	}
}
