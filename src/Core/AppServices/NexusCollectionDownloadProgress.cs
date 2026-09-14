using DivinityModManager.Models.NexusMods;

namespace DivinityModManager.AppServices;

public sealed record NexusCollectionDownloadProgress(NxmDownloadItem NextFile, int Total, int AwaitingLink, int Downloaded, int Installed, int Failed)
{
    public static NexusCollectionDownloadProgress From(IEnumerable<NexusCollectionFile> files, IEnumerable<NxmDownloadItem> downloads)
    {
        var identities = files.Select(f => (f.ModId, f.FileId)).ToHashSet();
        var matching = downloads.Where(d => identities.Contains((d.ModId, d.FileId))).OrderBy(d => d.QueuePosition).ToArray();
        return new(matching.FirstOrDefault(d => d.State == NxmDownloadState.NeedsFreshLink), matching.Length,
            matching.Count(d => d.State == NxmDownloadState.NeedsFreshLink),
            matching.Count(d => d.State is NxmDownloadState.Downloaded or NxmDownloadState.NeedsReview or NxmDownloadState.Installing),
            matching.Count(d => d.State == NxmDownloadState.Installed),
            matching.Count(d => d.State is NxmDownloadState.Failed or NxmDownloadState.InstallFailed));
    }
}
