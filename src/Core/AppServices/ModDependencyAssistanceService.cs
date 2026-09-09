using DivinityModManager.Models;
using DivinityModManager.Models.NexusMods;

namespace DivinityModManager.AppServices;

public sealed record DependencyDownloadMatch(NxmDownloadItem Download, bool Identified)
{
	public string Label => $"Show download: {Download.FileDisplayName} ({Download.StatusText})";
	public string Explanation => Identified
		? "This archive contained the required UUID when inspected. Review the file and version before installing."
		: "Same Nexus project only. This file may be a patch or a different variant; its contents are not identified as this dependency.";
}

public sealed record ModDependencyAssistance(
	string Uuid, string Name, string Status, DivinityModData InstalledMod,
	IReadOnlyList<DependencyDownloadMatch> Downloads, Uri NexusFilesUrl, string SourceHint)
{
	public bool CanCopyUuid => Guid.TryParse(Uuid, out var uuid) && uuid != Guid.Empty;
	public bool CanShowInstalled => InstalledMod != null;
	public bool CanOpenFiles => NexusFilesUrl != null;
}

public static class ModDependencyAssistanceService
{
	public static IReadOnlyList<ModDependencyAssistance> Build(
		IEnumerable<ModuleShortDesc> dependencies, IEnumerable<DivinityModData> installedMods,
		IEnumerable<NxmDownloadItem> downloads, string downloadsDirectory, bool sourceIntegrationsEnabled,
		IEnumerable<DivinityModData> bundledMods = null, Func<string, long?> resolveProject = null)
	{
		resolveProject ??= uuid => ReduxModDatabaseService.TryResolveModuleUuid(uuid)?.ModId;
		var installed = (installedMods ?? []).Where(mod => mod != null && !mod.IsVisualDivider).ToArray();
		var bundled = (bundledMods ?? []).Where(mod => mod != null).ToArray();
		var queue = (downloads ?? []).Where(item => item != null).ToArray();
		var inspected = queue.ToDictionary(item => item,
			item => HasCurrentInspection(item, downloadsDirectory) ? item.Inspection.Modules : []);
		var rows = new List<ModDependencyAssistance>();
		foreach (var group in (dependencies ?? []).Where(dependency => dependency != null)
			.GroupBy(dependency => NormalizeUuid(dependency.UUID), StringComparer.OrdinalIgnoreCase))
		{
			var dependency = group.OrderByDescending(entry => entry.Version?.VersionInt ?? 0).First();
			var uuid = group.Key;
			var validUuid = Guid.TryParse(uuid, out var parsed) && parsed != Guid.Empty;
			var found = validUuid ? installed.Where(mod => NormalizeUuid(mod.UUID) == uuid)
				.OrderByDescending(mod => mod.IsActive).FirstOrDefault() : null;
			var included = validUuid ? bundled.FirstOrDefault(mod => NormalizeUuid(mod.UUID) == uuid) : null;
			var projectId = validUuid ? resolveProject(uuid) : null;
			var matches = validUuid ? queue.Select(item => new DependencyDownloadMatch(item,
				inspected[item].Any(module => NormalizeUuid(module.UUID) == uuid)))
				.Where(match => match.Identified || (projectId > 0 && match.Download.ModId == projectId))
				.OrderByDescending(match => match.Identified).ToArray() : [];
			var status = !validUuid ? "Invalid dependency UUID; no source lookup was attempted."
				: found != null ? found.IsActive ? "Installed and active." : "Installed but inactive."
				: included != null ? "Included in this archive. Not installed yet."
				: matches.Any(match => match.Identified) ? "Not installed. Required UUID identified in a retained download; review its version."
				: matches.Length > 0 ? "Not installed. Possible same-project download; the dependency's file or variant is not confirmed."
				: "Not installed. No identified download; uninspected archives may still contain this dependency.";
			var available = found ?? included;
			if (available != null && dependency.Version?.VersionInt > 0
				&& (available.Version?.VersionInt ?? 0) < dependency.Version.VersionInt)
				status += $" Available version {available.Version} is older than declared requirement {dependency.Version}.";
			var url = projectId > 0 && sourceIntegrationsEnabled
				? new Uri($"https://www.nexusmods.com/baldursgate3/mods/{projectId}?tab=files") : null;
			var hint = projectId > 0
				? sourceIntegrationsEnabled ? "Reviewed UUID-to-Nexus link. Choose the appropriate file on Nexus." : "Online source links are disabled."
				: "No reviewed Nexus link for this UUID. No name-based match was guessed.";
			var name = String.IsNullOrWhiteSpace(dependency.Name) ? uuid : dependency.Name.Trim();
			if (name.Length > 160) name = name[..160] + "...";
			rows.Add(new(uuid, name, status, found, matches, url, hint));
		}
		return rows;
	}

	public static void RememberInspection(NxmDownloadItem item, string directory, IEnumerable<DivinityModData> modules)
	{
		if (item == null) return;
		item.Inspection = null;
		try
		{
			if (!IsLeafName(item.CompletedFileName)) return;
			var file = new FileInfo(Path.Combine(directory, item.CompletedFileName));
			if (!file.Exists) return;
			item.Inspection = new NxmArchiveInspection(item.CompletedFileName, file.Length, file.LastWriteTimeUtc,
				(modules ?? []).Where(mod => mod != null).Select(ModuleShortDesc.FromModData).ToArray());
		}
		catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException) { }
	}

	private static bool HasCurrentInspection(NxmDownloadItem item, string directory)
	{
		if (item.Inspection is not { } inspection || !IsLeafName(item.CompletedFileName)
			|| !String.Equals(item.CompletedFileName, inspection.FileName, StringComparison.OrdinalIgnoreCase)) return false;
		try
		{
			var file = new FileInfo(Path.Combine(directory, item.CompletedFileName));
			return file.Exists && file.Length == inspection.Length && file.LastWriteTimeUtc == inspection.LastWriteUtc;
		}
		catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException) { return false; }
	}

	private static bool IsLeafName(string name) => !String.IsNullOrWhiteSpace(name) && name == Path.GetFileName(name);
	private static string NormalizeUuid(string uuid) => Guid.TryParse(uuid, out var parsed) ? parsed.ToString("D") : (uuid ?? String.Empty).Trim();
}
