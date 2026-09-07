namespace DivinityModManager.AppServices;

public sealed class NxmDownloadScheduler
{
	private readonly object _sync = new();
	private readonly LinkedList<ScheduledWork> _queue = new();
	private int _active;
	private int _limit;
	private DateTimeOffset _suspendedUntil;
	private bool _suspensionTimerPending;
	private Task _lastStart = Task.CompletedTask;

	public NxmDownloadScheduler(int limit = 4) => _limit = Math.Clamp(limit, 1, 10);

	public int Limit
	{
		get { lock (_sync) return _limit; }
	}

	public int ActiveCount
	{
		get { lock (_sync) return _active; }
	}

	public int QueuedCount
	{
		get { lock (_sync) return _queue.Count; }
	}

	public Task Enqueue(Func<Task> work, CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(work);
		if (cancellationToken.IsCancellationRequested) return Task.FromCanceled(cancellationToken);
		var item = new ScheduledWork(work, cancellationToken);
		lock (_sync)
		{
			item.Node = _queue.AddLast(item);
			if (cancellationToken.CanBeCanceled)
				item.CancellationRegistration = cancellationToken.Register(() => CancelQueued(item));
			DispatchLocked();
		}
		return item.Completion.Task;
	}

	public void SetLimit(int limit)
	{
		lock (_sync)
		{
			_limit = Math.Clamp(limit, 1, 10);
			DispatchLocked();
		}
	}

	public void SuspendUntil(DateTimeOffset reset)
	{
		lock (_sync)
		{
			if (reset > _suspendedUntil) _suspendedUntil = reset;
			DispatchLocked();
		}
	}

	private void DispatchLocked()
	{
		var delay = _suspendedUntil - DateTimeOffset.UtcNow;
		if (delay > TimeSpan.Zero)
		{
			if (!_suspensionTimerPending) _ = ResumeAfterSuspensionAsync(delay);
			_suspensionTimerPending = true;
			return;
		}
		while (_active < _limit && _queue.First != null)
		{
			var item = _queue.First.Value;
			_queue.RemoveFirst();
			item.Node = null;
			item.StartAfter = _lastStart;
			_lastStart = item.Started.Task;
			_active++;
			_ = Task.Run(() => RunAsync(item));
		}
	}

	private async Task ResumeAfterSuspensionAsync(TimeSpan delay)
	{
		await Task.Delay(delay).ConfigureAwait(false);
		lock (_sync)
		{
			_suspensionTimerPending = false;
			DispatchLocked();
		}
	}

	private async Task RunAsync(ScheduledWork item)
	{
		try
		{
			await item.StartAfter;
			Task work;
			try
			{
				item.CancellationToken.ThrowIfCancellationRequested();
				work = item.Work();
			}
			finally { item.Started.TrySetResult(); }
			await work;
			item.Completion.TrySetResult();
		}
		catch (OperationCanceledException ex)
		{
			item.Completion.TrySetCanceled(ex.CancellationToken);
		}
		catch (Exception ex)
		{
			item.Completion.TrySetException(ex);
		}
		finally
		{
			item.CancellationRegistration.Dispose();
			lock (_sync)
			{
				_active--;
				DispatchLocked();
			}
		}
	}

	private void CancelQueued(ScheduledWork item)
	{
		lock (_sync)
		{
			if (item.Node == null) return;
			_queue.Remove(item.Node);
			item.Node = null;
			item.Completion.TrySetCanceled(item.CancellationToken);
		}
	}

	private sealed class ScheduledWork
	{
		public Func<Task> Work { get; }
		public CancellationToken CancellationToken { get; }
		public TaskCompletionSource Completion { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
		public Task StartAfter { get; set; } = Task.CompletedTask;
		public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
		public LinkedListNode<ScheduledWork> Node { get; set; }
		public CancellationTokenRegistration CancellationRegistration { get; set; }

		public ScheduledWork(Func<Task> work, CancellationToken cancellationToken)
		{
			Work = work;
			CancellationToken = cancellationToken;
		}
	}
}
