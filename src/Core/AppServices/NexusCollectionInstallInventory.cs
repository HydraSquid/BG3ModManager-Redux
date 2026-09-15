namespace DivinityModManager.AppServices;

public sealed record NexusInstalledFile(long ModId, long FileId, string Location = "Mod library");
public enum NexusCollectionInstallState { Missing, Installed, DifferentFile, Unverified }

/// <summary>Exact file identity only; a Nexus project can contain unrelated optional files.</summary>
public sealed class NexusCollectionInstallInventory
{
    private readonly Dictionary<long, HashSet<long>> _files;
    private readonly NexusInstalledFile[] _installed;
    public string Warning { get; init; } = "";
    public NexusCollectionInstallInventory(IEnumerable<NexusInstalledFile> files)
    {
        _installed = files.ToArray();
        _files = _installed.Where(f => f.ModId > 0).GroupBy(f => f.ModId)
            .ToDictionary(g => g.Key, g => g.Select(f => f.FileId).ToHashSet());
    }
    public string GetLocation(NexusCollectionFile file)
    {
        var matches = _installed.Where(f => f.ModId == file.ModId).ToArray();
        var exact = matches.Where(f => f.FileId == file.FileId).ToArray();
        return String.Join(" / ", (exact.Length > 0 ? exact : matches).Select(f => f.Location).Distinct());
    }
    public NexusCollectionInstallState GetState(NexusCollectionFile file)
    {
        if (!_files.TryGetValue(file.ModId, out var files)) return NexusCollectionInstallState.Missing;
        if (files.Contains(file.FileId)) return NexusCollectionInstallState.Installed;
        return files.Any(id => id > 0) ? NexusCollectionInstallState.DifferentFile : NexusCollectionInstallState.Unverified;
    }
}
