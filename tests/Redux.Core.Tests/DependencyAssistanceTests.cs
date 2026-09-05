using System;
using System.IO;
using System.Linq;

using DivinityModManager.AppServices;
using DivinityModManager.Models;
using DivinityModManager.Models.NexusMods;

using Newtonsoft.Json;

namespace Redux.Core.Tests;

internal sealed class DependencyAssistanceTests
{
	private const string Uuid = "069e5871-efe8-44bb-b02a-fe957df5ae0e";
	private static ModuleShortDesc Requirement() => new() { UUID = Uuid, Name = "Required library" };

	public void InstalledDependenciesAreDistinguishedFromMissingDownloads()
	{
		var installed = new RegressionModData { UUID = Uuid.ToUpperInvariant(), Name = "Required library", IsActive = false };
		var rows = ModDependencyAssistanceService.Build([Requirement(), Requirement()], [installed], [], "", true);
		RegressionAssert.Equal(1, rows.Count);
		RegressionAssert.Equal("Installed but inactive.", rows[0].Status);
		RegressionAssert.True(rows[0].CanShowInstalled);
		installed.IsActive = true;
		rows = ModDependencyAssistanceService.Build([Requirement()], [installed], [], "", true);
		RegressionAssert.Equal("Installed and active.", rows[0].Status);
	}

	public void SameProjectDownloadIsOnlyAPossibleDependencyMatch()
	{
		var queued = new NxmDownloadItem { ModId = 3902, FileId = 1, FileDisplayName = "Optional patch", State = NxmDownloadState.Queued };
		var row = ModDependencyAssistanceService.Build([Requirement()], [], [queued], "", true).Single();
		RegressionAssert.Contains(row.Status, "Possible same-project");
		RegressionAssert.False(row.Downloads.Single().Identified);
		RegressionAssert.Contains(row.Downloads[0].Explanation, "different variant");
		RegressionAssert.Contains(row.Downloads[0].Label, "Queued");
	}

	public void InspectedUuidMatchesAreInvalidatedByArchiveChanges()
	{
		var directory = Path.Combine(Path.GetTempPath(), "ReduxDependencyTests", Guid.NewGuid().ToString("N"));
		Directory.CreateDirectory(directory);
		try
		{
			var file = Path.Combine(directory, "library.zip");
			File.WriteAllText(file, "inspection fixture");
			var item = new NxmDownloadItem { CompletedFileName = "library.zip", ModId = 3902, State = NxmDownloadState.Downloaded };
			ModDependencyAssistanceService.RememberInspection(item, directory, [new RegressionModData { UUID = Uuid }]);
			var row = ModDependencyAssistanceService.Build([Requirement()], [], [item], directory, true).Single();
			RegressionAssert.True(row.Downloads.Single().Identified);
			RegressionAssert.Contains(row.Status, "review its version");
			RegressionAssert.False(JsonConvert.SerializeObject(item).Contains("Inspection"));
			File.AppendAllText(file, "changed archive");
			row = ModDependencyAssistanceService.Build([Requirement()], [], [item], directory, true).Single();
			RegressionAssert.False(row.Downloads.Single().Identified);
			ModDependencyAssistanceService.RememberInspection(item, directory, [new RegressionModData { UUID = Uuid }]);
			item.CompletedFileName = "replacement.zip";
			row = ModDependencyAssistanceService.Build([Requirement()], [], [item], directory, true).Single();
			RegressionAssert.False(row.Downloads.Single().Identified);
		}
		finally { Directory.Delete(directory, true); }
	}

	public void SourceLinksRequireReviewedUuidsAndEnabledIntegrations()
	{
		var known = ModDependencyAssistanceService.Build([Requirement()], [], [], "", true).Single();
		RegressionAssert.Equal("https://www.nexusmods.com/baldursgate3/mods/3902?tab=files", known.NexusFilesUrl.AbsoluteUri);
		var offline = ModDependencyAssistanceService.Build([Requirement()], [], [], "", false).Single();
		RegressionAssert.False(offline.CanOpenFiles);
		RegressionAssert.True(offline.CanCopyUuid);
		RegressionAssert.Contains(offline.SourceHint, "disabled");
		var unknown = new ModuleShortDesc { UUID = "11111111-1111-4111-8111-111111111111", Name = "Required library" };
		var similarlyNamed = new NxmDownloadItem { ProjectName = "Required library", ModId = 3902, State = NxmDownloadState.Downloading };
		var row = ModDependencyAssistanceService.Build([unknown], [], [similarlyNamed], "", true).Single();
		RegressionAssert.False(row.CanOpenFiles);
		RegressionAssert.Equal(0, row.Downloads.Count);
		RegressionAssert.True(row.CanCopyUuid);
		unknown.UUID = "https://example.test/not-a-uuid";
		row = ModDependencyAssistanceService.Build([unknown], [], [similarlyNamed], "", true).Single();
		RegressionAssert.False(row.CanOpenFiles);
		RegressionAssert.False(row.CanCopyUuid);
	}

	public void BundledAndOlderInstalledDependenciesAreExplained()
	{
		var requirement = Requirement();
		requirement.Version = DivinityModVersion2.FromInt(10);
		var dependency = new RegressionModData { UUID = Uuid, Version = DivinityModVersion2.FromInt(2) };
		var row = ModDependencyAssistanceService.Build([requirement], [], [], "", true, [dependency]).Single();
		RegressionAssert.Contains(row.Status, "Included in this archive");
		RegressionAssert.Contains(row.Status, "older than declared requirement");
		RegressionAssert.False(row.CanShowInstalled);
		row = ModDependencyAssistanceService.Build([requirement], [dependency], [], "", true).Single();
		RegressionAssert.Contains(row.Status, "Installed but inactive");
		RegressionAssert.Contains(row.Status, "older than declared requirement");
		RegressionAssert.True(row.CanShowInstalled);
	}
}
