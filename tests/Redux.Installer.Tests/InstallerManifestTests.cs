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
