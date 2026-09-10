using System;
using System.Collections.Generic;

namespace Redux.Installer.Tests;

internal static class Program
{
	private static int Main()
	{
		var manifest = new InstallerManifestTests();
		var package = new InstallerPackageTests();
		var environment = new InstallerEnvironmentTests();
		var transaction = new InstallerTransactionTests();
		var checks = new List<(string Name, Action Run)>
		{
			(nameof(manifest.ExactPublicAlphaManifestIsAccepted), manifest.ExactPublicAlphaManifestIsAccepted),
			(nameof(manifest.HotfixAwarePublicAlphaManifestIsAccepted), manifest.HotfixAwarePublicAlphaManifestIsAccepted),
			(nameof(manifest.DuplicateAndUnknownManifestPropertiesAreRejected), manifest.DuplicateAndUnknownManifestPropertiesAreRejected),
			(nameof(manifest.ManifestCannotRedirectSetupOutsideOfficialVersionedRelease), manifest.ManifestCannotRedirectSetupOutsideOfficialVersionedRelease),
			(nameof(package.VerifiedReleaseIsPreparedWithoutChangingAnInstallLocation), package.VerifiedReleaseIsPreparedWithoutChangingAnInstallLocation),
			(nameof(package.ChecksumMismatchLeavesNoPreparedPackage), package.ChecksumMismatchLeavesNoPreparedPackage),
			(nameof(package.ReleaseInventoryCannotClaimReduxUserState), package.ReleaseInventoryCannotClaimReduxUserState),
			(nameof(environment.DesktopRuntimeDetectionRequiresTheX64VersionEightFamily), environment.DesktopRuntimeDetectionRequiresTheX64VersionEightFamily),
			(nameof(environment.RecommendedPerUserDestinationIsWritableAndOutsideTheGame), environment.RecommendedPerUserDestinationIsWritableAndOutsideTheGame),
			(nameof(environment.GameDirectoryAndNonemptyFoldersAreRejected), environment.GameDirectoryAndNonemptyFoldersAreRejected),
			(nameof(environment.CurrentAndLegacyRuntimeNamesAreRecognizedAsExistingInstalls), environment.CurrentAndLegacyRuntimeNamesAreRecognizedAsExistingInstalls),
			(nameof(environment.RuntimeDownloadAcceptsOnlyMicrosoftX64DesktopRuntimeAssets), environment.RuntimeDownloadAcceptsOnlyMicrosoftX64DesktopRuntimeAssets),
			(nameof(transaction.FreshInstallCommitsReviewedFilesAndAnIndependentUninstaller), transaction.FreshInstallCommitsReviewedFilesAndAnIndependentUninstaller),
			(nameof(transaction.IntegrationFailureRollsBackTheFreshApplicationDirectory), transaction.IntegrationFailureRollsBackTheFreshApplicationDirectory),
			(nameof(transaction.UninstallRemovesOnlyReleaseFilesAndPreservesUserContent), transaction.UninstallRemovesOnlyReleaseFilesAndPreservesUserContent),
			(nameof(transaction.CleanUninstallDoesNotMistakeTheRunningUninstallerForUserContent), transaction.CleanUninstallDoesNotMistakeTheRunningUninstallerForUserContent)
		};
		var failed = 0;
		foreach (var check in checks)
		{
			try { check.Run(); Console.WriteLine("PASS " + check.Name); }
			catch (Exception ex) { failed++; Console.Error.WriteLine("FAIL " + check.Name + ": " + ex); }
		}
		Console.WriteLine((checks.Count - failed) + "/" + checks.Count + " installer checks passed.");
		return failed == 0 ? 0 : 1;
	}
}
