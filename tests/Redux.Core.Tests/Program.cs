using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;

using DivinityModManager.Models;

namespace Redux.Core.Tests;

internal static class Program
{
	[STAThread]
	private static int Main()
	{
		// Register WPF's pack URI support before exercising GUI-owned, nonvisual
		// services such as the portable Redux bundle reader/writer.
		_ = Application.Current ?? new Application();

		var source = new SourceAssociationTests();
		var manifest = new CreatorManifestValidationTests();
		var health = new ModHealthTests();
		var modules = new ReduxModuleStateTests();
		var tableStriping = new TableStripingTests();
		var windowShutdown = new WindowShutdownTests();
		var failureRecovery = new NxmFailureRecoveryTests();
		var dependencies = new DependencyAssistanceTests();
		var bundle = new ReduxBundleTests();
		var contribution = new ContributionReportPrivacyTests();
		var comparison = new LoadOrderComparisonTests();
		var restorePoints = new LoadOrderRestorePointTests();
		var annotations = new ModAnnotationTests();
		var overlaps = new ModFileOverlapTests();
		var preflight = new PackagePreflightTests();
		var archivePreflight = new ArchivePackagePreflightTests();
		var interactionPerformance = new InteractionPerformanceTests();
		var interactionBehavior = new InteractionBehaviorTests();
		var automaticCategories = new AutomaticModCategoryTests();
		var visualDividerDrag = new VisualDividerDragPolicyTests();
		var visualModSelection = new VisualModSelectionPolicyTests();
		var settingsMaintenance = new SettingsMaintenanceTests();
		var smoothLogicalScroll = new SmoothLogicalScrollPolicyTests();
		var startupNotifications = new StartupNotificationQueueTests();
		var nxmLinks = new NexusModManagerLinkTests();
		var nxmAssociation = new NxmAssociationTests();
		var nxmActivation = new NxmActivationTests();
		var nxmResolver = new NexusNxmResolverTests();
		var nxmImporter = new NexusDownloadedModImporterTests();
		var nxmStore = new NxmDownloadStoreTests();
		var nxmScheduler = new NxmDownloadSchedulerTests();
		var nxmTransfer = new NxmTransferTests();
		var nxmManager = new NxmDownloadManagerTests();
		var commandPaletteSearch = new CommandPaletteSearchTests();
		var fileSafety = new FileSafetyTests();
		var loadOrderWorkflow = new LoadOrderWorkflowTests();
		var tests = new (string Name, Action Run)[]
		{
			(nameof(source.ReviewedModuleUuidResolvesItsProject), source.ReviewedModuleUuidResolvesItsProject),
			(nameof(source.MissingDependencyOffersReviewedSourceOnlyWhenIntegrationsAreEnabled), source.MissingDependencyOffersReviewedSourceOnlyWhenIntegrationsAreEnabled),
			(nameof(source.CurrentNexusArchiveNamesResolveTheirProject), source.CurrentNexusArchiveNamesResolveTheirProject),
			(nameof(source.TransitionalNexusArchiveNamesResolveTheirProject), source.TransitionalNexusArchiveNamesResolveTheirProject),
			(nameof(source.LegacyNexusArchiveNamesResolveTheirProjectWithoutInventingAFileId), source.LegacyNexusArchiveNamesResolveTheirProjectWithoutInventingAFileId),
			(nameof(source.UnrelatedNumberedArchiveNamesRemainUnmatched), source.UnrelatedNumberedArchiveNamesRemainUnmatched),
			(nameof(source.ModioPageLinkCannotBeMisreadAsNexusProjectThree), source.ModioPageLinkCannotBeMisreadAsNexusProjectThree),
			(nameof(source.ManualModioLinkShowsItsProviderWithoutApiMetadata), source.ManualModioLinkShowsItsProviderWithoutApiMetadata),
			(nameof(source.MatchingNexusCreatorAndUploaderUseOneLinkedCreatorLabel), source.MatchingNexusCreatorAndUploaderUseOneLinkedCreatorLabel),
			(nameof(source.ManualNexusAssociationWinsOverCachedModioMetadata), source.ManualNexusAssociationWinsOverCachedModioMetadata),
			(nameof(source.CachedModioMetadataWinsOverAutomaticNexusMetadata), source.CachedModioMetadataWinsOverAutomaticNexusMetadata),
			(nameof(source.NativePublishHandleWinsOverAutomaticNexusMetadataWithoutAnApiKey), source.NativePublishHandleWinsOverAutomaticNexusMetadataWithoutAnApiKey),
			(nameof(source.NativeModioPackageRejectsOnlyAutomaticNexusImportMatches), source.NativeModioPackageRejectsOnlyAutomaticNexusImportMatches),
			(nameof(source.NexusArchiveImportWinsOverNativeModioMetadata), source.NexusArchiveImportWinsOverNativeModioMetadata),
			(nameof(source.FreshNexusInstallWinsBeforeMetadataFetch), source.FreshNexusInstallWinsBeforeMetadataFetch),
			(nameof(source.ExplicitNexusOriginWithoutAProjectCannotOverrideModio), source.ExplicitNexusOriginWithoutAProjectCannotOverrideModio),
			(nameof(source.NexusArchiveCacheBlocksNativeModioDiscoveryAfterRestart), source.NexusArchiveCacheBlocksNativeModioDiscoveryAfterRestart),
			(nameof(source.ReduxBundleNexusLinkOverridesOnlyWhenExplicitlyApplied), source.ReduxBundleNexusLinkOverridesOnlyWhenExplicitlyApplied),
			(nameof(source.PortableSourceLinkUsesTheDisplayedProviderWithoutPrivateUrlData), source.PortableSourceLinkUsesTheDisplayedProviderWithoutPrivateUrlData),
			(nameof(source.DeletingAnInstalledModRetiresItsRememberedSourceAssociations), source.DeletingAnInstalledModRetiresItsRememberedSourceAssociations),
			(nameof(source.LocalOnlyPresentationHidesProvidersWithoutDeletingCachedMetadata), source.LocalOnlyPresentationHidesProvidersWithoutDeletingCachedMetadata),
			(nameof(source.LocalMetadataUsesExplicitUnavailableFallbacks), source.LocalMetadataUsesExplicitUnavailableFallbacks),
			(nameof(source.CreatorManifestModioCacheRequiresTheCurrentProjectClaim), source.CreatorManifestModioCacheRequiresTheCurrentProjectClaim),
			(nameof(source.ManualSourceChoicesBlockCreatorManifestModioCache), source.ManualSourceChoicesBlockCreatorManifestModioCache),
			(nameof(source.InvalidManualModioCacheEntryIsRejected), source.InvalidManualModioCacheEntryIsRejected),
			(nameof(source.NativeModioCacheDoesNotDependOnCreatorManifest), source.NativeModioCacheDoesNotDependOnCreatorManifest),
			(nameof(source.NativePublishHandleRejectsAnInferredNexusCacheEntry), source.NativePublishHandleRejectsAnInferredNexusCacheEntry),
			(nameof(source.ManualModioAssociationRejectsAStaleManualNexusCacheEntry), source.ManualModioAssociationRejectsAStaleManualNexusCacheEntry),
			(nameof(source.CreatorManifestNexusCacheRequiresTheCurrentProjectClaim), source.CreatorManifestNexusCacheRequiresTheCurrentProjectClaim),
			(nameof(source.ManualAndNativeSourceChoicesBlockCreatorManifestNexusCache), source.ManualAndNativeSourceChoicesBlockCreatorManifestNexusCache),
			(nameof(source.NonManifestNexusCacheDoesNotDependOnCreatorManifest), source.NonManifestNexusCacheDoesNotDependOnCreatorManifest),
			(nameof(source.ValidCreatorManifestNexusCacheSurvivesRestart), source.ValidCreatorManifestNexusCacheSurvivesRestart),
			(nameof(source.ChangedCreatorManifestInvalidatesReloadedNexusCache), source.ChangedCreatorManifestInvalidatesReloadedNexusCache),
			(nameof(source.NativeModioCacheWinsOverCreatorManifestNexusAfterRestart), source.NativeModioCacheWinsOverCreatorManifestNexusAfterRestart),
			(nameof(source.ManualNexusCacheBlocksCreatorManifestModioAfterRestart), source.ManualNexusCacheBlocksCreatorManifestModioAfterRestart),
			(nameof(manifest.ValidManifestPreservesCreatorAuthorOrder), manifest.ValidManifestPreservesCreatorAuthorOrder),
			(nameof(manifest.CompactNexusManifestLinksThePrimaryModule), manifest.CompactNexusManifestLinksThePrimaryModule),
			(nameof(manifest.CompactNexusManifestRejectsAnUnrelatedModule), manifest.CompactNexusManifestRejectsAnUnrelatedModule),
			(nameof(manifest.CompactNexusManifestRejectsASecondaryModule), manifest.CompactNexusManifestRejectsASecondaryModule),
			(nameof(manifest.CompactNexusManifestPreservesAnOptionalFileId), manifest.CompactNexusManifestPreservesAnOptionalFileId),
			(nameof(manifest.CompactAndDetailedManifestFormsCannotBeMixed), manifest.CompactAndDetailedManifestFormsCannotBeMixed),
			(nameof(manifest.DuplicateAuthorsAreRejected), manifest.DuplicateAuthorsAreRejected),
			(nameof(manifest.MismatchedPakClaimIsRejected), manifest.MismatchedPakClaimIsRejected),
			(nameof(manifest.DuplicateJsonPropertiesAreRejected), manifest.DuplicateJsonPropertiesAreRejected),
			(nameof(manifest.TrailingJsonContentIsRejected), manifest.TrailingJsonContentIsRejected),
			(nameof(manifest.HomepageMustUsePublicHttpOrHttps), manifest.HomepageMustUsePublicHttpOrHttps),
			(nameof(manifest.PakExtensionMatchingIsCaseInsensitive), manifest.PakExtensionMatchingIsCaseInsensitive),
			(nameof(health.MissingAndInactiveDependenciesRemainIndependentOfLoadOrderGuidance), health.MissingAndInactiveDependenciesRemainIndependentOfLoadOrderGuidance),
			(nameof(health.LoadOrderGuidanceFindingsAreAbsentUntilEnabled), health.LoadOrderGuidanceFindingsAreAbsentUntilEnabled),
			(nameof(health.CorrectDependencyPlacementDoesNotProduceGuidanceNoise), health.CorrectDependencyPlacementDoesNotProduceGuidanceNoise),
			(nameof(health.LoadOrderGuidanceAppliesOnlyToNormalActiveEntries), health.LoadOrderGuidanceAppliesOnlyToNormalActiveEntries),
			(nameof(health.InvalidUuidIsReportedAsAReadOnlyHealthError), health.InvalidUuidIsReportedAsAReadOnlyHealthError),
			(nameof(health.SelfDependencyIsReportedWithoutDuplicateMissingDependencyNoise), health.SelfDependencyIsReportedWithoutDuplicateMissingDependencyNoise),
			(nameof(health.DependencyCyclesAreReportedOnlyByOptInGuidance), health.DependencyCyclesAreReportedOnlyByOptInGuidance),
			(nameof(health.InvalidCreatorManifestIsReportedWithoutApplyingItsClaims), health.InvalidCreatorManifestIsReportedWithoutApplyingItsClaims),
			(nameof(health.DuplicateUuidsAreReportedWithoutRemovingEitherPackage), health.DuplicateUuidsAreReportedWithoutRemovingEitherPackage),
			(nameof(health.ActiveDeclaredConflictsAreReportedConservatively), health.ActiveDeclaredConflictsAreReportedConservatively),
			(nameof(health.OlderInstalledDependencyVersionsAreReportedWithoutUpdatingThem), health.OlderInstalledDependencyVersionsAreReportedWithoutUpdatingThem),
			(nameof(health.ScriptExtenderErrorsAndWarningsRemainDistinct), health.ScriptExtenderErrorsAndWarningsRemainDistinct),
			(nameof(health.ForceLoadedVariantsRemainInformationalAndReadOnly), health.ForceLoadedVariantsRemainInformationalAndReadOnly),
			(nameof(health.LocalOnlyPresentationSuppressesProviderFindingsWithoutDeletingMetadata), health.LocalOnlyPresentationSuppressesProviderFindingsWithoutDeletingMetadata),
			(nameof(health.InactiveMcmExplainsItsInGameLoadOrderWarning), health.InactiveMcmExplainsItsInGameLoadOrderWarning),
			(nameof(health.ModioWarningExplainsSteamCloudPersistence), health.ModioWarningExplainsSteamCloudPersistence),
			(nameof(health.FreshNexusInstallDoesNotReceiveModioManagementWarning), health.FreshNexusInstallDoesNotReceiveModioManagementWarning),
			(nameof(health.DisablingModioWarningsHidesOnlyThatFinding), health.DisablingModioWarningsHidesOnlyThatFinding),
			(nameof(modules.DefaultsKeepModDiagnosticsOnAndGuidanceOptIn), modules.DefaultsKeepModDiagnosticsOnAndGuidanceOptIn),
			(nameof(modules.NexusDownloadsDefaultToConfirmationAndFourTransfers), modules.NexusDownloadsDefaultToConfirmationAndFourTransfers),
			(nameof(modules.FirstRunOnboardingStartsWithEveryOptionalFeatureOff), modules.FirstRunOnboardingStartsWithEveryOptionalFeatureOff),
			(nameof(modules.ReturningUsersKeepTheirOptionalFeatureChoices), modules.ReturningUsersKeepTheirOptionalFeatureChoices),
			(nameof(modules.CategoryInteractionSettingSynchronizesLegacyPresentationFlags), modules.CategoryInteractionSettingSynchronizesLegacyPresentationFlags),
			(nameof(modules.IconsOnlySettingSynchronizesLegacySourceFlag), modules.IconsOnlySettingSynchronizesLegacySourceFlag),
			(nameof(modules.CustomThemeClonePreservesUnifiedPresentationSettings), modules.CustomThemeClonePreservesUnifiedPresentationSettings),
			(nameof(modules.CustomThemePreviewRegeneratesEverySemanticPillGradient), modules.CustomThemePreviewRegeneratesEverySemanticPillGradient),
			(nameof(modules.CustomThemeBackgroundEditsPreserveUntouchedBaseRoles), modules.CustomThemeBackgroundEditsPreserveUntouchedBaseRoles),
			(nameof(tableStriping.TablesAlternateAcrossThemesAndLivePaletteChanges), tableStriping.TablesAlternateAcrossThemesAndLivePaletteChanges),
			(nameof(nxmImporter.UnreadableArchiveHasAnActionableFailureWithoutInstalling), nxmImporter.UnreadableArchiveHasAnActionableFailureWithoutInstalling),
			(nameof(nxmImporter.BundledDependenciesAreIdentifiedBeforeAnyPackagePreflight), nxmImporter.BundledDependenciesAreIdentifiedBeforeAnyPackagePreflight),
			(nameof(failureRecovery.MissingDependencyFailureNamesTheRequirementAndRecovery), failureRecovery.MissingDependencyFailureNamesTheRequirementAndRecovery),
			(nameof(failureRecovery.FailureDescriptionsNeverExposeRawExceptionCapabilities), failureRecovery.FailureDescriptionsNeverExposeRawExceptionCapabilities),
			(nameof(failureRecovery.RecoveryActionsReflectTheFailureAndNotifyBindings), failureRecovery.RecoveryActionsReflectTheFailureAndNotifyBindings),
			(nameof(dependencies.InstalledDependenciesAreDistinguishedFromMissingDownloads), dependencies.InstalledDependenciesAreDistinguishedFromMissingDownloads),
			(nameof(dependencies.SameProjectDownloadIsOnlyAPossibleDependencyMatch), dependencies.SameProjectDownloadIsOnlyAPossibleDependencyMatch),
			(nameof(dependencies.InspectedUuidMatchesAreInvalidatedByArchiveChanges), dependencies.InspectedUuidMatchesAreInvalidatedByArchiveChanges),
			(nameof(dependencies.SourceLinksRequireReviewedUuidsAndEnabledIntegrations), dependencies.SourceLinksRequireReviewedUuidsAndEnabledIntegrations),
			(nameof(dependencies.BundledAndOlderInstalledDependenciesAreExplained), dependencies.BundledAndOlderInstalledDependenciesAreExplained),
			(nameof(nxmStore.DetailedInstallFailureSurvivesRestartAndMissingFileInvalidation), nxmStore.DetailedInstallFailureSurvivesRestartAndMissingFileInvalidation),
			(nameof(nxmManager.DownloadAgainPreservesTheArchiveAndUsesFreshAuthorizationWhenRequired), nxmManager.DownloadAgainPreservesTheArchiveAndUsesFreshAuthorizationWhenRequired),
			(nameof(nxmManager.FailedRedownloadSavePreservesTheOriginalQueueRecord), nxmManager.FailedRedownloadSavePreservesTheOriginalQueueRecord),
			(nameof(nxmManager.DownloadAgainNeverReusesExistingPartialData), nxmManager.DownloadAgainNeverReusesExistingPartialData),
			(nameof(nxmManager.RedownloadAfterMetadataFailureUsesResolvedPakExtension), nxmManager.RedownloadAfterMetadataFailureUsesResolvedPakExtension),
			(nameof(windowShutdown.IdleShutdownDefersFinalCloseUntilClosingReturns), windowShutdown.IdleShutdownDefersFinalCloseUntilClosingReturns),
			(nameof(windowShutdown.FailedShutdownKeepsMainWindowVisibleAndCanBeRetried), windowShutdown.FailedShutdownKeepsMainWindowVisibleAndCanBeRetried),
			(nameof(windowShutdown.CanceledClosingDoesNotStartNexusShutdownAndCanBeRetried), windowShutdown.CanceledClosingDoesNotStartNexusShutdownAndCanBeRetried),
			(nameof(modules.LocalOnlyModeChangesOnlySourceIntegrations), modules.LocalOnlyModeChangesOnlySourceIntegrations),
			(nameof(modules.LoadOrderGuidanceRequiresDiagnosticsWithoutLosingItsPreference), modules.LoadOrderGuidanceRequiresDiagnosticsWithoutLosingItsPreference),
			(nameof(modules.DisposedModuleStateStopsTrackingSettings), modules.DisposedModuleStateStopsTrackingSettings),
			(nameof(modules.DisabledNexusProviderCannotInitializeItsClient), modules.DisabledNexusProviderCannotInitializeItsClient),
			(nameof(bundle.BundleRoundTripPreservesOrderAndReduxPresentation), bundle.BundleRoundTripPreservesOrderAndReduxPresentation),
			(nameof(bundle.SourceLinksCannotReferenceModsOutsideTheOrder), bundle.SourceLinksCannotReferenceModsOutsideTheOrder),
			(nameof(bundle.LegacyBundleWithoutMembershipRemainsUnmigrated), bundle.LegacyBundleWithoutMembershipRemainsUnmigrated),
			(nameof(bundle.ExplicitlyEmptySeparatorMembershipRoundTripsAsEmpty), bundle.ExplicitlyEmptySeparatorMembershipRoundTripsAsEmpty),
			(nameof(bundle.DuplicateSeparatorOwnershipIsRejected), bundle.DuplicateSeparatorOwnershipIsRejected),
			(nameof(bundle.BundleNeverContainsModsettingsLsx), bundle.BundleNeverContainsModsettingsLsx),
			(nameof(bundle.MismatchedOrderAndPresentationAreRejected), bundle.MismatchedOrderAndPresentationAreRejected),
			(nameof(bundle.UnexpectedFilesAreRejectedDuringImport), bundle.UnexpectedFilesAreRejectedDuringImport),
			(nameof(bundle.ExistingBundleCanBeAtomicallyReplaced), bundle.ExistingBundleCanBeAtomicallyReplaced),
			(nameof(bundle.FailedReplacementPreservesTheExistingBundle), bundle.FailedReplacementPreservesTheExistingBundle),
			(nameof(bundle.PrivateNotesRoundTripOnlyWhenPresent), bundle.PrivateNotesRoundTripOnlyWhenPresent),
			(nameof(bundle.PrivateNotesCannotReferenceModsOutsideTheOrder), bundle.PrivateNotesCannotReferenceModsOutsideTheOrder),
			(nameof(contribution.ContributionReportsIncludeOnlyUniqueInstalledUserMods), contribution.ContributionReportsIncludeOnlyUniqueInstalledUserMods),
			(nameof(contribution.ContributionReportsStripPrivatePathsAndOrderingData), contribution.ContributionReportsStripPrivatePathsAndOrderingData),
			(nameof(contribution.ContributionReportsPreserveKnownNexusIdentifiers), contribution.ContributionReportsPreserveKnownNexusIdentifiers),
			(nameof(contribution.ContributionReportsRejectCredentialBearingProviderUrls), contribution.ContributionReportsRejectCredentialBearingProviderUrls),
			(nameof(contribution.TamperedContributionReportsCannotBeSaved), contribution.TamperedContributionReportsCannotBeSaved),
			(nameof(comparison.ReportsActivationDeactivationAndAutomaticDependencies), comparison.ReportsActivationDeactivationAndAutomaticDependencies),
			(nameof(comparison.SavedOrderComparisonTreatsRightOnlyModsAsIntentionalAdditions), comparison.SavedOrderComparisonTreatsRightOnlyModsAsIntentionalAdditions),
			(nameof(comparison.AddedOrRemovedModsDoNotCreateFalsePositionChanges), comparison.AddedOrRemovedModsDoNotCreateFalsePositionChanges),
			(nameof(comparison.ReportsTheSmallestPlacementChangeForASingleMove), comparison.ReportsTheSmallestPlacementChangeForASingleMove),
			(nameof(comparison.IgnoresDuplicateAndBlankEntriesButRetainsMissingBaselineMods), comparison.IgnoresDuplicateAndBlankEntriesButRetainsMissingBaselineMods),
			(nameof(comparison.PreservesFirstExportState), comparison.PreservesFirstExportState),
			(nameof(restorePoints.RoundTripPreservesProfileReasonAndOrder), restorePoints.RoundTripPreservesProfileReasonAndOrder),
			(nameof(restorePoints.EmptyExportedOrderCanBeRestored), restorePoints.EmptyExportedOrderCanBeRestored),
			(nameof(restorePoints.RetentionKeepsOnlyTheNewestTwentySnapshots), restorePoints.RetentionKeepsOnlyTheNewestTwentySnapshots),
			(nameof(restorePoints.RestorePointsFromAnotherProfileAreRejected), restorePoints.RestorePointsFromAnotherProfileAreRejected),
			(nameof(restorePoints.InvalidSnapshotsAreIgnoredWithoutLeavingTemporaryFiles), restorePoints.InvalidSnapshotsAreIgnoredWithoutLeavingTemporaryFiles),
			(nameof(restorePoints.DeleteRemovesOnlyTheMatchingProfileSnapshot), restorePoints.DeleteRemovesOnlyTheMatchingProfileSnapshot),
			(nameof(annotations.AnnotationsRoundTripWithoutPackageOrProfileData), annotations.AnnotationsRoundTripWithoutPackageOrProfileData),
			(nameof(annotations.ClearingTheLastValueRemovesTheAnnotation), annotations.ClearingTheLastValueRemovesTheAnnotation),
			(nameof(annotations.OversizedNotesAreRejectedBeforeTheStoreChanges), annotations.OversizedNotesAreRejectedBeforeTheStoreChanges),
			(nameof(annotations.BulkNotesUpdateAtomically), annotations.BulkNotesUpdateAtomically),
			(nameof(overlaps.NormalizesSlashAndCaseDifferences), overlaps.NormalizesSlashAndCaseDifferences),
			(nameof(overlaps.DuplicatePathsInsideOnePackageAreNotOverlaps), overlaps.DuplicatePathsInsideOnePackageAreNotOverlaps),
			(nameof(overlaps.ExcludesUniquePathsAndCountsAffectedPackages), overlaps.ExcludesUniquePathsAndCountsAffectedPackages),
			(nameof(overlaps.OrdersBroadestOverlapsBeforePathName), overlaps.OrdersBroadestOverlapsBeforePathName),
			(nameof(overlaps.MalformedPackagePathsAreReportedWithoutAbortingTheScan), overlaps.MalformedPackagePathsAreReportedWithoutAbortingTheScan),
			(nameof(preflight.ValidPackageHasNoBlockingFindings), preflight.ValidPackageHasNoBlockingFindings),
			(nameof(preflight.MissingDependencyAndDevelopmentDebrisAreReported), preflight.MissingDependencyAndDevelopmentDebrisAreReported),
			(nameof(preflight.InstalledUpdateUuidIsAReviewWarningInsteadOfADuplicateError), preflight.InstalledUpdateUuidIsAReviewWarningInsteadOfADuplicateError),
			(nameof(preflight.MissingReleaseIdentityIsReportedConservatively), preflight.MissingReleaseIdentityIsReportedConservatively),
			(nameof(archivePreflight.OrdinaryArchiveLayoutHasNoContainerFindings), archivePreflight.OrdinaryArchiveLayoutHasNoContainerFindings),
			(nameof(archivePreflight.UnsafePathsDuplicatesAndDevelopmentDebrisAreReported), archivePreflight.UnsafePathsDuplicatesAndDevelopmentDebrisAreReported),
			(nameof(archivePreflight.ArchiveWithoutPakIsReported), archivePreflight.ArchiveWithoutPakIsReported),
			(nameof(archivePreflight.ZipPakIsStagedForInspectionWithoutChangingTheArchive), archivePreflight.ZipPakIsStagedForInspectionWithoutChangingTheArchive),
			(nameof(interactionPerformance.ReorderingOneRowEmitsOneMoveInsteadOfACollectionReset), interactionPerformance.ReorderingOneRowEmitsOneMoveInsteadOfACollectionReset),
			(nameof(interactionPerformance.RemovingOneRowDoesNotMoveOrResetTheRemainingRows), interactionPerformance.RemovingOneRowDoesNotMoveOrResetTheRemainingRows),
			(nameof(interactionPerformance.UnchangedLargeCollectionUsesLinearComparisonWork), interactionPerformance.UnchangedLargeCollectionUsesLinearComparisonWork),
			(nameof(interactionPerformance.LargeSeparatorProjectionUsesOneCollectionReset), interactionPerformance.LargeSeparatorProjectionUsesOneCollectionReset),
			(nameof(interactionPerformance.SmallSeparatorProjectionKeepsIncrementalNotifications), interactionPerformance.SmallSeparatorProjectionKeepsIncrementalNotifications),
			(nameof(interactionPerformance.AnimatedSeparatorProjectionPreservesRecyclableContainers), interactionPerformance.AnimatedSeparatorProjectionPreservesRecyclableContainers),
			(nameof(interactionPerformance.ImportProgressIsSharedAcrossFilesAndNeverExceedsOne), interactionPerformance.ImportProgressIsSharedAcrossFilesAndNeverExceedsOne),
			(nameof(interactionPerformance.EquivalentCategoryAndHealthDataCanReuseExistingRowBindings), interactionPerformance.EquivalentCategoryAndHealthDataCanReuseExistingRowBindings),
			(nameof(interactionBehavior.DrawerRetainsASelectedModDuringCrossListTransferOnly), interactionBehavior.DrawerRetainsASelectedModDuringCrossListTransferOnly),
			(nameof(interactionBehavior.CrossListSelectionClearWaitsForTheCurrentSelectionTransaction), interactionBehavior.CrossListSelectionClearWaitsForTheCurrentSelectionTransaction),
			(nameof(interactionBehavior.NewerSelectionSupersedesQueuedCrossListClear), interactionBehavior.NewerSelectionSupersedesQueuedCrossListClear),
			(nameof(interactionBehavior.ReentrantSelectionSupersedesAnExecutingClear), interactionBehavior.ReentrantSelectionSupersedesAnExecutingClear),
			(nameof(interactionBehavior.SavingCurrentOrderCanNeverWriteTheGameExportFile), interactionBehavior.SavingCurrentOrderCanNeverWriteTheGameExportFile),
			(nameof(interactionBehavior.NewBlankOrderContainsNoActivatedMods), interactionBehavior.NewBlankOrderContainsNoActivatedMods),
			(nameof(interactionBehavior.WorkingChangesStayDetachedUntilExplicitlySaved), interactionBehavior.WorkingChangesStayDetachedUntilExplicitlySaved),
			(nameof(interactionBehavior.ShutdownSnapshotNeverReplacesTheUnsavedWorkingPresentation), interactionBehavior.ShutdownSnapshotNeverReplacesTheUnsavedWorkingPresentation),
			(nameof(interactionBehavior.DuplicateWandChoiceNormalizesToTheSingleVisibleIcon), interactionBehavior.DuplicateWandChoiceNormalizesToTheSingleVisibleIcon),
			(nameof(interactionBehavior.AsyncProviderMetadataSignalsAutomaticCategoryRefresh), interactionBehavior.AsyncProviderMetadataSignalsAutomaticCategoryRefresh),
			(nameof(automaticCategories.NexusCategoryIdsMatchTheBg3ProviderTaxonomy), automaticCategories.NexusCategoryIdsMatchTheBg3ProviderTaxonomy),
			(nameof(automaticCategories.ExplicitNexusCategoryWinsOverContradictoryKeywords), automaticCategories.ExplicitNexusCategoryWinsOverContradictoryKeywords),
			(nameof(automaticCategories.NexusCategoryStaysFirstWhileStrongSecondaryCategoriesFillThreeSlots), automaticCategories.NexusCategoryStaysFirstWhileStrongSecondaryCategoriesFillThreeSlots),
			(nameof(automaticCategories.AutomaticCategoriesNeverExceedThree), automaticCategories.AutomaticCategoriesNeverExceedThree),
			(nameof(automaticCategories.WeakDescriptionMentionsDoNotCreateSecondaryCategoryNoise), automaticCategories.WeakDescriptionMentionsDoNotCreateSecondaryCategoryNoise),
			(nameof(automaticCategories.BundledNexusProjectPreservesItsAuthorCategoryOffline), automaticCategories.BundledNexusProjectPreservesItsAuthorCategoryOffline),
			(nameof(automaticCategories.NativeModioCategoryWinsOverASecondaryNexusMatch), automaticCategories.NativeModioCategoryWinsOverASecondaryNexusMatch),
			(nameof(automaticCategories.UnknownProviderTaxonomyFallsBackToPackageKeywords), automaticCategories.UnknownProviderTaxonomyFallsBackToPackageKeywords),
			(nameof(automaticCategories.DisabledProviderCategoryFallsBackToAnEnabledCategory), automaticCategories.DisabledProviderCategoryFallsBackToAnEnabledCategory),
			(nameof(visualDividerDrag.NormalModDragNeverIncludesASelectedDivider), visualDividerDrag.NormalModDragNeverIncludesASelectedDivider),
			(nameof(visualDividerDrag.ExpandedDividerDragContainsOnlyItsMarker), visualDividerDrag.ExpandedDividerDragContainsOnlyItsMarker),
			(nameof(visualDividerDrag.CollapsedDividerDragStartsWithLightweightMarker), visualDividerDrag.CollapsedDividerDragStartsWithLightweightMarker),
			(nameof(visualDividerDrag.CollapsedSeparatorPayloadCarriesOnlyItsSealedMembers), visualDividerDrag.CollapsedSeparatorPayloadCarriesOnlyItsSealedMembers),
			(nameof(visualDividerDrag.CollapsedSeparatorMovesAroundAnotherClosedBlockWithoutAbsorption), visualDividerDrag.CollapsedSeparatorMovesAroundAnotherClosedBlockWithoutAbsorption),
			(nameof(visualDividerDrag.ExpandedSeparatorMoveLeavesEveryModInPlace), visualDividerDrag.ExpandedSeparatorMoveLeavesEveryModInPlace),
			(nameof(visualDividerDrag.RecreatedExpandedSeparatorResolvesToCanonicalMarkerOnly), visualDividerDrag.RecreatedExpandedSeparatorResolvesToCanonicalMarkerOnly),
			(nameof(visualDividerDrag.DropAfterCollapsedSeparatorSkipsItsHiddenSection), visualDividerDrag.DropAfterCollapsedSeparatorSkipsItsHiddenSection),
			(nameof(visualDividerDrag.CollapsedSeparatorDoesNotAdoptAModDroppedBelowItsClosedBlock), visualDividerDrag.CollapsedSeparatorDoesNotAdoptAModDroppedBelowItsClosedBlock),
			(nameof(visualDividerDrag.MovingASeparatorAboveAClosedSectionCannotChangeItsContents), visualDividerDrag.MovingASeparatorAboveAClosedSectionCannotChangeItsContents),
			(nameof(visualDividerDrag.VisibleDropSlotMapsPastOmittedCollapsedMembers), visualDividerDrag.VisibleDropSlotMapsPastOmittedCollapsedMembers),
			(nameof(visualDividerDrag.VisibleDropSlotMatchesRecreatedDividerByIdentity), visualDividerDrag.VisibleDropSlotMatchesRecreatedDividerByIdentity),
			(nameof(visualDividerDrag.ProgressiveExpansionInsertsBeforeUnownedDestinationSuffix), visualDividerDrag.ProgressiveExpansionInsertsBeforeUnownedDestinationSuffix),
			(nameof(visualDividerDrag.ExpansionRestoresClosedMembersBeforeNewlyAdoptedRows), visualDividerDrag.ExpansionRestoresClosedMembersBeforeNewlyAdoptedRows),
			(nameof(visualDividerDrag.CollapseAllChangesOnlyTheRequestedPaneAndOnlyOnce), visualDividerDrag.CollapseAllChangesOnlyTheRequestedPaneAndOnlyOnce),
			(nameof(visualDividerDrag.LegacyPositionsMigrateToDurableSectionMembership), visualDividerDrag.LegacyPositionsMigrateToDurableSectionMembership),
			(nameof(visualDividerDrag.LegacyMembershipWaitsForCompletedListLoading), visualDividerDrag.LegacyMembershipWaitsForCompletedListLoading),
			(nameof(visualDividerDrag.VisualSequencePreservesAuthoritativeModOrder), visualDividerDrag.VisualSequencePreservesAuthoritativeModOrder),
			(nameof(visualDividerDrag.DuplicateOwnershipKeepsFirstDividerAndMissingIds), visualDividerDrag.DuplicateOwnershipKeepsFirstDividerAndMissingIds),
			(nameof(visualDividerDrag.CollapsedVisibilityUsesExplicitMembershipOnly), visualDividerDrag.CollapsedVisibilityUsesExplicitMembershipOnly),
			(nameof(visualDividerDrag.CollapsedVisibilityStopsAtTheNextSeparator), visualDividerDrag.CollapsedVisibilityStopsAtTheNextSeparator),
			(nameof(visualModSelection.SelectAllIncludesOnlyVisibleModRows), visualModSelection.SelectAllIncludesOnlyVisibleModRows),
			(nameof(visualModSelection.FilterProjectionOmitsCollapsedRowsFromTheItemsSource), visualModSelection.FilterProjectionOmitsCollapsedRowsFromTheItemsSource),
			(nameof(settingsMaintenance.RestoringAutomaticCategoriesClearsCurrentAndLegacyAssignmentsOnly), settingsMaintenance.RestoringAutomaticCategoriesClearsCurrentAndLegacyAssignmentsOnly),
			(nameof(settingsMaintenance.RestoringAutomaticCategoriesMakesTheClassifierAuthoritativeAgain), settingsMaintenance.RestoringAutomaticCategoriesMakesTheClassifierAuthoritativeAgain),
			(nameof(smoothLogicalScroll.PartialWheelDeltasAccumulateWithoutPrematureScrolling), smoothLogicalScroll.PartialWheelDeltasAccumulateWithoutPrematureScrolling),
			(nameof(smoothLogicalScroll.LargeWheelBurstsStayWithinTheAnimationSafetyCap), smoothLogicalScroll.LargeWheelBurstsStayWithinTheAnimationSafetyCap),
			(nameof(smoothLogicalScroll.SmoothScrollingIsStandardUnlessMotionOrInteractionSuppressesIt), smoothLogicalScroll.SmoothScrollingIsStandardUnlessMotionOrInteractionSuppressesIt),
			(nameof(smoothLogicalScroll.MixedHeightRowsProduceDirectionCorrectCompensation), smoothLogicalScroll.MixedHeightRowsProduceDirectionCorrectCompensation),
			(nameof(smoothLogicalScroll.MissingCachedRowsUseAStableFallbackWithoutChangingDirection), smoothLogicalScroll.MissingCachedRowsUseAStableFallbackWithoutChangingDirection),
			(nameof(smoothLogicalScroll.ScrollRangeIsKnownBeforeDeferredLayoutPublishesTheNewOffset), smoothLogicalScroll.ScrollRangeIsKnownBeforeDeferredLayoutPublishesTheNewOffset),
			(nameof(startupNotifications.StartupNotificationsWaitForReadinessAndDrainInOrder), startupNotifications.StartupNotificationsWaitForReadinessAndDrainInOrder),
			(nameof(startupNotifications.RepeatedStartupNotificationUsesLatestDataExactlyOnce), startupNotifications.RepeatedStartupNotificationUsesLatestDataExactlyOnce),
			(nameof(startupNotifications.StaleStartupNotificationCanBeCancelledBeforeReadiness), startupNotifications.StaleStartupNotificationCanBeCancelledBeforeReadiness),
			(nameof(startupNotifications.NotificationsQueuedDuringDrainRemainSequential), startupNotifications.NotificationsQueuedDuringDrainRemainSequential),
			(nameof(nxmLinks.ParsesAuthenticatedBg3Link), nxmLinks.ParsesAuthenticatedBg3Link),
			(nameof(nxmLinks.ParsesPremiumBg3LinkWithoutAuthorizationQuery), nxmLinks.ParsesPremiumBg3LinkWithoutAuthorizationQuery),
			(nameof(nxmLinks.RejectsWrongSchemeOrGame), nxmLinks.RejectsWrongSchemeOrGame),
			(nameof(nxmLinks.RejectsUnsafeAuthorityAndPathForms), nxmLinks.RejectsUnsafeAuthorityAndPathForms),
			(nameof(nxmLinks.RejectsMalformedOrUnexpectedQueryData), nxmLinks.RejectsMalformedOrUnexpectedQueryData),
			(nameof(nxmLinks.RejectsExpiredAuthorization), nxmLinks.RejectsExpiredAuthorization),
			(nameof(nxmLinks.RejectsOversizedInput), nxmLinks.RejectsOversizedInput),
			(nameof(nxmLinks.RedactsAuthorizationValues), nxmLinks.RedactsAuthorizationValues)
			,(nameof(nxmAssociation.EnableAndDisableRestoresPriorUserHandler), nxmAssociation.EnableAndDisableRestoresPriorUserHandler)
			,(nameof(nxmAssociation.DisableRevealsMachineHandlerWhenNoUserHandlerExisted), nxmAssociation.DisableRevealsMachineHandlerWhenNoUserHandlerExisted)
			,(nameof(nxmAssociation.RepairUpdatesOnlyOwnedMovedRegistration), nxmAssociation.RepairUpdatesOnlyOwnedMovedRegistration)
			,(nameof(nxmAssociation.DisableNeverOverwritesAnInterveningHandler), nxmAssociation.DisableNeverOverwritesAnInterveningHandler)
			,(nameof(nxmAssociation.DifferentReduxInstallationCannotRepairOrDisableOwner), nxmAssociation.DifferentReduxInstallationCannotRepairOrDisableOwner)
			,(nameof(nxmAssociation.RegistrySnapshotPreservesValueKindsAndSubkeys), nxmAssociation.RegistrySnapshotPreservesValueKindsAndSubkeys)
			,(nameof(nxmAssociation.ProductionRegistryStoreRoundTripsOnlyDisposableHkcuPaths), nxmAssociation.ProductionRegistryStoreRoundTripsOnlyDisposableHkcuPaths)
			,(nameof(nxmAssociation.FailedEnableRestoresPriorHandler), nxmAssociation.FailedEnableRestoresPriorHandler)
			,(nameof(nxmAssociation.FailedDisableKeepsOwnedHandlerAndBackup), nxmAssociation.FailedDisableKeepsOwnedHandlerAndBackup)
			,(nameof(nxmAssociation.ReduxShapedInterveningCommandIsNotOwned), nxmAssociation.ReduxShapedInterveningCommandIsNotOwned)
			,(nameof(nxmAssociation.MissingExecutableMarkerIsAnOwnershipConflict), nxmAssociation.MissingExecutableMarkerIsAnOwnershipConflict)
			,(nameof(nxmAssociation.PreviousHandlerCommandIsParsedWithoutShell), nxmAssociation.PreviousHandlerCommandIsParsedWithoutShell)
			,(nameof(nxmAssociation.PreviousHandlerRejectsEmbeddedPlaceholderAndReduxRecursion), nxmAssociation.PreviousHandlerRejectsEmbeddedPlaceholderAndReduxRecursion)
			,(nameof(nxmActivation.SameUserPipeDeliversValidatedLink), nxmActivation.SameUserPipeDeliversValidatedLink)
			,(nameof(nxmActivation.OversizedMessageIsRejectedBeforeConnection), nxmActivation.OversizedMessageIsRejectedBeforeConnection)
			,(nameof(nxmActivation.ListenerSurvivesMalformedClientMessage), nxmActivation.ListenerSurvivesMalformedClientMessage)
			,(nameof(nxmResolver.FreeUserResolutionUsesExactFileAndAuthorization), nxmResolver.FreeUserResolutionUsesExactFileAndAuthorization)
			,(nameof(nxmResolver.PremiumResolutionDoesNotForwardShortLivedAuthorization), nxmResolver.PremiumResolutionDoesNotForwardShortLivedAuthorization)
			,(nameof(nxmResolver.FreeUserRequiresFreshMatchingAuthorization), nxmResolver.FreeUserRequiresFreshMatchingAuthorization)
			,(nameof(nxmResolver.ResolutionRejectsWrongFileAndUnsafeDownloadUri), nxmResolver.ResolutionRejectsWrongFileAndUnsafeDownloadUri)
			,(nameof(nxmResolver.ThirdPartyFailureDoesNotExposeSignedUrl), nxmResolver.ThirdPartyFailureDoesNotExposeSignedUrl)
			,(nameof(nxmImporter.ValidationFailureChangesNoInstalledFiles), nxmImporter.ValidationFailureChangesNoInstalledFiles)
			,(nameof(nxmImporter.DuplicateFlattenedPakNamesAreRejected), nxmImporter.DuplicateFlattenedPakNamesAreRejected)
			,(nameof(nxmImporter.CommitFailureRestoresEveryDestination), nxmImporter.CommitFailureRestoresEveryDestination)
			,(nameof(nxmImporter.SuccessfulCommitReplacesAllFilesAndKeepsRecoveryCopies), nxmImporter.SuccessfulCommitReplacesAllFilesAndKeepsRecoveryCopies)
			,(nameof(nxmImporter.DisposingUnfinishedCommitRollsBackFiles), nxmImporter.DisposingUnfinishedCommitRollsBackFiles)
			,(nameof(nxmImporter.ForceLoadedIdentityUsesFinalInstalledPath), nxmImporter.ForceLoadedIdentityUsesFinalInstalledPath)
			,(nameof(nxmImporter.PreflightReceivesBoundedStagedPakWithSupportedExtension), nxmImporter.PreflightReceivesBoundedStagedPakWithSupportedExtension)
			,(nameof(nxmImporter.DuplicateModUuidsAreRejectedBeforeCommit), nxmImporter.DuplicateModUuidsAreRejectedBeforeCommit)
			,(nameof(nxmImporter.RollbackFailureIdentifiesAffectedFileAndRecoveryDirectory), nxmImporter.RollbackFailureIdentifiesAffectedFileAndRecoveryDirectory)
			,(nameof(nxmStore.RoundTripPreservesPublicQueueStateWithoutCapabilities), nxmStore.RoundTripPreservesPublicQueueStateWithoutCapabilities)
			,(nameof(nxmStore.ReconcileRequiresFreshLinkForIncompleteFreeDownload), nxmStore.ReconcileRequiresFreshLinkForIncompleteFreeDownload)
			,(nameof(nxmStore.ReconcileMarksMissingCompletedFileAsFailed), nxmStore.ReconcileMarksMissingCompletedFileAsFailed)
			,(nameof(nxmStore.ReconcileRejectsCompletedFileWithWrongLength), nxmStore.ReconcileRejectsCompletedFileWithWrongLength)
			,(nameof(nxmStore.ReconcileRecoversInterruptedResolvingAndInstallingStates), nxmStore.ReconcileRecoversInterruptedResolvingAndInstallingStates)
			,(nameof(nxmStore.ReconcileValidatesRetainedArchiveForInstallFailure), nxmStore.ReconcileValidatesRetainedArchiveForInstallFailure)
			,(nameof(nxmStore.ReconcileRecoversLegacyRetryWhenCompletedArchiveIsIntact), nxmStore.ReconcileRecoversLegacyRetryWhenCompletedArchiveIsIntact)
			,(nameof(nxmStore.ReconcilePersistsLegacyRecoveryWhenPartialCleanupFails), nxmStore.ReconcilePersistsLegacyRecoveryWhenPartialCleanupFails)
			,(nameof(nxmStore.CorruptManifestIsQuarantinedWithoutBlockingStartup), nxmStore.CorruptManifestIsQuarantinedWithoutBlockingStartup)
			,(nameof(nxmStore.CorruptManifestRecoversLastKnownGoodBackup), nxmStore.CorruptManifestRecoversLastKnownGoodBackup)
			,(nameof(nxmScheduler.DefaultLimitRunsFourAndQueuesTheRest), nxmScheduler.DefaultLimitRunsFourAndQueuesTheRest)
			,(nameof(nxmScheduler.RaisingLimitDispatchesQueuedItemsInFifoOrder), nxmScheduler.RaisingLimitDispatchesQueuedItemsInFifoOrder)
			,(nameof(nxmScheduler.CancellationRemovesSuspendedQueuedWorkImmediately), nxmScheduler.CancellationRemovesSuspendedQueuedWorkImmediately)
			,(nameof(nxmTransfer.MatchingRangeResponseResumesPartialFile), nxmTransfer.MatchingRangeResponseResumesPartialFile)
			,(nameof(nxmTransfer.FullResponseRestartsInsteadOfAppendingPartialFile), nxmTransfer.FullResponseRestartsInsteadOfAppendingPartialFile)
			,(nameof(nxmTransfer.SidecarValidatorEnablesResumeAfterRestart), nxmTransfer.SidecarValidatorEnablesResumeAfterRestart)
			,(nameof(nxmTransfer.MismatchedResumeValidatorRestartsFromZero), nxmTransfer.MismatchedResumeValidatorRestartsFromZero)
			,(nameof(nxmTransfer.ShortResponseIsNotPublishedAsComplete), nxmTransfer.ShortResponseIsNotPublishedAsComplete)
			,(nameof(nxmTransfer.ApproximateMetadataSizeDoesNotRejectCompleteResponse), nxmTransfer.ApproximateMetadataSizeDoesNotRejectCompleteResponse)
			,(nameof(nxmTransfer.UnsolicitedPartialResponseIsNotPublished), nxmTransfer.UnsolicitedPartialResponseIsNotPublished)
			,(nameof(nxmTransfer.ResponseWithoutADeclaredLengthIsNotPublished), nxmTransfer.ResponseWithoutADeclaredLengthIsNotPublished)
			,(nameof(nxmTransfer.StalledResponseBodyTimesOutWithoutPublishing), nxmTransfer.StalledResponseBodyTimesOutWithoutPublishing)
			,(nameof(nxmManager.DuplicateLinkFocusesExistingItem), nxmManager.DuplicateLinkFocusesExistingItem)
			,(nameof(nxmManager.ResolvedItemsDownloadWithoutBlockingIngress), nxmManager.ResolvedItemsDownloadWithoutBlockingIngress)
			,(nameof(nxmManager.RemovingResolvingItemCancelsItBeforeTransfer), nxmManager.RemovingResolvingItemCancelsItBeforeTransfer)
			,(nameof(nxmManager.CancelWinsRaceWithTransferCompletion), nxmManager.CancelWinsRaceWithTransferCompletion)
			,(nameof(nxmManager.HttpProgressTotalReplacesMetadataEstimate), nxmManager.HttpProgressTotalReplacesMetadataEstimate)
			,(nameof(nxmManager.PauseWaitsForTheOwnedTransferAndRejectsItsLateCompletion), nxmManager.PauseWaitsForTheOwnedTransferAndRejectsItsLateCompletion)
			,(nameof(nxmManager.DisablingNetworkWaitsForTransfersAndReenableDoesNotResumeThem), nxmManager.DisablingNetworkWaitsForTransfersAndReenableDoesNotResumeThem)
			,(nameof(nxmManager.EnqueueSaveFailureDoesNotPublishTheItem), nxmManager.EnqueueSaveFailureDoesNotPublishTheItem)
			,(nameof(nxmManager.ReservedWindowsFilenameUsesAStableSafeName), nxmManager.ReservedWindowsFilenameUsesAStableSafeName)
			,(nameof(nxmManager.InitializeRestartsPersistedQueuedDownload), nxmManager.InitializeRestartsPersistedQueuedDownload)
			,(nameof(nxmManager.FailedPauseSaveDoesNotPublishPausedState), nxmManager.FailedPauseSaveDoesNotPublishPausedState)
			,(nameof(nxmManager.PermanentTransferFailureDoesNotEnterRetryLoop), nxmManager.PermanentTransferFailureDoesNotEnterRetryLoop)
			,(nameof(nxmManager.MetadataCompletionOrderDoesNotChangeQueueFifo), nxmManager.MetadataCompletionOrderDoesNotChangeQueueFifo)
			,(nameof(nxmManager.ShutdownRejectsLateEnqueue), nxmManager.ShutdownRejectsLateEnqueue)
			,(nameof(nxmManager.FreshLinkSaveFailureDoesNotPublishResolvingState), nxmManager.FreshLinkSaveFailureDoesNotPublishResolvingState)
			,(nameof(nxmManager.FailedShutdownCanBeRetriedUntilPausedStateIsDurable), nxmManager.FailedShutdownCanBeRetriedUntilPausedStateIsDurable)
			,(nameof(nxmManager.QueueStatesHaveHumanReadableLabels), nxmManager.QueueStatesHaveHumanReadableLabels)
			,(nameof(nxmManager.InstallFailuresHaveDedicatedHumanReadableState), nxmManager.InstallFailuresHaveDedicatedHumanReadableState),
			(nameof(commandPaletteSearch.AliasesAndWordOrderMakeActionsDiscoverable), commandPaletteSearch.AliasesAndWordOrderMakeActionsDiscoverable),
			(nameof(commandPaletteSearch.MinimumQueryLengthStillProtectsLargeDynamicLists), commandPaletteSearch.MinimumQueryLengthStillProtectsLargeDynamicLists),
			(nameof(fileSafety.FailedStagedWritePreservesTheExistingDestination), fileSafety.FailedStagedWritePreservesTheExistingDestination),
			(nameof(fileSafety.AtomicCopyReplacesTheDestinationAndKeepsItsBackup), fileSafety.AtomicCopyReplacesTheDestinationAndKeepsItsBackup),
			(nameof(fileSafety.AsyncCopyReplacesTheDestinationAndKeepsItsBackup), fileSafety.AsyncCopyReplacesTheDestinationAndKeepsItsBackup),
			(nameof(fileSafety.ConcurrentWritesNeverExposePartialContent), fileSafety.ConcurrentWritesNeverExposePartialContent),
			(nameof(fileSafety.CancelledAsyncCopyPreservesTheExistingDestination), fileSafety.CancelledAsyncCopyPreservesTheExistingDestination),
			(nameof(fileSafety.ProviderCredentialsAreEncryptedAndExcludedFromSettingsJson), fileSafety.ProviderCredentialsAreEncryptedAndExcludedFromSettingsJson),
			(nameof(loadOrderWorkflow.SaveSwitchRenameAndRestartPreservesEachOrder), loadOrderWorkflow.SaveSwitchRenameAndRestartPreservesEachOrder),
			(nameof(loadOrderWorkflow.RenameRequiresConfirmationBeforeReplacingAnotherSavedOrder), loadOrderWorkflow.RenameRequiresConfirmationBeforeReplacingAnotherSavedOrder)
		};

		var failures = 0;
		foreach (var test in tests)
		{
			try
			{
				test.Run();
				Console.WriteLine($"PASS {test.Name}");
			}
			catch (Exception ex)
			{
				failures++;
				Console.Error.WriteLine($"FAIL {test.Name}: {ex.Message}");
			}
		}

		Console.WriteLine($"{tests.Length - failures}/{tests.Length} Redux regression checks passed.");
		return failures == 0 ? 0 : 1;
	}
}

internal static class RegressionAssert
{
	public static void Equal<T>(T expected, T actual)
	{
		if (!EqualityComparer<T>.Default.Equals(expected, actual))
			throw new InvalidOperationException($"Expected '{expected}', received '{actual}'.");
	}

	public static void True(bool value)
	{
		if (!value) throw new InvalidOperationException("Expected true, received false.");
	}

	public static void False(bool value)
	{
		if (value) throw new InvalidOperationException("Expected false, received true.");
	}

	public static void Contains(string value, string expectedSubstring)
	{
		if (value?.Contains(expectedSubstring, StringComparison.OrdinalIgnoreCase) != true)
			throw new InvalidOperationException($"Expected '{value}' to contain '{expectedSubstring}'.");
	}

	public static void Throws<TException>(Action action) where TException : Exception
	{
		try
		{
			action();
		}
		catch (TException)
		{
			return;
		}
		throw new InvalidOperationException($"Expected {typeof(TException).Name}.");
	}

	public static void SequenceEqual<T>(IEnumerable<T> expected, IEnumerable<T> actual)
	{
		if (!expected.SequenceEqual(actual))
			throw new InvalidOperationException("Sequences are not equal.");
	}
}

internal sealed class RegressionModData : DivinityModData
{
	public override string GetDisplayName() => Name ?? String.Empty;
}
