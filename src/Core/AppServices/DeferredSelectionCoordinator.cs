namespace DivinityModManager.AppServices;

public sealed class DeferredSelectionCoordinator<T> where T : class
{
	private int _generation;
	public void Schedule(
		T selectedContext,
		Action<Action> defer,
		IEnumerable<T> contexts,
		Action<T> clearSelection)
	{
		var generation = ++_generation;
		defer(() =>
		{
			if (generation != _generation) return;
			foreach (var context in contexts)
			{
				if (generation != _generation) return;
				if (!ReferenceEquals(context, selectedContext)) clearSelection(context);
			}
		});
	}
}
