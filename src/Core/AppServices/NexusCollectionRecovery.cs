using DivinityModManager.Models.NexusMods;

namespace DivinityModManager.AppServices;

public sealed record NexusCollectionRetryResult(int Retried, int Failed);
public static class NexusCollectionRecovery
{
    public static async Task<NexusCollectionRetryResult> RetryAsync(IEnumerable<NexusCollectionFile> files, IEnumerable<NxmDownloadItem> downloads,
        Func<string, Task> retry, CancellationToken token)
    {
        var retried = 0;
        var failed = 0;
        foreach (var item in Eligible(files, downloads))
        {
            token.ThrowIfCancellationRequested();
            if (!CanRetry(item)) continue;
            try
            {
                await retry(item.Id);
                if (item.State == NxmDownloadState.Failed) failed++; else retried++;
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception) { failed++; }
        }
        return new(retried, failed);
    }
    public static bool CanRetry(NxmDownloadItem item) => item.State == NxmDownloadState.Failed && item.ErrorCode != "rollback-failed";
    public static IReadOnlyList<NxmDownloadItem> Eligible(IEnumerable<NexusCollectionFile> files, IEnumerable<NxmDownloadItem> downloads)
    {
        var ids = files.Select(f => (f.ModId, f.FileId)).ToHashSet();
        return downloads.Where(d => ids.Contains((d.ModId, d.FileId)) && CanRetry(d)).OrderBy(d => d.QueuePosition).ToArray();
    }
}
