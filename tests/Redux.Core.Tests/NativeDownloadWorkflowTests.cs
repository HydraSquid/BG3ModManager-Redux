using System;
using System.Collections.Generic;
using System.IO;
using DivinityModManager.AppServices;
using DivinityModManager.Models.NexusMods;
using DivinityModManager.ViewModels;

namespace Redux.Core.Tests;

internal sealed class NativeDownloadWorkflowTests
{
	public void NativeGameBuildUsesTheFullProductVersion()
	{
		RegressionAssert.Equal(new Version(4, 1, 1, 7398727), MainWindowViewModel.ParseNativeGameVersion("4.1.1.7398727"));
		RegressionAssert.Equal(new Version(4, 1, 1, 6931813), MainWindowViewModel.ParseNativeGameVersion(" 4.1.1.6931813 "));
		RegressionAssert.True(MainWindowViewModel.ParseNativeGameVersion("Unknown") == null);
		RegressionAssert.True(MainWindowViewModel.ParseNativeGameVersion("") == null);
	}

	public void LoaderDownloadProvidesRatherThanRequiresAnExistingLoader()
	{
		var missing = new NativeLoaderStatus(false, false, "Native Mod Loader is missing.");
		var badge = MainWindowViewModel.GetNativeRequirementPresentation(NativeModCatalog.Find(944)!, missing);
		RegressionAssert.False(badge.Warning);
		RegressionAssert.Equal("Installs Native Mod Loader", badge.Label);
		var loader = new NxmInstallCandidate(new NxmDownloadItem { ModId = 944, State = NxmDownloadState.Downloaded }, []);
		var plan = NxmBatchInstallPlanner.Build([loader], [], nativeLoaderPresent: false);
		RegressionAssert.Equal(0, plan.Blocked.Count);
		RegressionAssert.Equal(loader, plan.Ordered[0]);
		RegressionAssert.True(NxmBatchInstallPlanner.GetExecutionBlockReason(plan, loader, new HashSet<NxmDownloadItem>(), [], false) == null);
	}

	public void OnlyDependentNativePluginsWarnWhenTheLoaderIsMissing()
	{
		foreach (var id in new long[] { 781, 945 })
		{
			var badge = MainWindowViewModel.GetNativeRequirementPresentation(NativeModCatalog.Find(id)!, new(false, false, "Missing"));
			RegressionAssert.True(badge.Warning);
			RegressionAssert.Equal("Loader missing / blocked", badge.Label);
			var installed = MainWindowViewModel.GetNativeRequirementPresentation(NativeModCatalog.Find(id)!, new(true, true, "Verified"));
			RegressionAssert.False(installed.Warning);
			RegressionAssert.Equal("Loader verified", installed.Label);
		}
	}

	public void NativeBatchFailuresKeepTheirReasonWithoutExposingRawIoErrors()
	{
		const string reason = "WASD Character Movement requires BG3 4.1.1.6931813 (Hotfix 34) or newer.";
		RegressionAssert.Equal(reason, MainWindowViewModel.DescribeNxmInspectionFailure(781, new InvalidOperationException(reason)));
		RegressionAssert.Equal("Unexpected archive layout.", MainWindowViewModel.DescribeNxmInspectionFailure(944, new InvalidDataException("Unexpected archive layout.")));
		const string capability = "https://example.invalid/file?key=private";
		RegressionAssert.False(MainWindowViewModel.DescribeNxmInspectionFailure(781, new IOException(capability)).Contains(capability));
		RegressionAssert.False(MainWindowViewModel.DescribeNxmInspectionFailure(99999, new InvalidOperationException(capability)).Contains(capability));
	}
}
