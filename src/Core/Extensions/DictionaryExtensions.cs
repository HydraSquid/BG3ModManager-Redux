namespace DivinityModManager.Extensions;

public static class DictionaryExtensions
{
	/// <summary>
	/// ToDictionary that allows duplicate key entries.
	/// Source: https://stackoverflow.com/a/22508992/2290477
	/// </summary>
	/// <typeparam name="TSource"></typeparam>
	/// <typeparam name="TKey"></typeparam>
	/// <typeparam name="TElement"></typeparam>
	/// <param name="source"></param>
	/// <param name="keySelector"></param>
	/// <param name="elementSelector"></param>
	/// <param name="comparer"></param>
	/// <returns></returns>
	public static Dictionary<TKey, TElement> SafeToDictionary<TSource, TKey, TElement>(
	this IEnumerable<TSource> source,
	Func<TSource, TKey> keySelector,
	Func<TSource, TElement> elementSelector,
	IEqualityComparer<TKey> comparer = null)
	{
		var dictionary = new Dictionary<TKey, TElement>(comparer);

		if (source == null)
		{
			return dictionary;
		}

		foreach (TSource element in source)
		{
			dictionary[keySelector(element)] = elementSelector(element);
		}

		return dictionary;
	}
}
