using DivinityModManager.Models;

namespace DivinityModManager.AppServices;

public enum SaveModStatus { Active, Inactive, Missing, Unavailable }
public sealed record SaveModRequirement(string UUID, string Name, SaveModStatus Status, string Detail = "")
{
    public bool CanActivate => Status == SaveModStatus.Inactive;
    public string StatusText => Status switch
    {
        SaveModStatus.Active => "Active",
        SaveModStatus.Inactive => "Inactive",
        SaveModStatus.Missing => "Missing",
        _ => "Needs attention"
    };
}

/// <summary>Matches the save's recorded UUIDs, never names. Does not mutate lists or game files.</summary>
public static class SaveModReviewService
{
    private static string Key(string value) => Guid.TryParse(value, out var id) ? id.ToString("D") : null;

    public static IReadOnlyList<SaveModRequirement> Review(IEnumerable<DivinityLoadOrderEntry> required,
        IEnumerable<DivinityModData> installed, IEnumerable<string> active, IEnumerable<string> inactive)
    {
        var inventory = installed.Where(m => !m.IsVisualDivider && Key(m.UUID) != null).Distinct()
            .GroupBy(m => Key(m.UUID)).ToDictionary(g => g.Key, g => g.ToArray());
        var activeIds = active.Select(Key).Where(x => x != null).ToHashSet();
        var inactiveIds = inactive.Select(Key).Where(x => x != null).ToHashSet();
        var seen = new HashSet<string>();
        var result = new List<SaveModRequirement>();
        foreach (var entry in required)
        {
            var key = Key(entry.UUID);
            if (!seen.Add(key ?? entry.UUID ?? entry.Name ?? "")) continue;
            var status = SaveModStatus.Unavailable;
            var detail = "The save contains an invalid mod UUID.";
            if (key != null)
            {
                if (!inventory.TryGetValue(key, out var matches)) { status = SaveModStatus.Missing; detail = "No installed mod matches this UUID."; }
                else if (matches.Length == 1)
                {
                    detail = "Installed, but not available in the inactive list for activation.";
                    if (activeIds.Contains(key)) { status = SaveModStatus.Active; detail = "Already in your current active order."; }
                    else if (inactiveIds.Contains(key) && matches[0].CanAddToLoadOrder) { status = SaveModStatus.Inactive; detail = "Available to activate. Added after your existing active mods."; }
                }
                else detail = "Multiple installed files use this UUID. Resolve the duplicate first.";
            }
            result.Add(new SaveModRequirement(entry.UUID ?? "", String.IsNullOrWhiteSpace(entry.Name) ? entry.UUID ?? "Unknown mod" : entry.Name, status, detail));
        }
        return result;
    }

    public static IReadOnlyList<string> SelectedActivationIds(IEnumerable<SaveModRequirement> review, IEnumerable<string> selected)
    {
        var ids = selected.Select(Key).Where(x => x != null).ToHashSet();
        return review.Where(r => r.CanActivate && ids.Contains(Key(r.UUID))).Select(r => r.UUID).ToArray();
    }
}
