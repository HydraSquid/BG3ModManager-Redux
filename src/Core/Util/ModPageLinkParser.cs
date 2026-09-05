using System.Text.RegularExpressions;

namespace DivinityModManager.Util;

public sealed record ModPageLinkTarget(
	ModSourceType SourceType,
	long ProjectId,
	string ModioNameId,
	string PageUrl);

public static class ModPageLinkParser
{
	private const int MaximumLength = 2048;
	private static readonly Regex ModioNameIdPattern = new(
		@"^[a-z0-9](?:[a-z0-9-]*[a-z0-9])?$",
		RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

	public static bool TryParseBg3(string value, out ModPageLinkTarget link, out string error)
	{
		link = null;
		error = null;
		var input = value?.Trim();
		if (String.IsNullOrWhiteSpace(input) || input.Length > MaximumLength)
		{
			error = "Paste a Baldur's Gate 3 Nexus Mods or mod.io page URL.";
			return false;
		}

		if (Int64.TryParse(input, out var numericId) && numericId >= DivinityApp.NEXUSMODS_MOD_ID_START)
		{
			link = new ModPageLinkTarget(
				ModSourceType.NEXUSMODS,
				numericId,
				String.Empty,
				String.Format(DivinityApp.NEXUSMODS_MOD_URL, numericId));
			return true;
		}

		if (!Uri.TryCreate(input, UriKind.Absolute, out var uri)
			|| !uri.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)
			|| !uri.IsDefaultPort
			|| !String.IsNullOrWhiteSpace(uri.UserInfo))
		{
			error = "Only public HTTPS Nexus Mods and mod.io page URLs are supported.";
			return false;
		}

		var segments = uri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries)
			.Select(Uri.UnescapeDataString)
			.ToArray();
		if (IsHost(uri, "nexusmods.com")
			&& segments.Length == 3
			&& segments[0].Equals("baldursgate3", StringComparison.OrdinalIgnoreCase)
			&& segments[1].Equals("mods", StringComparison.OrdinalIgnoreCase)
			&& Int64.TryParse(segments[2], out var nexusId)
			&& nexusId >= DivinityApp.NEXUSMODS_MOD_ID_START)
		{
			link = new ModPageLinkTarget(
				ModSourceType.NEXUSMODS,
				nexusId,
				String.Empty,
				String.Format(DivinityApp.NEXUSMODS_MOD_URL, nexusId));
			return true;
		}

		if (IsHost(uri, "mod.io")
			&& segments.Length == 4
			&& segments[0].Equals("g", StringComparison.OrdinalIgnoreCase)
			&& segments[1].Equals("baldursgate3", StringComparison.OrdinalIgnoreCase)
			&& segments[2].Equals("m", StringComparison.OrdinalIgnoreCase)
			&& ModioNameIdPattern.IsMatch(segments[3]))
		{
			var nameId = segments[3].ToLowerInvariant();
			link = new ModPageLinkTarget(
				ModSourceType.MODIO,
				-1,
				nameId,
				$"https://mod.io/g/baldursgate3/m/{nameId}");
			return true;
		}

		error = "Paste a Baldur's Gate 3 Nexus Mods page or mod.io page, not a page for another provider or game.";
		return false;
	}

	private static bool IsHost(Uri uri, string host) =>
		uri.Host.Equals(host, StringComparison.OrdinalIgnoreCase)
		|| uri.Host.Equals($"www.{host}", StringComparison.OrdinalIgnoreCase);
}
