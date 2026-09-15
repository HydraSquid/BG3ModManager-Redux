using System.Text.Json;
using DivinityModManager.Util;

namespace DivinityModManager.AppServices;

public sealed record NexusCollectionFileIdentity(long ModId, long FileId);
public sealed record NexusCollectionSession(string Slug, string Name, int Revision, NexusCollectionFileIdentity[] SelectedFiles)
{
    public string Link => $"https://www.nexusmods.com/games/baldursgate3/collections/{Slug}";
    public bool Matches(NexusCollectionPreview preview) => Revision == preview.Revision && String.Equals(Slug, preview.Slug, StringComparison.OrdinalIgnoreCase);
}

/// <summary>Remembers choices only. Download progress and authorization remain owned by the download queue.</summary>
public sealed class NexusCollectionSessionStore(string path)
{
    private const int MaximumBytes = 8 * 1024 * 1024;
    public IReadOnlyList<NexusCollectionSession> Load()
    {
        if (!File.Exists(path)) return Array.Empty<NexusCollectionSession>();
        if (new FileInfo(path).Length > MaximumBytes) throw new InvalidDataException("Saved collection choices are too large.");
        var sessions = JsonSerializer.Deserialize<NexusCollectionSession[]>(File.ReadAllText(path));
        if (sessions == null || sessions.Length > 10) throw new InvalidDataException("Saved collection choices are invalid.");
        foreach (var session in sessions) Validate(session);
        return sessions;
    }
    public IReadOnlyList<NexusCollectionSession> Save(NexusCollectionSession session)
    {
        Validate(session);
        var sessions = new[] { session }.Concat(Load().Where(s => !s.Slug.Equals(session.Slug, StringComparison.OrdinalIgnoreCase))).Take(10).ToArray();
        var json = JsonSerializer.Serialize(sessions);
        if (System.Text.Encoding.UTF8.GetByteCount(json) > MaximumBytes) throw new InvalidDataException("Saved collection choices are too large.");
        AtomicFileWriter.WriteAllText(path, json);
        return sessions;
    }
    private static void Validate(NexusCollectionSession session)
    {
        if (session == null || session.Name == null || session.Name.Length > 500 || session.Revision <= 0
            || !NexusCollectionLink.TryParse(session.Link, out var link) || link.Slug != session.Slug
            || session.SelectedFiles == null || session.SelectedFiles.Length > 10000
            || session.SelectedFiles.Any(f => f == null || f.ModId <= 0 || f.FileId <= 0)
            || session.SelectedFiles.Distinct().Count() != session.SelectedFiles.Length)
            throw new InvalidDataException("Saved collection choices are invalid.");
    }
}
