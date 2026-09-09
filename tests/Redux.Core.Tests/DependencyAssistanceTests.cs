using DivinityModManager.AppServices;
using DivinityModManager.Models;
using DivinityModManager.Models.NexusMods;

using Newtonsoft.Json;

using System;
using System.IO;
using System.Linq;

namespace Redux.Core.Tests;

internal sealed class DependencyAssistanceTests
{
	private const string Uuid = "069e5871-efe8-44bb-b02a-fe957df5ae0e";
	private static ModuleShortDesc Requirement() => new() { UUID = Uuid, Name = "Required library" };

	public void InstalledAndBundledDependenciesReportTheirAvailableVersion()
	{
		var requirement = Requirement();
		requirement.Version = DivinityModVersion2.FromInt(10);
		var dependency = new RegressionModData { UUID = Uuid, Version = DivinityModVersion2.FromInt(2), IsActive = false };

		var bundled = ModDependencyAssistanceService.Build([requirement], [], [], "", false, [dependency], _ => null).Single();
		var installed = ModDependencyAssistanceService.Build([requirement], [dependency], [], "", false,
			resolveProject: _ => null).Single();

		RegressionAssert.Contains(bundled.Status, "Included in this archive");
		RegressionAssert.Contains(bundled.Status, "older than declared requirement");
		RegressionAssert.Contains(installed.Status, "Installed but inactive");
		RegressionAssert.True(installed.CanShowInstalled);
	}

	public void InspectedQueueMatchIsInvalidatedWhenTheArchiveChanges()
	{
		var directory = Path.Combine(Path.GetTempPath(), "ReduxDependencyTests", Guid.NewGuid().ToString("N"));
		Directory.CreateDirectory(directory);
		try
		{
			var file = Path.Combine(directory, "library.zip");
			File.WriteAllText(file, "inspection fixture");
			var item = new NxmDownloadItem { CompletedFileName = "library.zip", ModId = 3902, State = NxmDownloadState.Downloaded };
			ModDependencyAssistanceService.RememberInspection(item, directory, [new RegressionModData { UUID = Uuid }]);

			var matched = ModDependencyAssistanceService.Build([Requirement()], [], [item], directory, true,
				resolveProject: _ => 3902).Single();
			File.AppendAllText(file, "changed archive");
			File.SetLastWriteTimeUtc(file, DateTime.UtcNow.AddSeconds(1));
			var invalidated = ModDependencyAssistanceService.Build([Requirement()], [], [item], directory, true,
				resolveProject: _ => 3902).Single();

			RegressionAssert.True(matched.Downloads.Single().Identified);
			RegressionAssert.False(invalidated.Downloads.Single().Identified);
			RegressionAssert.True(Newtonsoft.Json.Linq.JObject.Parse(JsonConvert.SerializeObject(item)).Property("Inspection") == null);
		}
		finally { Directory.Delete(directory, true); }
	}

	public void UnreviewedNamesDoNotCreateDependencyMatches()
	{
		var unknown = new ModuleShortDesc { UUID = "11111111-1111-4111-8111-111111111111", Name = "Required library" };
		var similarlyNamed = new NxmDownloadItem { ProjectName = "Required library", ModId = 3902, State = NxmDownloadState.Downloading };

		var row = ModDependencyAssistanceService.Build([unknown], [], [similarlyNamed], "", true,
			resolveProject: _ => null).Single();

		RegressionAssert.False(row.CanOpenFiles);
		RegressionAssert.Equal(0, row.Downloads.Count);
		RegressionAssert.True(row.CanCopyUuid);
	}
}
