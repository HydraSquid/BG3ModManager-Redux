using System.Threading.Tasks;
using DivinityModManager.AppServices;
using DivinityModManager.Models.NexusMods;

namespace Redux.Core.Tests;

public sealed class ReleaseFlowTests
{
	public void InstanceGuardExcludesAnotherThreadAndReleasesItsLease()
	{
		var name = "Redux.Regression." + System.Guid.NewGuid().ToString("N");
		using (var first = new ReduxInstanceGuard(name))
		{
			RegressionAssert.True(first.TryAcquire());
			var secondAcquired = Task.Run(() => { using var second = new ReduxInstanceGuard(name); return second.TryAcquire(); }).GetAwaiter().GetResult();
			RegressionAssert.False(secondAcquired);
		}
		using var next = new ReduxInstanceGuard(name);
		RegressionAssert.True(next.TryAcquire());
	}
	public void PakCountIsMetadataWithoutRepeatedPlacementInstructions()
	{
		var item = new NxmDownloadItem { State = NxmDownloadState.Downloaded, Author = "Eola", InspectionSummary = "1 PAK mod · New mods go inactive; updates keep their placement" };
		RegressionAssert.Contains(item.MetadataText, "Nexus Mods  ·  1 PAK  ·  by Eola");
		RegressionAssert.Equal(string.Empty, item.DownloadDetailText);
	}
}
