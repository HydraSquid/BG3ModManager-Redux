using System;
using System.Collections.Generic;
using System.Linq;

using DivinityModManager.AppServices;
using DivinityModManager.Models;
using DivinityModManager.Models.Health;

namespace Redux.Core.Tests;

internal sealed class LoadOrderAdvisorOrganizerTests
{
	public void PreserveSeparatorsSortsInsideButNeverAcrossUserBoundaries()
	{
		var dependency = CreateMod("dependency", "Dependency");
		var dependent = CreateMod("dependent", "Dependent");
		var knowledge = CreateKnowledge(entries: [Entry(dependent, dependencies: [dependency])]);
		var plan = LoadOrderAdvisorOrganizer.CreatePlan(
			[dependent, dependency], [Divider("First", 0, dependent.UUID, dependency.UUID)],
			LoadOrderAdvisorSeparatorPolicy.PreserveMySeparators, knowledge);

		RegressionAssert.SequenceEqual([dependency, dependent], plan.OrderedMods);
		RegressionAssert.Equal("First", plan.Dividers.Single().Title);
		RegressionAssert.SequenceEqual([dependency.UUID, dependent.UUID], plan.Dividers.Single().MemberModUuids);
		RegressionAssert.Equal(0, plan.SeparatorChanges.Count);
	}

	public void PreserveSeparatorsReportsRelationshipsItCannotSafelyApply()
	{
		var dependency = CreateMod("dependency", "Dependency");
		var dependent = CreateMod("dependent", "Dependent");
		var knowledge = CreateKnowledge(entries: [Entry(dependent, dependencies: [dependency])]);
		var plan = LoadOrderAdvisorOrganizer.CreatePlan(
			[dependent, dependency],
			[Divider("First", 0, dependent.UUID), Divider("Second", 2, dependency.UUID)],
			LoadOrderAdvisorSeparatorPolicy.PreserveMySeparators, knowledge);

		RegressionAssert.SequenceEqual([dependent, dependency], plan.OrderedMods);
		RegressionAssert.Equal(1, plan.UnresolvedRelationships.Count);
		RegressionAssert.Contains(plan.UnresolvedRelationships[0].Reason, "separator placement prevents");
	}

	public void PreserveSeparatorsAcceptsRelationshipsAlreadySatisfiedAcrossSeparators()
	{
		var dependency = CreateMod("dependency", "Dependency");
		var dependent = CreateMod("dependent", "Dependent");
		var knowledge = CreateKnowledge(entries: [Entry(dependent, dependencies: [dependency])]);
		var plan = LoadOrderAdvisorOrganizer.CreatePlan(
			[dependency, dependent],
			[Divider("First", 0, dependency.UUID), Divider("Second", 2, dependent.UUID)],
			LoadOrderAdvisorSeparatorPolicy.PreserveMySeparators, knowledge);

		RegressionAssert.SequenceEqual([dependency, dependent], plan.OrderedMods);
		RegressionAssert.Equal(0, plan.UnresolvedRelationships.Count);
	}

	public void SuggestedSeparatorsUseOnlyNonemptyOfflineGroups()
	{
		var late = CreateMod("late", "Late Mod");
		var unknown = CreateMod("unknown", "Unknown Mod");
		var early = CreateMod("early", "Early Mod");
		var knowledge = CreateKnowledge(
			groups:
			[
				new ReduxOrderingGroupKnowledge { Name = "Early" },
				new ReduxOrderingGroupKnowledge { Name = "Unused", After = ["Early"] },
				new ReduxOrderingGroupKnowledge { Name = "Late", After = ["Unused"] }
			],
			entries: [Entry(early, "Early"), Entry(late, "Late")]);
		var plan = LoadOrderAdvisorOrganizer.CreatePlan(
			[late, unknown, early], [],
			LoadOrderAdvisorSeparatorPolicy.CreateSuggestedSeparators, knowledge);

		RegressionAssert.SequenceEqual([early, late, unknown], plan.OrderedMods);
		RegressionAssert.SequenceEqual(["Early", "Late", "Needs Review"], plan.Dividers.Select(divider => divider.Title));
		RegressionAssert.False(plan.Dividers.Any(divider => divider.Title == "Unused"));
		RegressionAssert.Equal(3, plan.SeparatorChanges.Count);
		RegressionAssert.True(plan.SeparatorChanges.All(change => change.Kind == LoadOrderAdvisorSeparatorChangeKind.Created));
	}

	public void RemoveSeparatorsGloballySortsWithoutReturningMarkers()
	{
		var dependency = CreateMod("dependency", "Dependency");
		var dependent = CreateMod("dependent", "Dependent");
		var knowledge = CreateKnowledge(entries: [Entry(dependent, dependencies: [dependency])]);
		var plan = LoadOrderAdvisorOrganizer.CreatePlan(
			[dependent, dependency], [Divider("Old", 0, dependent.UUID, dependency.UUID)],
			LoadOrderAdvisorSeparatorPolicy.RemoveSeparators, knowledge);

		RegressionAssert.SequenceEqual([dependency, dependent], plan.OrderedMods);
		RegressionAssert.Equal(0, plan.Dividers.Count);
		RegressionAssert.Equal(1, plan.SeparatorChanges.Count);
		RegressionAssert.Equal(LoadOrderAdvisorSeparatorChangeKind.Removed, plan.SeparatorChanges[0].Kind);
	}

	public void UnknownModsRetainTheirRelativeOrder()
	{
		var unknownA = CreateMod("unknown-a", "Unknown A");
		var known = CreateMod("known", "Known");
		var unknownB = CreateMod("unknown-b", "Unknown B");
		var knowledge = CreateKnowledge(
			groups: [new ReduxOrderingGroupKnowledge { Name = "Known" }],
			entries: [Entry(known, "Known")]);
		var plan = LoadOrderAdvisorOrganizer.CreatePlan(
			[unknownA, known, unknownB], [],
			LoadOrderAdvisorSeparatorPolicy.RemoveSeparators, knowledge);

		var result = plan.OrderedMods.ToList();
		RegressionAssert.True(result.IndexOf(unknownA) < result.IndexOf(unknownB));
	}

	public void PreserveSeparatorsDoesNotAdoptAVisibleRowBelowAClosedSeparator()
	{
		var member = CreateMod("member", "Sealed Member");
		var visibleRow = CreateMod("visible", "Visible Row");
		var divider = Divider("Closed", 0, member.UUID);
		divider.IsCollapsed = true;

		var plan = LoadOrderAdvisorOrganizer.CreatePlan(
			[member, visibleRow], [divider],
			LoadOrderAdvisorSeparatorPolicy.PreserveMySeparators,
			CreateKnowledge());

		RegressionAssert.SequenceEqual([member.UUID], plan.Dividers.Single().MemberModUuids);
	}

	public void PreserveSeparatorsReportsOnlyMarkersThatActuallyMove()
	{
		var member = CreateMod("member", "Member");
		var divider = Divider("Moved", -3, member.UUID);

		var plan = LoadOrderAdvisorOrganizer.CreatePlan(
			[member], [divider],
			LoadOrderAdvisorSeparatorPolicy.PreserveMySeparators,
			CreateKnowledge());

		RegressionAssert.Equal(1, plan.SeparatorChanges.Count);
		RegressionAssert.Equal(LoadOrderAdvisorSeparatorChangeKind.Repositioned, plan.SeparatorChanges[0].Kind);
		RegressionAssert.Equal(-3, plan.SeparatorChanges[0].PreviousPosition);
		RegressionAssert.Equal(0, plan.SeparatorChanges[0].Divider.Position);
	}

	public void IgnoringOneRelationshipDoesNotSuppressOtherAdvisorKnowledge()
	{
		var firstDependency = CreateMod("dependency-a", "Dependency A");
		var secondDependency = CreateMod("dependency-b", "Dependency B");
		var dependent = CreateMod("dependent", "Dependent");
		var knowledge = CreateKnowledge(entries:
		[
			Entry(dependent, dependencies: [firstDependency, secondDependency])
		]);
		var ignoredKey = LoadOrderAdvisorFindingIdentity.Create(
			dependent.UUID,
			ModHealthFindingCode.DependencyLoadsLater,
			[firstDependency.UUID]);

		var plan = LoadOrderAdvisorOrganizer.CreatePlan(
			[dependent, firstDependency, secondDependency], [],
			LoadOrderAdvisorSeparatorPolicy.RemoveSeparators,
			knowledge,
			[ignoredKey]);

		var ordered = plan.OrderedMods.ToList();
		RegressionAssert.True(ordered.IndexOf(secondDependency) < ordered.IndexOf(dependent));
		RegressionAssert.False(plan.Moves.Any(move => String.Equals(move.IgnoreKey, ignoredKey, StringComparison.OrdinalIgnoreCase)));
	}

	private static ReduxLoadOrderAdvisorKnowledge CreateKnowledge(
		IEnumerable<ReduxOrderingGroupKnowledge>? groups = null,
		IEnumerable<ReduxLoadOrderEntryKnowledge>? entries = null) =>
		ReduxLoadOrderAdvisorKnowledge.Create(
			groups ?? [], new Dictionary<string, string>(),
			new Dictionary<string, List<string>>(), entries ?? []);

	private static ReduxLoadOrderEntryKnowledge Entry(
		DivinityModData mod,
		string? group = null,
		IEnumerable<DivinityModData>? dependencies = null) => new()
	{
		Uuid = mod.UUID,
		Name = mod.Name,
		Group = group ?? String.Empty,
		Dependencies = (dependencies ?? []).Select(dependency => new ReduxLoadOrderDependencyKnowledge
		{
			Uuid = dependency.UUID,
			Name = dependency.Name
		}).ToList()
	};

	private static ModListVisualDividerData Divider(string title, int position, params string[] members) => new()
	{
		Title = title,
		Position = position,
		IsActiveList = true,
		MemberModUuids = members.ToList()
	};

	private static RegressionModData CreateMod(string uuid, string name) => new()
	{
		UUID = uuid,
		Name = name,
		Folder = name,
		IsActive = true,
		Version = DivinityModVersion2.FromInt(1)
	};
}
