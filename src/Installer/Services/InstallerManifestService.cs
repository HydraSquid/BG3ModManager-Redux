using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

using ReduxInstaller.Models;

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace ReduxInstaller.Services;

internal static class InstallerManifestService
{
	public const int SchemaVersion = 1;
	public const long MaximumManifestBytes = 256 * 1024;
	public const long MaximumArtifactBytes = 1024L * 1024 * 1024;
	public const string Channel = "public-alpha";
	public const string ManifestUrl =
		"https://github.com/circleainn/BG3ModManager-Redux/releases/download/public-alpha/Redux-Update-Public-Alpha.json";

	private static readonly HashSet<string> RootProperties = new HashSet<string>(StringComparer.Ordinal)
	{
		"schemaVersion", "channel", "displayVersion", "internalVersion", "publishedAtUtc",
		"releaseNotesUrl", "artifacts"
	};
	private static readonly HashSet<string> ArtifactProperties = new HashSet<string>(StringComparer.Ordinal)
	{
		"kind", "url", "sizeBytes", "sha256"
	};
	private static readonly Regex DisplayVersionPattern = new Regex(
		@"^0\.1\.0-alpha\.(?<release>[1-9][0-9]*)$",
		RegexOptions.Compiled | RegexOptions.CultureInvariant);
	private static readonly Regex Sha256Pattern = new Regex(
		"^[a-fA-F0-9]{64}$",
		RegexOptions.Compiled | RegexOptions.CultureInvariant);

	public static InstallerReleaseManifest ParseAndValidate(string json)
	{
		if (String.IsNullOrWhiteSpace(json)) throw new InvalidDataException("The update manifest is empty.");
		if (Encoding.UTF8.GetByteCount(json) > MaximumManifestBytes)
			throw new InvalidDataException("The update manifest is larger than the supported limit.");

		JToken token;
		try
		{
			using (var text = new StringReader(json))
			using (var reader = new JsonTextReader(text)
			{
				DateParseHandling = DateParseHandling.None,
				FloatParseHandling = FloatParseHandling.Decimal,
				MaxDepth = 16
			})
			{
				token = JToken.ReadFrom(reader, new JsonLoadSettings
				{
					DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error,
					LineInfoHandling = LineInfoHandling.Ignore
				});
				if (reader.Read()) throw new InvalidDataException("The update manifest contains trailing content.");
			}
		}
		catch (JsonException ex)
		{
			throw new InvalidDataException("The update manifest is not valid JSON.", ex);
		}

		var root = RequireObject(token, "manifest");
		RejectUnknown(root, RootProperties, "manifest");
		if (RequireInteger(root, "schemaVersion") != SchemaVersion)
			throw new InvalidDataException("The update manifest schema is not supported.");
		if (!String.Equals(RequireString(root, "channel", 64), Channel, StringComparison.Ordinal))
			throw new InvalidDataException("The update manifest is not the Redux public-alpha channel.");

		var displayVersion = RequireString(root, "displayVersion", 64);
		var versionMatch = DisplayVersionPattern.Match(displayVersion);
		int release;
		if (!versionMatch.Success || !Int32.TryParse(versionMatch.Groups["release"].Value, out release))
			throw new InvalidDataException("The release version is not a supported Redux public-alpha version.");

		var internalVersionText = RequireString(root, "internalVersion", 64);
		Version internalVersion;
		if (!Version.TryParse(internalVersionText, out internalVersion)
			|| internalVersion.Major != 0 || internalVersion.Minor != 1 || internalVersion.Build != 0
			|| internalVersion.Revision != release)
			throw new InvalidDataException("The display and internal release versions do not agree.");

		var publishedText = RequireString(root, "publishedAtUtc", 64);
		DateTimeOffset publishedAt;
		if (!publishedText.EndsWith("Z", StringComparison.Ordinal)
			|| !DateTimeOffset.TryParse(publishedText, CultureInfo.InvariantCulture,
				DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out publishedAt))
			throw new InvalidDataException("The release timestamp must use ISO 8601 UTC.");

		var releaseNotes = RequireOfficialUrl(root, "releaseNotesUrl",
			"/circleainn/BG3ModManager-Redux/releases/tag/v" + displayVersion, false);
		var artifactTokens = root["artifacts"] as JArray;
		if (artifactTokens == null || artifactTokens.Count != 1)
			throw new InvalidDataException("The update manifest must contain exactly one portable artifact.");
		var artifactObject = RequireObject(artifactTokens[0], "artifact");
		RejectUnknown(artifactObject, ArtifactProperties, "artifact");
		if (!String.Equals(RequireString(artifactObject, "kind", 32), "portable", StringComparison.Ordinal))
			throw new InvalidDataException("The installer accepts only the portable Redux artifact.");
		var artifactUrl = RequireOfficialUrl(artifactObject, "url",
			"/circleainn/BG3ModManager-Redux/releases/download/v" + displayVersion + "/", true);
		if (!artifactUrl.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
			throw new InvalidDataException("The portable Redux artifact must be a ZIP archive.");
		var size = RequireInteger(artifactObject, "sizeBytes");
		if (size <= 0 || size > MaximumArtifactBytes)
			throw new InvalidDataException("The release download size is outside the supported limit.");
		var hash = RequireString(artifactObject, "sha256", 64);
		if (!Sha256Pattern.IsMatch(hash))
			throw new InvalidDataException("The release SHA-256 is invalid.");

		return new InstallerReleaseManifest
		{
			SchemaVersion = SchemaVersion,
			Channel = Channel,
			DisplayVersion = displayVersion,
			InternalVersion = internalVersion.ToString(),
			PublishedAtUtc = publishedAt,
			ReleaseNotesUrl = releaseNotes,
			Artifact = new InstallerReleaseArtifact
			{
				Kind = "portable",
				Url = artifactUrl,
				SizeBytes = size,
				Sha256 = hash.ToLowerInvariant()
			}
		};
	}

	private static JObject RequireObject(JToken token, string name)
	{
		var value = token as JObject;
		if (value == null) throw new InvalidDataException(name + " must be an object.");
		return value;
	}

	private static void RejectUnknown(JObject value, ISet<string> allowed, string name)
	{
		var unknown = value.Properties().FirstOrDefault(property => !allowed.Contains(property.Name));
		if (unknown != null) throw new InvalidDataException(name + " contains unsupported property '" + unknown.Name + "'.");
	}

	private static string RequireString(JObject parent, string name, int maximumLength)
	{
		var token = parent[name];
		if (token == null || token.Type != JTokenType.String)
			throw new InvalidDataException(name + " must be a string.");
		var value = (token.Value<string>() ?? String.Empty).Trim();
		if (value.Length == 0 || value.Length > maximumLength)
			throw new InvalidDataException(name + " has an invalid length.");
		return value;
	}

	private static long RequireInteger(JObject parent, string name)
	{
		var token = parent[name];
		if (token == null || token.Type != JTokenType.Integer)
			throw new InvalidDataException(name + " must be an integer.");
		return token.Value<long>();
	}

	private static string RequireOfficialUrl(JObject parent, string name, string requiredPath, bool requireFileName)
	{
		var text = RequireString(parent, name, 2048);
		Uri uri;
		if (!Uri.TryCreate(text, UriKind.Absolute, out uri)
			|| !String.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)
			|| !String.Equals(uri.Host, "github.com", StringComparison.OrdinalIgnoreCase)
			|| !uri.IsDefaultPort || !String.IsNullOrEmpty(uri.UserInfo)
			|| !String.IsNullOrEmpty(uri.Query) || !String.IsNullOrEmpty(uri.Fragment))
			throw new InvalidDataException(name + " must use the official Redux GitHub repository.");

		var validPath = requireFileName
			? uri.AbsolutePath.StartsWith(requiredPath, StringComparison.OrdinalIgnoreCase)
				&& uri.AbsolutePath.Length > requiredPath.Length
				&& uri.AbsolutePath.IndexOf('/', requiredPath.Length) < 0
			: String.Equals(uri.AbsolutePath, requiredPath, StringComparison.OrdinalIgnoreCase);
		if (!validPath) throw new InvalidDataException(name + " does not identify the declared Redux release.");
		return uri.AbsoluteUri;
	}
}
