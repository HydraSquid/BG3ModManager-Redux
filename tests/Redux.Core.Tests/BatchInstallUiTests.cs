using System;
using System.Threading.Tasks;
using DivinityModManager.Models.NexusMods;
using DivinityModManager.ViewModels;
using DivinityModManager.Views;

namespace Redux.Core.Tests;

public sealed class BatchInstallUiTests
{
	public void ToolbarPrioritizesFailuresAndClearsWhenPackagesAreInstalled()
	{
		var ready = new NxmDownloadItem { State = NxmDownloadState.Downloaded };
		var attention = new NxmDownloadItem { State = NxmDownloadState.NeedsFreshLink };
		var failure = new NxmDownloadItem { State = NxmDownloadState.InstallFailed };
		RegressionAssert.Equal("Ready", MainWindowViewModel.GetDownloadManagerStatus([ready]));
		RegressionAssert.Equal("Warning", MainWindowViewModel.GetDownloadManagerStatus([ready, attention]));
		RegressionAssert.Equal("Error", MainWindowViewModel.GetDownloadManagerStatus([ready, attention, failure]));
		ready.State = NxmDownloadState.Installed;
		RegressionAssert.Equal("Normal", MainWindowViewModel.GetDownloadManagerStatus([ready]));
	}

	public void ProgressCannotCloseDuringWorkAndReleasesAfterFailure()
	{
		var progress = new ReduxInstallProgressWindow(null!);
		var ran = false;
		try
		{
			progress.Run(async () =>
			{
				ran = true;
				progress.Close();
				RegressionAssert.True(progress.IsVisible);
				await progress.ReportAsync("Checking package", "Example mod", 1, 2);
				throw new InvalidOperationException("fixture failure");
			});
			throw new Exception("Expected operation failure");
		}
		catch (InvalidOperationException ex) when (ex.Message == "fixture failure") { }
		RegressionAssert.True(ran);
		RegressionAssert.False(progress.IsVisible);
	}
}
