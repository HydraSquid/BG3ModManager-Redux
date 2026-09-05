using DivinityModManager.AppServices;
using DivinityModManager.Models;
using DivinityModManager.Util;

using System;
using System.Collections.Generic;

namespace Redux.Core.Tests;

public sealed class InteractionBehaviorTests
{
	public void CrossListSelectionClearWaitsForTheCurrentSelectionTransaction()
	{
		var active = new SelectionContext { IsSelected = true };
		var inactive = new SelectionContext { IsSelected = true };
		var forceLoaded = new SelectionContext { IsSelected = true };
		var pending = new Queue<Action>();
		var coordinator = new DeferredSelectionCoordinator<SelectionContext>();

		coordinator.Schedule(
			inactive,
			pending.Enqueue,
			[active, inactive, forceLoaded],
			context => context.IsSelected = false);

		RegressionAssert.True(active.IsSelected);
		RegressionAssert.True(forceLoaded.IsSelected);
		RegressionAssert.Equal(1, pending.Count);

		pending.Dequeue()();

		RegressionAssert.False(active.IsSelected);
		RegressionAssert.True(inactive.IsSelected);
		RegressionAssert.False(forceLoaded.IsSelected);
	}

	public void NewerSelectionSupersedesQueuedCrossListClear()
	{
		var active = new SelectionContext { IsSelected = true };
		var inactive = new SelectionContext { IsSelected = true };
		var pending = new Queue<Action>();
		var coordinator = new DeferredSelectionCoordinator<SelectionContext>();
		coordinator.Schedule(active, pending.Enqueue, [active, inactive], context => context.IsSelected = false);
		coordinator.Schedule(inactive, pending.Enqueue, [active, inactive], context => context.IsSelected = false);
		pending.Dequeue()();
		RegressionAssert.True(active.IsSelected);
		RegressionAssert.True(inactive.IsSelected);
		pending.Dequeue()();
		RegressionAssert.False(active.IsSelected);
		RegressionAssert.True(inactive.IsSelected);
	}

	public void ReentrantSelectionSupersedesAnExecutingClear()
	{
		var active = new SelectionContext { IsSelected = true };
		var inactive = new SelectionContext { IsSelected = true };
		var forceLoaded = new SelectionContext { IsSelected = true };
		var pending = new Queue<Action>();
		var coordinator = new DeferredSelectionCoordinator<SelectionContext>();
		coordinator.Schedule(inactive, pending.Enqueue, [active, inactive, forceLoaded], context =>
		{
			context.IsSelected = false;
			coordinator.Schedule(forceLoaded, pending.Enqueue, [active, inactive, forceLoaded], other => other.IsSelected = false);
		});
		pending.Dequeue()();
		RegressionAssert.True(forceLoaded.IsSelected);
		pending.Dequeue()();
		RegressionAssert.False(active.IsSelected);
		RegressionAssert.False(inactive.IsSelected);
		RegressionAssert.True(forceLoaded.IsSelected);
	}

	public void DrawerRetainsASelectedModDuringCrossListTransferOnly()
	{
		var displayed = new DivinityModData { UUID = "moving-mod", IsSelected = true };
		var retained = SelectionContinuity.ResolveDisplayedItem<DivinityModData>(
			null,
			displayed,
			mod => mod.IsSelected);
		RegressionAssert.Equal(displayed, retained);

		displayed.IsSelected = false;
		var cleared = SelectionContinuity.ResolveDisplayedItem<DivinityModData>(
			null,
			displayed,
			mod => mod.IsSelected);
		RegressionAssert.True(cleared == null);

		var replacement = new DivinityModData { UUID = "replacement" };
		var replaced = SelectionContinuity.ResolveDisplayedItem(
			replacement,
			displayed,
			_ => true);
		RegressionAssert.Equal(replacement, replaced);
	}

	public void SavingCurrentOrderCanNeverWriteTheGameExportFile()
	{
		var current = new DivinityLoadOrder
		{
			Name = "Current",
			FilePath = @"C:\Profiles\Public\modsettings.lsx",
			IsModSettings = true
		};
		var defensiveLsxCase = new DivinityLoadOrder
		{
			Name = "Unexpected LSX",
			FilePath = @"C:\Profiles\Public\MODSETTINGS.LSX"
		};
		var saved = new DivinityLoadOrder
		{
			Name = "My Order",
			FilePath = @"C:\Orders\My Order.json"
		};

		RegressionAssert.True(LoadOrderPersistencePolicy.RequiresSaveAs(current));
		RegressionAssert.True(LoadOrderPersistencePolicy.RequiresSaveAs(defensiveLsxCase));
		RegressionAssert.False(LoadOrderPersistencePolicy.RequiresSaveAs(saved));
	}

	public void NewBlankOrderContainsNoActivatedMods()
	{
		var order = LoadOrderPersistencePolicy.CreateBlankOrder(
			"New Load Order",
			@"C:\Orders\New Load Order.json");

		RegressionAssert.Equal("New Load Order", order.Name);
		RegressionAssert.Equal(@"C:\Orders\New Load Order.json", order.FilePath);
		RegressionAssert.Equal(0, order.Order.Count);
		RegressionAssert.False(order.IsModSettings);
	}

	private sealed class SelectionContext
	{
		public bool IsSelected { get; set; }
	}
}
