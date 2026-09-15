using DivinityModManager.Models;
using DivinityModManager.Models.NexusMods;

namespace DivinityModManager.AppServices;

public sealed record NexusUpdateInstalledFile(string Uuid, string Name, long ModId, long FileId,
	string InstalledVersion, bool HasExactFileIdentity)
{
	public string FilePath { get; init; }
	public string PackageSha256 { get; init; }
	public string DownloadVersion { get; init; }
	public string IdentityDescription { get; init; }
	public static NexusUpdateInstalledFile FromMod(DivinityModData mod)
	{
		if (mod == null || mod.IsLarianMod || mod.IsEditorMod || mod.IsVisualDivider
			|| mod.Metadata.SourceType != ModSourceType.NEXUSMODS) return null;
		var source = mod.NexusModsData;
		// A recorded ID is a hint until the resolver checks the current installed bytes.
		return new(mod.UUID, mod.DisplayName, source.ModId, source.LastFileId,
			mod.Version?.ToString() ?? "Unknown", false) { FilePath = mod.FilePath,
			IdentityDescription = source.LastFileId > 0 ? $"Recorded download file #{source.LastFileId}; local verification pending." : null };
	}
}

public sealed record NexusRemoteFile(long FileId, string Name, string Version, int CategoryId)
{
	public bool IsDownloadable => CategoryId is 1 or 2 or 3;
	public string ChoiceLabel => $"{Name} · release {Version} · file #{FileId}"
		+ (IsDownloadable ? String.Empty : " · historical file");
}

public sealed record NexusFileReplacement(long OldFileId, long NewFileId);
public sealed record NexusProjectFiles(List<NexusRemoteFile> Files, List<NexusFileReplacement> Replacements);

public enum NexusModUpdateStatus { UpdateAvailable, NeedsReview, CheckFailed, NotChecked, NoUpdateReported, DownloadNotIdentified, Acknowledged }

public sealed record NexusModUpdateResult(NexusUpdateInstalledFile Installed, NexusModUpdateStatus Status,
	string Reason, NexusRemoteFile Candidate = null, DateTimeOffset? CheckedUtc = null, bool FromCache = false)
{
	public IReadOnlyList<NexusRemoteFile> AvailableFiles { get; init; } = Array.Empty<NexusRemoteFile>();
	public NexusUpdateAcknowledgement Acknowledgement { get; init; }
	public bool CanChooseReference { get; init; }
	public string ReferenceLabel => Acknowledgement == null ? String.Empty
		: $"Your reference: {Acknowledgement.FileName} · release {Acknowledgement.Version} · file #{Acknowledgement.FileId}. This is your choice, not proof of installation.";
	public string Name => Installed.Name;
	public string StatusLabel => Status switch
	{
		NexusModUpdateStatus.UpdateAvailable => "Update available",
		NexusModUpdateStatus.NeedsReview => "Needs review",
		NexusModUpdateStatus.DownloadNotIdentified => "Choose an installed-download reference",
		NexusModUpdateStatus.Acknowledged => "No newer replacement since your reference",
		NexusModUpdateStatus.CheckFailed => "Check failed",
		NexusModUpdateStatus.NoUpdateReported => "No update reported",
		_ => "Not checked"
	};
	public string InstalledLabel => $"Package metadata version: {Installed.InstalledVersion}\n"
		+ (Installed.HasExactFileIdentity ? $"Recorded Nexus file #{Installed.FileId}" : "Nexus project linked · Installed download not identified")
		+ (String.IsNullOrEmpty(Installed.DownloadVersion) ? String.Empty : $"\nVerified download release: {Installed.DownloadVersion}")
		+ (String.IsNullOrEmpty(Installed.IdentityDescription) ? String.Empty : $"\n{Installed.IdentityDescription}");
	public string CandidateLabel => Candidate == null ? String.Empty
		: $"Replacement: {Candidate.Name}  ·  {Candidate.Version}  ·  file #{Candidate.FileId}";
	public string CheckedLabel => CheckedUtc.HasValue
		? $"Checked {CheckedUtc.Value.LocalDateTime:g}" + (FromCache ? " · cached" : String.Empty)
		: "No successful file check yet";
	public string FilesPageUrl => $"https://www.nexusmods.com/baldursgate3/mods/{Installed.ModId}?tab=files"
		+ (Candidate == null ? String.Empty : $"&file_id={Candidate.FileId}");
}

/// <summary>Uses explicit Nexus replacement relationships, never project versions or file-ID ordering.</summary>
public static class NexusModUpdateEvaluator
{
	public static NexusModUpdateResult Evaluate(NexusUpdateInstalledFile installed, NexusProjectFiles project,
		DateTimeOffset? checkedUtc = null, bool fromCache = false)
	{
		NexusModUpdateResult Result(NexusModUpdateStatus status, string reason, NexusRemoteFile file = null) =>
			new(installed, status, reason, file, checkedUtc, fromCache);
		if (!installed.HasExactFileIdentity || installed.FileId <= 0)
			return Result(NexusModUpdateStatus.DownloadNotIdentified,
				project == null ? "Check for updates to list this project's downloads. Redux will not guess which one you installed."
				: "File list checked. Choose the matching main/optional release as your reference, or open Nexus Files. No update conclusion is available yet.");
		if (project == null)
			return Result(NexusModUpdateStatus.NotChecked, "Run a manual check to read this project's file history.");
		var next = project.Replacements.GroupBy(edge => edge.OldFileId)
			.ToDictionary(group => group.Key, group => group.Select(edge => edge.NewFileId).Distinct().ToArray());
		var current = installed.FileId;
		var visited = new HashSet<long>();
		while (next.TryGetValue(current, out var replacements))
		{
			if (!visited.Add(current) || replacements.Length != 1)
				return Result(NexusModUpdateStatus.NeedsReview,
					"Nexus reports an ambiguous or circular replacement history. Review the files manually.");
			current = replacements[0];
		}
		var matches = project.Files.Where(file => file.FileId == current).ToArray();
		if (matches.Length != 1 || !matches[0].IsDownloadable)
			return Result(NexusModUpdateStatus.NeedsReview,
				"The file or its replacement is missing, archived, or no longer a current download. Review the project's Files page.");
		return current != installed.FileId
			? Result(NexusModUpdateStatus.UpdateAvailable, "Nexus explicitly lists this file as a replacement for the recorded installed file.", matches[0])
			: Result(NexusModUpdateStatus.NoUpdateReported,
				"Nexus reports no replacement for this file. Other optional files or unlinked uploads may still exist.");
	}
}
