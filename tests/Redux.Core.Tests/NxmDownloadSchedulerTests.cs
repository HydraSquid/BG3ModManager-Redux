using DivinityModManager.AppServices;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Redux.Core.Tests;

internal sealed class NxmDownloadSchedulerTests
{
	public void DefaultLimitRunsFourAndQueuesTheRest()
	{
		var scheduler = new NxmDownloadScheduler();
		var releases = Enumerable.Range(0, 8).Select(_ => new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously)).ToArray();
		var started = new List<int>();
		var tasks = releases.Select((release, index) => scheduler.Enqueue(async () =>
		{
			lock (started) started.Add(index);
			await release.Task;
		})).ToArray();
		SpinWait.SpinUntil(() => scheduler.ActiveCount == 4, 2000);
		SpinWait.SpinUntil(() => { lock (started) return started.Count == 4; }, 2000);

		RegressionAssert.Equal(4, scheduler.ActiveCount);
		RegressionAssert.Equal(4, scheduler.QueuedCount);
		lock (started) RegressionAssert.Equal("0,1,2,3", String.Join(',', started));
		foreach (var release in releases) release.TrySetResult();
		Task.WhenAll(tasks).GetAwaiter().GetResult();
	}

	public void RaisingLimitDispatchesQueuedItemsInFifoOrder()
	{
		var scheduler = new NxmDownloadScheduler(1);
		var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
		var started = new List<int>();
		var tasks = Enumerable.Range(0, 4).Select(index => scheduler.Enqueue(async () =>
		{
			lock (started) started.Add(index);
			await release.Task;
		})).ToArray();
		SpinWait.SpinUntil(() => scheduler.ActiveCount == 1, 2000);
		scheduler.SetLimit(3);
		SpinWait.SpinUntil(() => scheduler.ActiveCount == 3, 2000);
		SpinWait.SpinUntil(() => { lock (started) return started.Count == 3; }, 2000);

		lock (started) RegressionAssert.Equal("0,1,2", String.Join(',', started));
		scheduler.SetLimit(99);
		RegressionAssert.Equal(10, scheduler.Limit);
		release.TrySetResult();
		Task.WhenAll(tasks).GetAwaiter().GetResult();
	}

	public void CancellationRemovesSuspendedQueuedWorkImmediately()
	{
		var scheduler = new NxmDownloadScheduler(1);
		scheduler.SuspendUntil(DateTimeOffset.UtcNow.AddMinutes(5));
		using var cancellation = new CancellationTokenSource();
		var started = false;
		var task = scheduler.Enqueue(() =>
		{
			started = true;
			return Task.CompletedTask;
		}, cancellation.Token);
		RegressionAssert.Equal(1, scheduler.QueuedCount);

		cancellation.Cancel();

		RegressionAssert.Throws<OperationCanceledException>(() => task.GetAwaiter().GetResult());
		RegressionAssert.Equal(0, scheduler.QueuedCount);
		RegressionAssert.False(started);
	}
}
