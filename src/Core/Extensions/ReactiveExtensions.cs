using System.Linq.Expressions;

namespace DivinityModManager;

public static class ReactiveExtensions
{
	/// <summary>
	/// ToPropertyEx with deferSubscription set to true, and the default scheduler set to RxApp.MainThreadScheduler.
	/// </summary>
	/// <typeparam name="TObj"></typeparam>
	/// <typeparam name="TRet"></typeparam>
	/// <param name="obs"></param>
	/// <param name="source"></param>
	/// <param name="property"></param>
	/// <param name="initialValue"></param>
	/// <returns></returns>
	public static ObservableAsPropertyHelper<TRet> ToUIProperty<TObj, TRet>(this IObservable<TRet> obs, TObj source, Expression<Func<TObj, TRet>> property, TRet initialValue = default) where TObj : ReactiveObject
	{
		return obs.ToPropertyEx(source, property, initialValue, true, RxApp.MainThreadScheduler);
	}

	/// <summary>
	/// ToPropertyEx with deferSubscription set to false, and the default scheduler set to RxApp.MainThreadScheduler.
	/// deferSubscription is false so the value is set immediately, which is important when used in other logic, such as collection filters.
	/// </summary>
	/// <typeparam name="TObj"></typeparam>
	/// <typeparam name="TRet"></typeparam>
	/// <param name="obs"></param>
	/// <param name="source"></param>
	/// <param name="property"></param>
	/// <param name="initialValue"></param>
	/// <returns></returns>
	public static ObservableAsPropertyHelper<TRet> ToUIPropertyImmediate<TObj, TRet>(this IObservable<TRet> obs, TObj source, Expression<Func<TObj, TRet>> property, TRet initialValue = default) where TObj : ReactiveObject
	{
		return obs.ToPropertyEx(source, property, initialValue, false, RxApp.MainThreadScheduler);
	}

}
