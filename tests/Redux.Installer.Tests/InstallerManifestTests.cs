using ReduxInstaller.Services;

using System.IO;

namespace Redux.Installer.Tests;

internal sealed class InstallerManifestTests
{
	public void ExactPublicAlphaManifestIsAccepted()
	{
		var manifest = InstallerManifestService.ParseAndValidate(ValidManifest());
		RegressionAssert.Equal("0.1.0-alpha.15", manifest.DisplayVersion);
		RegressionAssert.Equal("0.1.0.15", manifest.InternalVersion);
		RegressionAssert.Equal("portable", manifest.Artifact.Kind);
		RegressionAssert.Equal(8192L, manifest.Artifact.SizeBytes);
	}

	public void HotfixAwarePublicAlphaManifestIsAccepted()
	{
		var manifest = InstallerManifestService.ParseAndValidate(
			ValidManifest()
				.Replace("0.1.0-alpha.15", "0.1.0-alpha.16.1")
				.Replace("0.1.0.15", "0.1.16.1"));

		RegressionAssert.Equal("0.1.0-alpha.16.1", manifest.DisplayVersion);
		RegressionAssert.Equal("0.1.16.1", manifest.InternalVersion);
		RegressionAssert.True(InstallerManifestService.CompareDisplayVersions(
			"0.1.0-alpha.16", "0.1.0-alpha.16.1") < 0);
		RegressionAssert.True(InstallerManifestService.CompareDisplayVersions(
			"0.1.0-alpha.16.2", "0.1.0-alpha.17") < 0);
		RegressionAssert.True(InstallerManifestService.CompareDisplayVersions(
			"0.1.0-alpha.16.3", "0.1.0-alpha.16.3.1") < 0);
		RegressionAssert.True(InstallerManifestService.CompareDisplayVersions(
			"0.1.0-alpha.16.3.1", "0.1.0-alpha.16.4") < 0);
		var maintenance = InstallerManifestService.ParseAndValidate(
			ValidManifest()
				.Replace("0.1.0-alpha.15", "0.1.0-alpha.16.3.1")
				.Replace("0.1.0.15", "0.1.16.301"));
		RegressionAssert.Equal("0.1.16.301", maintenance.InternalVersion);
		RegressionAssert.Throws<InvalidDataException>(() => InstallerManifestService.ParseAndValidate(
			ValidManifest()
				.Replace("0.1.0-alpha.15", "0.1.0-alpha.16.3.1")
				.Replace("0.1.0.15", "0.1.16.3")));
		RegressionAssert.Throws<InvalidDataException>(() => InstallerManifestService.ParseAndValidate(
			ValidManifest()
				.Replace("0.1.0-alpha.15", "0.1.0-alpha.16")
				.Replace("0.1.0.15", "0.1.16.0")));
		RegressionAssert.Throws<InvalidDataException>(() => InstallerManifestService.ParseAndValidate(
			ValidManifest()
				.Replace("0.1.0-alpha.15", "0.1.0-alpha.17")
				.Replace("0.1.0.15", "0.1.0.17")));
	}

	public void DuplicateAndUnknownManifestPropertiesAreRejected()
	{
		RegressionAssert.Throws<InvalidDataException>(() => InstallerManifestService.ParseAndValidate(
			ValidManifest().Replace("\"schemaVersion\": 1,", "\"schemaVersion\": 1, \"schemaVersion\": 1,")));
		RegressionAssert.Throws<InvalidDataException>(() => InstallerManifestService.ParseAndValidate(
			ValidManifest().Replace("\"channel\":", "\"unexpected\": true, \"channel\":")));
	}

	public void ManifestCannotRedirectSetupOutsideOfficialVersionedRelease()
	{
		RegressionAssert.Throws<InvalidDataException>(() => InstallerManifestService.ParseAndValidate(
			ValidManifest().Replace("https://github.com/circleainn/BG3ModManager-Redux/releases/download/v0.1.0-alpha.15/",
				"https://example.com/")));
		RegressionAssert.Throws<InvalidDataException>(() => InstallerManifestService.ParseAndValidate(
			ValidManifest().Replace("v0.1.0-alpha.15/BG3ModManager", "v0.1.0-alpha.14/BG3ModManager")));
	}

	internal static string ValidManifest() => @"{
  ""schemaVersion"": 1,
  ""channel"": ""public-alpha"",
  ""displayVersion"": ""0.1.0-alpha.15"",
  ""internalVersion"": ""0.1.0.15"",
  ""publishedAtUtc"": ""2026-09-08T16:30:00Z"",
  ""releaseNotesUrl"": ""https://github.com/circleainn/BG3ModManager-Redux/releases/tag/v0.1.0-alpha.15"",
  ""artifacts"": [{
    ""kind"": ""portable"",
    ""url"": ""https://github.com/circleainn/BG3ModManager-Redux/releases/download/v0.1.0-alpha.15/BG3ModManager-Redux_v0.1.0-alpha.15.zip"",
    ""sizeBytes"": 8192,
    ""sha256"": ""bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb""
  }]
}";
}
