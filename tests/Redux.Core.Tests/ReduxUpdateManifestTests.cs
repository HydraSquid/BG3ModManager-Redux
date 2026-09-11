using DivinityModManager.AppServices;
using DivinityModManager.Models.Updates;

using Newtonsoft.Json.Linq;

using System;
using System.IO;
using System.Security.Cryptography;

namespace Redux.Core.Tests;

public sealed class ReduxUpdateManifestTests
{
	public void ValidPublicAlphaManifestSelectsPortableArtifact()
	{
		var manifest = ReduxUpdateManifestService.ParseAndValidate(CreateManifest().ToString());
		var decision = ReduxUpdateManifestService.Evaluate(manifest, "0.1.0.14");

		RegressionAssert.Equal(ReduxUpdateAvailability.UpdateAvailable, decision.Availability);
		RegressionAssert.Equal(ReduxUpdateArtifactKinds.Portable, decision.Artifact.Kind);
	}

	public void SameAndNewerInstalledVersionsAreNeverOfferedAsUpdates()
	{
		var manifest = ReduxUpdateManifestService.ParseAndValidate(CreateManifest().ToString());

		RegressionAssert.Equal(
			ReduxUpdateAvailability.UpToDate,
			ReduxUpdateManifestService.Evaluate(manifest, "0.1.0.15").Availability);
		RegressionAssert.Equal(
			ReduxUpdateAvailability.InstalledVersionIsNewer,
			ReduxUpdateManifestService.Evaluate(manifest, "0.1.0.16").Availability);
	}

	public void HotfixVersionsUpdateTheirBaseAndOrderBeforeTheNextAlpha()
	{
		var hotfix = ReduxUpdateManifestService.ParseAndValidate(
			CreateManifest("0.1.0-alpha.16.1", "0.1.16.1").ToString());
		var nextAlpha = ReduxUpdateManifestService.ParseAndValidate(
			CreateManifest("0.1.0-alpha.17", "0.1.17.0").ToString());

		RegressionAssert.Equal(
			ReduxUpdateAvailability.UpdateAvailable,
			ReduxUpdateManifestService.Evaluate(hotfix, "0.1.0.16").Availability);
		RegressionAssert.Equal(
			ReduxUpdateAvailability.UpdateAvailable,
			ReduxUpdateManifestService.Evaluate(nextAlpha, hotfix.InternalVersion).Availability);
	}

	public void MaintenanceVersionsOrderBetweenTheirHotfixAndTheNextHotfix()
	{
		var maintenance = ReduxUpdateManifestService.ParseAndValidate(
			CreateManifest("0.1.0-alpha.16.3.1", "0.1.16.301").ToString());
		var nextHotfix = ReduxUpdateManifestService.ParseAndValidate(
			CreateManifest("0.1.0-alpha.16.4", "0.1.16.400").ToString());

		RegressionAssert.Equal(
			ReduxUpdateAvailability.UpdateAvailable,
			ReduxUpdateManifestService.Evaluate(maintenance, "0.1.16.3").Availability);
		RegressionAssert.Equal(
			ReduxUpdateAvailability.UpdateAvailable,
			ReduxUpdateManifestService.Evaluate(nextHotfix, maintenance.InternalVersion).Availability);
	}

	public void HotfixVersionsRejectZeroOverflowAndMismatchedInternalVersions()
	{
		RegressionAssert.Throws<InvalidDataException>(() => ReduxUpdateManifestService.ParseAndValidate(
			CreateManifest("0.1.0-alpha.16.0", "0.1.16.0").ToString()));
		RegressionAssert.Throws<InvalidDataException>(() => ReduxUpdateManifestService.ParseAndValidate(
			CreateManifest("0.1.0-alpha.16.65536", "0.1.16.65536").ToString()));
		RegressionAssert.Throws<InvalidDataException>(() => ReduxUpdateManifestService.ParseAndValidate(
			CreateManifest("0.1.0-alpha.16.3.0", "0.1.16.300").ToString()));
		RegressionAssert.Throws<InvalidDataException>(() => ReduxUpdateManifestService.ParseAndValidate(
			CreateManifest("0.1.0-alpha.16.3.100", "0.1.16.400").ToString()));
		RegressionAssert.Throws<InvalidDataException>(() => ReduxUpdateManifestService.ParseAndValidate(
			CreateManifest("0.1.0-alpha.16.3.1", "0.1.16.3").ToString()));
		RegressionAssert.Throws<InvalidDataException>(() => ReduxUpdateManifestService.ParseAndValidate(
			CreateManifest("0.1.0-alpha.16.1", "0.1.0.16").ToString()));
		RegressionAssert.Throws<InvalidDataException>(() => ReduxUpdateManifestService.ParseAndValidate(
			CreateManifest("0.1.0-alpha.16", "0.1.16.0").ToString()));
		RegressionAssert.Throws<InvalidDataException>(() => ReduxUpdateManifestService.ParseAndValidate(
			CreateManifest("0.1.0-alpha.17", "0.1.0.17").ToString()));
	}

	public void ManifestRequiresExactlyOnePortableArtifact()
	{
		var missing = CreateManifest();
		((JArray)missing["artifacts"]!).Clear();
		var extra = CreateManifest();
		((JArray)extra["artifacts"]!).Add(((JArray)extra["artifacts"]!)[0]!.DeepClone());
		var installer = CreateManifest();
		installer["artifacts"]![0]!["kind"] = "installer";

		RegressionAssert.Throws<InvalidDataException>(() =>
			ReduxUpdateManifestService.ParseAndValidate(missing.ToString()));
		RegressionAssert.Throws<InvalidDataException>(() =>
			ReduxUpdateManifestService.ParseAndValidate(extra.ToString()));
		RegressionAssert.Throws<InvalidDataException>(() =>
			ReduxUpdateManifestService.ParseAndValidate(installer.ToString()));
	}

	public void ManifestRejectsDuplicateAndUnknownProperties()
	{
		var duplicate = CreateManifest().ToString().Replace(
			"\"schemaVersion\": 1,",
			"\"schemaVersion\": 1, \"schemaVersion\": 1,");
		var unknown = CreateManifest();
		unknown["redirect"] = "https://example.com";

		RegressionAssert.Throws<Newtonsoft.Json.JsonReaderException>(() =>
			ReduxUpdateManifestService.ParseAndValidate(duplicate));
		RegressionAssert.Throws<InvalidDataException>(() =>
			ReduxUpdateManifestService.ParseAndValidate(unknown.ToString()));
	}

	public void ManifestRejectsTrailingContentAndWrongChannel()
	{
		var wrongChannel = CreateManifest();
		wrongChannel["channel"] = "stable";

		RegressionAssert.Throws<Newtonsoft.Json.JsonReaderException>(() =>
			ReduxUpdateManifestService.ParseAndValidate(CreateManifest() + " true"));
		RegressionAssert.Throws<InvalidDataException>(() =>
			ReduxUpdateManifestService.ParseAndValidate(wrongChannel.ToString()));
	}

	public void ManifestRejectsMismatchedDisplayAndInternalVersions()
	{
		var json = CreateManifest();
		json["internalVersion"] = "0.1.0.16";

		RegressionAssert.Throws<InvalidDataException>(() =>
			ReduxUpdateManifestService.ParseAndValidate(json.ToString()));
	}

	public void ManifestRejectsUntrustedArtifactAndReleaseNotesUrls()
	{
		var artifact = CreateManifest();
		artifact["artifacts"]![0]!["url"] = "https://example.com/ReduxSetup.exe";
		var notes = CreateManifest();
		notes["releaseNotesUrl"] = "http://github.com/circleainn/BG3ModManager-Redux/releases/tag/v0.1.0-alpha.15";
		var staleArtifact = CreateManifest();
		staleArtifact["artifacts"]![0]!["url"] =
			"https://github.com/circleainn/BG3ModManager-Redux/releases/download/v0.1.0-alpha.14/ReduxSetup.exe";

		RegressionAssert.Throws<InvalidDataException>(() =>
			ReduxUpdateManifestService.ParseAndValidate(artifact.ToString()));
		RegressionAssert.Throws<InvalidDataException>(() =>
			ReduxUpdateManifestService.ParseAndValidate(notes.ToString()));
		RegressionAssert.Throws<InvalidDataException>(() =>
			ReduxUpdateManifestService.ParseAndValidate(staleArtifact.ToString()));
	}

	public void ArtifactVerificationRequiresMatchingLengthAndSha256()
	{
		var directory = Path.Combine(Path.GetTempPath(), "redux-update-tests-" + Guid.NewGuid().ToString("N"));
		Directory.CreateDirectory(directory);
		try
		{
			var path = Path.Combine(directory, "artifact.bin");
			var bytes = new byte[] { 1, 2, 3, 4, 5 };
			File.WriteAllBytes(path, bytes);
			var artifact = new ReduxUpdateArtifact
			{
				Kind = ReduxUpdateArtifactKinds.Portable,
				Url = "https://github.com/circleainn/BG3ModManager-Redux/releases/download/v0.1.0-alpha.15/redux.zip",
				SizeBytes = bytes.Length,
				Sha256 = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant()
			};

			ReduxUpdateManifestService.VerifyArtifactAsync(path, artifact).GetAwaiter().GetResult();
			File.WriteAllBytes(path, new byte[] { 1, 2, 3, 4, 6 });

			RegressionAssert.Throws<InvalidDataException>(() =>
				ReduxUpdateManifestService.VerifyArtifactAsync(path, artifact).GetAwaiter().GetResult());
		}
		finally
		{
			Directory.Delete(directory, recursive: true);
		}
	}

	private static JObject CreateManifest(
		string displayVersion = "0.1.0-alpha.15",
		string internalVersion = "0.1.0.15") => new()
	{
		["schemaVersion"] = 1,
		["channel"] = ReduxUpdateChannels.PublicAlpha,
		["displayVersion"] = displayVersion,
		["internalVersion"] = internalVersion,
		["publishedAtUtc"] = "2026-09-08T16:30:00Z",
		["releaseNotesUrl"] = $"https://github.com/circleainn/BG3ModManager-Redux/releases/tag/v{displayVersion}",
		["artifacts"] = new JArray
		{
			new JObject
			{
				["kind"] = ReduxUpdateArtifactKinds.Portable,
				["url"] = $"https://github.com/circleainn/BG3ModManager-Redux/releases/download/v{displayVersion}/BG3ModManager-Redux_v{displayVersion}.zip",
				["sizeBytes"] = 8192,
				["sha256"] = new string('b', 64)
			}
		}
	};
}
