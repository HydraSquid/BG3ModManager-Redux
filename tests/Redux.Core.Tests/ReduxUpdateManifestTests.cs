using DivinityModManager.AppServices;
using DivinityModManager.Models.Updates;

using Newtonsoft.Json.Linq;

using System;
using System.IO;
using System.Security.Cryptography;

namespace Redux.Core.Tests;

public sealed class ReduxUpdateManifestTests
{
	public void ValidPublicAlphaManifestSelectsArtifactForDeploymentType()
	{
		var manifest = ReduxUpdateManifestService.ParseAndValidate(CreateManifest().ToString());

		var installed = ReduxUpdateManifestService.Evaluate(manifest, "0.1.0.14", installerManaged: true);
		var portable = ReduxUpdateManifestService.Evaluate(manifest, "0.1.0.14", installerManaged: false);

		RegressionAssert.Equal(ReduxUpdateAvailability.UpdateAvailable, installed.Availability);
		RegressionAssert.Equal(ReduxUpdateArtifactKinds.Installer, installed.PreferredArtifact.Kind);
		RegressionAssert.Equal(ReduxUpdateArtifactKinds.Portable, portable.PreferredArtifact.Kind);
	}

	public void SameAndNewerInstalledVersionsAreNeverOfferedAsUpdates()
	{
		var manifest = ReduxUpdateManifestService.ParseAndValidate(CreateManifest().ToString());

		RegressionAssert.Equal(
			ReduxUpdateAvailability.UpToDate,
			ReduxUpdateManifestService.Evaluate(manifest, "0.1.0.15", installerManaged: true).Availability);
		RegressionAssert.Equal(
			ReduxUpdateAvailability.InstalledVersionIsNewer,
			ReduxUpdateManifestService.Evaluate(manifest, "0.1.0.16", installerManaged: true).Availability);
	}

	public void PortableReleaseCanBootstrapBeforeInstallerArtifactExists()
	{
		var json = CreateManifest();
		((JArray)json["artifacts"]!).RemoveAt(0);

		var manifest = ReduxUpdateManifestService.ParseAndValidate(json.ToString());
		var decision = ReduxUpdateManifestService.Evaluate(manifest, "0.1.0.14", installerManaged: false);

		RegressionAssert.Equal(ReduxUpdateArtifactKinds.Portable, decision.PreferredArtifact.Kind);
		RegressionAssert.Throws<InvalidDataException>(() =>
			ReduxUpdateManifestService.Evaluate(manifest, "0.1.0.14", installerManaged: true));
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

	private static JObject CreateManifest() => new()
	{
		["schemaVersion"] = 1,
		["channel"] = ReduxUpdateChannels.PublicAlpha,
		["displayVersion"] = "0.1.0-alpha.15",
		["internalVersion"] = "0.1.0.15",
		["publishedAtUtc"] = "2026-09-08T16:30:00Z",
		["releaseNotesUrl"] = "https://github.com/circleainn/BG3ModManager-Redux/releases/tag/v0.1.0-alpha.15",
		["artifacts"] = new JArray
		{
			new JObject
			{
				["kind"] = ReduxUpdateArtifactKinds.Installer,
				["url"] = "https://github.com/circleainn/BG3ModManager-Redux/releases/download/v0.1.0-alpha.15/ReduxSetup.exe",
				["sizeBytes"] = 4096,
				["sha256"] = new string('a', 64)
			},
			new JObject
			{
				["kind"] = ReduxUpdateArtifactKinds.Portable,
				["url"] = "https://github.com/circleainn/BG3ModManager-Redux/releases/download/v0.1.0-alpha.15/BG3ModManager-Redux_v0.1.0-alpha.15.zip",
				["sizeBytes"] = 8192,
				["sha256"] = new string('b', 64)
			}
		}
	};
}
