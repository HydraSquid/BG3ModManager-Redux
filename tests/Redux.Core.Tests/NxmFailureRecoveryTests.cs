using System;
using System.IO;
using DynamicData;

using DivinityModManager.AppServices;
using DivinityModManager.Models;
using DivinityModManager.Models.Health;
using DivinityModManager.Models.NexusMods;
using DivinityModManager.Util;

namespace Redux.Core.Tests;

internal sealed class NxmFailureRecoveryTests
{
	public void MissingDependencyFailureNamesTheRequirementAndRecovery()
	{
		var mod = new RegressionModData
		{
			UUID = Guid.NewGuid().ToString(), Name = "Goon's Paladin Overhaul", Author = "Goonsack",
			Folder = "Goon_Paladin_Overhaul", HasMetadata = true, IsUserMod = true
		};
		mod.Dependencies.AddOrUpdate(new ModuleShortDesc { UUID = "07fbc2f1-f359-4b9d-b243-fe28bd783e4c", Name = "Goon's Library" });
		mod.Files = ["Mods/Goon_Paladin_Overhaul/meta.lsx"];
		var report = PackagePreflightService.AnalyzeLoadedPackage("Paladin.pak", mod, []);
		NexusDownloadedModValidationException? error = null;
		try { NexusDownloadedModValidationException.ThrowIfBlocked(report); }
		catch (NexusDownloadedModValidationException ex) { error = ex; }
		RegressionAssert.True(error != null);
		RegressionAssert.Equal("missing-dependencies", error!.ErrorCode);
		RegressionAssert.Contains(error.Message, "Goon's Library");
		RegressionAssert.Contains(error.Message, "retry installation");
		RegressionAssert.Contains(error.Message, "Downloading this archive again will not");
	}

	public void FailureDescriptionsNeverExposeRawExceptionCapabilities()
	{
		const string secret = "private-download-token";
		var error = new IOException($"https://example.test/file?key={secret}&expires=123&user_id=45");
		RegressionAssert.False(NexusDownloadedModValidationException.Describe(error).Details.Contains(secret));
		var report = new PackagePreflightReport("Test.pak", null!, 0, 0,
			[new PackagePreflightFinding(ModHealthSeverity.Error, "Missing dependency",
				$"Dependency https://example.test/file?key={secret} key={secret} is not installed.")]);
		try { NexusDownloadedModValidationException.ThrowIfBlocked(report); }
		catch (NexusDownloadedModValidationException ex)
		{
			RegressionAssert.False(ex.Message.Contains(secret));
			RegressionAssert.Contains(ex.Message, "[redacted]");
			return;
		}
		throw new InvalidOperationException("Expected validation failure.");
	}

	public void RecoveryActionsReflectTheFailureAndNotifyBindings()
	{
		var item = new NxmDownloadItem { State = NxmDownloadState.InstallFailed };
		var notified = false;
		item.PropertyChanged += (_, e) => notified |= e.PropertyName == nameof(item.StatusText);
		item.ErrorCode = "missing-dependencies";
		item.ErrorDetails = "Goon's Library is missing.";
		RegressionAssert.True(notified);
		RegressionAssert.Equal("Blocked by dependencies", item.StatusText);
		RegressionAssert.Equal(item.ErrorDetails, item.FailureDetails);
		RegressionAssert.True(item.CanRetryInstall);
		RegressionAssert.True(item.CanDownloadAgain);
		item.ErrorCode = "archive-unreadable";
		RegressionAssert.False(item.CanRetryInstall);
		RegressionAssert.True(item.CanDownloadAgain);
		item.ErrorCode = "rollback-failed";
		RegressionAssert.False(item.CanRetryInstall);
		RegressionAssert.False(item.CanDownloadAgain);
		item.State = NxmDownloadState.Installed;
		item.ErrorCode = "";
		RegressionAssert.False(item.CanDownloadAgain);
	}
}
