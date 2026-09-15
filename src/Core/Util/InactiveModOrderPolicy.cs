using DivinityModManager.Models;

namespace DivinityModManager.Util;

/// <summary>Redux-only inactive organization; never a game load order.</summary>
public static class InactiveModOrderPolicy
{
	public static IReadOnlyList<DivinityModData> Restore(IEnumerable<DivinityModData> mods, IEnumerable<string> savedOrder)
	{
		var ranks = (savedOrder ?? []).Where(id => !String.IsNullOrWhiteSpace(id))
			.Distinct(StringComparer.OrdinalIgnoreCase).Select((id, index) => (id, index))
			.ToDictionary(pair => pair.id, pair => pair.index, StringComparer.OrdinalIgnoreCase);
		// Preserve the existing discovery order for new/unrecorded mods.
		return mods.OrderBy(mod => ranks.TryGetValue(mod.UUID ?? "", out var rank) ? rank : Int32.MaxValue).ToList();
	}

	public static List<string> Capture(IEnumerable<DivinityModData> mods, IEnumerable<string> previousOrder) =>
		mods.Where(mod => !mod.IsVisualDivider).Select(mod => mod.UUID)
			.Concat(previousOrder ?? []).Where(id => !String.IsNullOrWhiteSpace(id))
			.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
}
