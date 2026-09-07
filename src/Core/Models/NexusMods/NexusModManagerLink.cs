namespace DivinityModManager.Models.NexusMods;

public sealed record NexusModManagerLink(
	long ModId,
	long FileId,
	string DownloadKey,
	long? ExpiresUnixSeconds,
	long? UserId);
