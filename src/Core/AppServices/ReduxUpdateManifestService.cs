using DivinityModManager.Models.Updates;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

using System.Security.Cryptography;
using System.Text.RegularExpressions;

namespace DivinityModManager.AppServices;

/// <summary>
/// Validates the public update-channel contract before any release asset is downloaded or launched.
/// This service deliberately does not install updates and does not enable the public channel.
/// </summary>
public static partial class ReduxUpdateManifestService
{
	public const int CurrentSchemaVersion = 1;
	public const long MaximumManifestBytes = 256 * 1024;
	public const long MaximumArtifactBytes = 1024L * 1024 * 1024;
	private const int MaximumJsonDepth = 16;
	private const int LastLegacyPublicAlpha = 16;
	private const int LastFlatHotfix = 3;
	private const int MaintenanceScale = 100;
	private const string OfficialRepositoryPath = "/circleainn/BG3ModManager-Redux/";

	private static readonly HashSet<string> RootProperties = new(StringComparer.Ordinal)
	{
		"schemaVersion", "channel", "displayVersion", "internalVersion", "publishedAtUtc",
		"releaseNotesUrl", "artifacts"
	};
	private static readonly HashSet<string> ArtifactProperties = new(StringComparer.Ordinal)
	{
		"kind", "url", "sizeBytes", "sha256"
	};

	[GeneratedRegex(@"^0\.1\.0-alpha\.(?<release>[1-9][0-9]*)(?:\.(?<hotfix>[1-9][0-9]*)(?:\.(?<maintenance>[1-9][0-9]*))?)?$", RegexOptions.CultureInvariant)]
	private static partial Regex DisplayVersionPattern();

	[GeneratedRegex(@"^[a-fA-F0-9]{64}$", RegexOptions.CultureInvariant)]
	private static partial Regex Sha256Pattern();

	public static ReduxUpdateManifest ParseAndValidate(string json, string expectedChannel = ReduxUpdateChannels.PublicAlpha)
	{
		if (String.IsNullOrWhiteSpace(json))
			throw new InvalidDataException("The update manifest is empty.");
		if (System.Text.Encoding.UTF8.GetByteCount(json) > MaximumManifestBytes)
			throw new InvalidDataException("The update manifest is larger than the supported limit.");

		using var stringReader = new StringReader(json);
		using var jsonReader = new JsonTextReader(stringReader)
		{
			DateParseHandling = DateParseHandling.None,
			FloatParseHandling = FloatParseHandling.Decimal,
			MaxDepth = MaximumJsonDepth
		};
		var token = JToken.ReadFrom(jsonReader, new JsonLoadSettings
		{
			DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error,
			LineInfoHandling = LineInfoHandling.Ignore
		});
		if (jsonReader.Read())
			throw new InvalidDataException("The update manifest contains trailing JSON content.");

		var root = RequireObject(token, "manifest");
		RejectUnknownProperties(root, RootProperties, "manifest");
		if (RequireInteger(root, "schemaVersion") != CurrentSchemaVersion)
			throw new InvalidDataException($"Only update manifest schemaVersion {CurrentSchemaVersion} is supported.");

		var channel = RequireString(root, "channel", 64);
		if (!String.Equals(channel, expectedChannel, StringComparison.Ordinal))
			throw new InvalidDataException($"The update manifest channel must be '{expectedChannel}'.");

		var displayVersion = RequireString(root, "displayVersion", 64);
		var versionMatch = DisplayVersionPattern().Match(displayVersion);
		if (!versionMatch.Success
			|| !Int32.TryParse(versionMatch.Groups["release"].Value, out var releaseNumber)
			|| releaseNumber > UInt16.MaxValue)
			throw new InvalidDataException("The update display version is not a supported public-alpha version.");
		var hotfixNumber = 0;
		if (versionMatch.Groups["hotfix"].Success
			&& (!Int32.TryParse(versionMatch.Groups["hotfix"].Value, out hotfixNumber)
				|| hotfixNumber > UInt16.MaxValue))
		{
			throw new InvalidDataException("The update display version is not a supported public-alpha version.");
		}
		var maintenanceNumber = 0;
		if (versionMatch.Groups["maintenance"].Success
			&& (!Int32.TryParse(versionMatch.Groups["maintenance"].Value, out maintenanceNumber)
				|| maintenanceNumber >= MaintenanceScale))
		{
			throw new InvalidDataException("The update display version is not a supported public-alpha version.");
		}
		var encodedRevision = EncodeRevision(hotfixNumber, maintenanceNumber);

		var internalVersionText = RequireString(root, "internalVersion", 64);
		if (!Version.TryParse(internalVersionText, out var internalVersion)
			|| !MatchesDisplayVersion(internalVersion, releaseNumber, hotfixNumber, maintenanceNumber, encodedRevision))
		{
			throw new InvalidDataException("The display and internal update versions do not identify the same release.");
		}

		var publishedText = RequireString(root, "publishedAtUtc", 64);
		if (!DateTimeOffset.TryParse(publishedText, System.Globalization.CultureInfo.InvariantCulture,
			System.Globalization.DateTimeStyles.AssumeUniversal | System.Globalization.DateTimeStyles.AdjustToUniversal,
			out var publishedAtUtc) || !publishedText.EndsWith('Z'))
		{
			throw new InvalidDataException("publishedAtUtc must be an ISO 8601 UTC timestamp.");
		}

		var releaseNotesUrl = RequireOfficialReleaseUrl(
			root,
			"releaseNotesUrl",
			OfficialRepositoryPath + "releases/tag/v" + displayVersion,
			requireFileName: false);
		var artifactsToken = root["artifacts"] as JArray
			?? throw new InvalidDataException("artifacts must be an array.");
		if (artifactsToken.Count != 1)
			throw new InvalidDataException("The update manifest must contain exactly one portable release artifact.");

		var artifacts = new List<ReduxUpdateArtifact>(artifactsToken.Count);
		var kinds = new HashSet<string>(StringComparer.Ordinal);
		foreach (var artifactToken in artifactsToken)
		{
			var artifactObject = RequireObject(artifactToken, "artifact");
			RejectUnknownProperties(artifactObject, ArtifactProperties, "artifact");
			var kind = RequireString(artifactObject, "kind", 32);
			if (kind != ReduxUpdateArtifactKinds.Portable)
				throw new InvalidDataException("Artifact kind must be 'portable'.");
			if (!kinds.Add(kind))
				throw new InvalidDataException($"The update manifest contains more than one '{kind}' artifact.");

			var url = RequireOfficialReleaseUrl(
				artifactObject,
				"url",
				OfficialRepositoryPath + "releases/download/v" + displayVersion + "/",
				requireFileName: true);
			if (!url.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
				throw new InvalidDataException("The portable artifact must use the .zip file type.");
			var sizeBytes = RequireInteger(artifactObject, "sizeBytes");
			if (sizeBytes <= 0 || sizeBytes > MaximumArtifactBytes)
				throw new InvalidDataException("Artifact sizeBytes is outside the supported range.");
			var sha256 = RequireString(artifactObject, "sha256", 64);
			if (!Sha256Pattern().IsMatch(sha256))
				throw new InvalidDataException("Artifact sha256 must contain exactly 64 hexadecimal characters.");

			artifacts.Add(new ReduxUpdateArtifact
			{
				Kind = kind,
				Url = url,
				SizeBytes = sizeBytes,
				Sha256 = sha256.ToLowerInvariant()
			});
		}
		if (!kinds.Contains(ReduxUpdateArtifactKinds.Portable))
			throw new InvalidDataException("The update manifest must contain the portable release artifact.");

		return new ReduxUpdateManifest
		{
			SchemaVersion = CurrentSchemaVersion,
			Channel = channel,
			DisplayVersion = displayVersion,
			InternalVersion = internalVersion.ToString(),
			PublishedAtUtc = publishedAtUtc,
			ReleaseNotesUrl = releaseNotesUrl,
			Artifacts = artifacts
		};
	}

	private static int EncodeRevision(int hotfixNumber, int maintenanceNumber)
	{
		var encoded = checked((hotfixNumber * MaintenanceScale) + maintenanceNumber);
		if (encoded > UInt16.MaxValue)
			throw new InvalidDataException("The update display version is not a supported public-alpha version.");
		return encoded;
	}

	private static bool MatchesDisplayVersion(
		Version internalVersion,
		int releaseNumber,
		int hotfixNumber,
		int maintenanceNumber,
		int encodedRevision)
	{
		if (internalVersion.Major != 0 || internalVersion.Minor != 1) return false;

		// Alpha.15 and alpha.16 used 0.1.0.N before hotfix-aware versioning.
		var legacyBaseRelease = releaseNumber <= LastLegacyPublicAlpha
			&& hotfixNumber == 0
			&& internalVersion.Build == 0
			&& internalVersion.Revision == releaseNumber;
		var legacyFlatHotfix = releaseNumber == LastLegacyPublicAlpha
			&& maintenanceNumber == 0
			&& hotfixNumber is > 0 and <= LastFlatHotfix
			&& internalVersion.Build == releaseNumber
			&& internalVersion.Revision == hotfixNumber;
		var maintenanceAwareRelease = (hotfixNumber > 0 || releaseNumber > LastLegacyPublicAlpha)
			&& internalVersion.Build == releaseNumber
			&& internalVersion.Revision == encodedRevision;
		return legacyBaseRelease || legacyFlatHotfix || maintenanceAwareRelease;
	}

	public static ReduxUpdateDecision Evaluate(
		ReduxUpdateManifest manifest,
		string installedInternalVersion)
	{
		ArgumentNullException.ThrowIfNull(manifest);
		if (!Version.TryParse(installedInternalVersion, out var installed))
			throw new ArgumentException("The installed Redux version is invalid.", nameof(installedInternalVersion));
		if (!Version.TryParse(manifest.InternalVersion, out var available))
			throw new InvalidDataException("The validated update version is invalid.");

		var comparison = available.CompareTo(installed);
		var availability = comparison > 0
			? ReduxUpdateAvailability.UpdateAvailable
			: comparison < 0
				? ReduxUpdateAvailability.InstalledVersionIsNewer
				: ReduxUpdateAvailability.UpToDate;
		var artifact = manifest.Artifacts.SingleOrDefault()
			?? throw new InvalidDataException("The update manifest does not contain its portable artifact.");

		return new ReduxUpdateDecision
		{
			Availability = availability,
			Manifest = manifest,
			Artifact = artifact
		};
	}

	public static async Task VerifyArtifactAsync(
		string filePath,
		ReduxUpdateArtifact artifact,
		CancellationToken cancellationToken = default)
	{
		if (String.IsNullOrWhiteSpace(filePath)) throw new ArgumentException("An artifact path is required.", nameof(filePath));
		ArgumentNullException.ThrowIfNull(artifact);
		var info = new FileInfo(filePath);
		if (!info.Exists) throw new FileNotFoundException("The downloaded update artifact was not found.", info.FullName);
		if (info.Length != artifact.SizeBytes)
			throw new InvalidDataException("The downloaded update artifact has an unexpected size.");

		await using var stream = new FileStream(info.FullName, FileMode.Open, FileAccess.Read, FileShare.Read,
			65536, FileOptions.Asynchronous | FileOptions.SequentialScan);
		var actualHash = Convert.ToHexString(await SHA256.HashDataAsync(stream, cancellationToken)).ToLowerInvariant();
		if (!String.Equals(actualHash, artifact.Sha256, StringComparison.OrdinalIgnoreCase))
			throw new InvalidDataException("The downloaded update artifact failed SHA-256 verification.");
	}

	private static string RequireOfficialReleaseUrl(
		JObject parent,
		string propertyName,
		string requiredPath,
		bool requireFileName)
	{
		var text = RequireString(parent, propertyName, 2048);
		var validPath = false;
		if (Uri.TryCreate(text, UriKind.Absolute, out var candidate))
		{
			validPath = requireFileName
				? candidate.AbsolutePath.StartsWith(requiredPath, StringComparison.OrdinalIgnoreCase)
					&& candidate.AbsolutePath.Length > requiredPath.Length
					&& candidate.AbsolutePath.IndexOf('/', requiredPath.Length) < 0
				: String.Equals(candidate.AbsolutePath, requiredPath, StringComparison.OrdinalIgnoreCase);
		}
		if (!Uri.TryCreate(text, UriKind.Absolute, out var uri)
			|| !String.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)
			|| !String.Equals(uri.Host, "github.com", StringComparison.OrdinalIgnoreCase)
			|| uri.IsDefaultPort == false
			|| !String.IsNullOrEmpty(uri.UserInfo)
			|| !String.IsNullOrEmpty(uri.Query)
			|| !String.IsNullOrEmpty(uri.Fragment)
			|| !validPath)
		{
			throw new InvalidDataException($"{propertyName} must be an official HTTPS Redux release URL.");
		}
		return uri.AbsoluteUri;
	}

	private static JObject RequireObject(JToken token, string name) =>
		token as JObject ?? throw new InvalidDataException($"{name} must be an object.");

	private static void RejectUnknownProperties(JObject value, ISet<string> allowed, string name)
	{
		var unknown = value.Properties().FirstOrDefault(property => !allowed.Contains(property.Name));
		if (unknown != null) throw new InvalidDataException($"{name} contains unsupported property '{unknown.Name}'.");
	}

	private static string RequireString(JObject parent, string propertyName, int maximumLength)
	{
		if (parent[propertyName]?.Type != JTokenType.String)
			throw new InvalidDataException($"{propertyName} must be a string.");
		var value = parent[propertyName]!.Value<string>()?.Trim() ?? String.Empty;
		if (value.Length == 0 || value.Length > maximumLength)
			throw new InvalidDataException($"{propertyName} must contain between 1 and {maximumLength} characters.");
		return value;
	}

	private static long RequireInteger(JObject parent, string propertyName)
	{
		if (parent[propertyName]?.Type != JTokenType.Integer)
			throw new InvalidDataException($"{propertyName} must be an integer.");
		return parent[propertyName]!.Value<long>();
	}
}
