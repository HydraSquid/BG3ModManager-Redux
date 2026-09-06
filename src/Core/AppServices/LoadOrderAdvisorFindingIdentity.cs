using DivinityModManager.Models.Health;

namespace DivinityModManager.AppServices;

public static class LoadOrderAdvisorFindingIdentity
{
	public static bool IsAdvisorFinding(ModHealthFinding finding) => finding?.Code is
		ModHealthFindingCode.DependencyLoadsLater or
		ModHealthFindingCode.RecommendedPredecessorLoadsLater or
		ModHealthFindingCode.DependencyCycle;

	public static string Create(string affectedModUuid, ModHealthFinding finding) => finding == null
		? String.Empty
		: Create(affectedModUuid, finding.Code, finding.RelatedModUuids);

	public static string Create(
		string affectedModUuid,
		ModHealthFindingCode code,
		IEnumerable<string> relatedModUuids)
	{
		if (String.IsNullOrWhiteSpace(affectedModUuid)) return String.Empty;
		var related = (relatedModUuids ?? [])
			.Where(uuid => !String.IsNullOrWhiteSpace(uuid))
			.Select(uuid => uuid.Trim().ToLowerInvariant())
			.Distinct(StringComparer.Ordinal)
			.OrderBy(uuid => uuid, StringComparer.Ordinal);
		return $"{code}|{affectedModUuid.Trim().ToLowerInvariant()}|{String.Join(",", related)}";
	}
}
