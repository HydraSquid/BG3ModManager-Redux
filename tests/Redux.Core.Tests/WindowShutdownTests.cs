using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

using DivinityModManager.Controls;
using DivinityModManager.Util;

namespace Redux.Core.Tests;

internal sealed class WindowShutdownTests
{
	public void IdleShutdownDefersFinalCloseUntilClosingReturns()
	{
		WithWindow((window, _) =>
		{
			var prepared = false;
			var closed = false;
			Exception? failure = null;
			ReduxWindowBehavior.AttachAsyncShutdown(window, () =>
			{
				prepared = true;
				return Task.CompletedTask;
			}, ex => failure = ex);
			window.Closed += (_, _) => closed = true;
			window.Close();
			RegressionAssert.False(prepared);
			RegressionAssert.True(window.IsVisible);
			PumpUntil(() => closed || failure != null);
			RegressionAssert.True(failure == null);
			RegressionAssert.True(closed);
		});
	}

	public void FailedShutdownKeepsMainWindowVisibleAndCanBeRetried()
	{
		WithWindow((window, titleBar) =>
		{
			var pending = new TaskCompletionSource<bool>();
			var prepareCount = 0;
			var failures = 0;
			var closed = false;
			ReduxWindowBehavior.AttachAsyncShutdown(window, async () =>
			{
				if (++prepareCount == 1)
				{
					await pending.Task;
					throw new IOException("Test shutdown persistence failure");
				}
			}, _ => failures++);
			window.Closed += (_, _) => closed = true;
			try
			{
				((Button)titleBar.FindName("CloseButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
				PumpUntil(() => prepareCount == 1);
				RegressionAssert.True(window.IsVisible);
				RegressionAssert.False(window.IsEnabled);
				RegressionAssert.Equal(1.0, titleBar.Opacity);
				window.Close();
				RegressionAssert.Equal(1, prepareCount);
				pending.SetResult(true);
				PumpUntil(() => failures == 1);
				RegressionAssert.True(window.IsVisible);
				RegressionAssert.True(window.IsEnabled);
				RegressionAssert.False(closed);
				((Button)titleBar.FindName("CloseButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
				PumpUntil(() => closed);
				RegressionAssert.Equal(2, prepareCount);
			}
			finally { pending.TrySetResult(true); }
		});
	}

	public void CanceledClosingDoesNotStartNexusShutdownAndCanBeRetried()
	{
		WithWindow((window, _) =>
		{
			var cancel = true;
			var prepared = false;
			var closed = false;
			window.Closing += (_, e) => e.Cancel = cancel;
			ReduxWindowBehavior.AttachAsyncShutdown(window, () =>
			{
				prepared = true;
				return Task.CompletedTask;
			}, ex => throw new InvalidOperationException("Unexpected shutdown failure", ex));
			window.Closed += (_, _) => closed = true;
			try
			{
				window.Close();
				window.Dispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
				RegressionAssert.False(prepared);
				RegressionAssert.True(window.IsVisible);
				RegressionAssert.True(window.IsEnabled);
				cancel = false;
				window.Close();
				PumpUntil(() => closed);
				RegressionAssert.True(prepared);
			}
			finally { cancel = false; }
		});
	}

	private static void WithWindow(Action<Window, ReduxWindowTitleBar> test)
	{
		var app = Application.Current;
		var mainWindow = app.MainWindow;
		var shutdownMode = app.ShutdownMode;
		var reduceMotion = ReduxWindowBehavior.ReduceMotion;
		var disableEffects = ReduxWindowBehavior.BackgroundEffectsDisabled;
		app.ShutdownMode = ShutdownMode.OnExplicitShutdown;
		try
		{
			foreach (var reduced in new[] { true, false })
			{
				ReduxWindowBehavior.ConfigureAccessibility(reduced, true);
				var titleBar = new ReduxWindowTitleBar();
				var window = new Window
				{
					Content = titleBar, Width = 500, Height = 240, Left = -15000, Top = -15000,
					ShowActivated = false, ShowInTaskbar = false
				};
				app.MainWindow = window;
				window.Show();
				try { test(window, titleBar); }
				finally
				{
					window.Close();
					PumpUntil(() => !window.IsVisible);
				}
			}
		}
		finally
		{
			app.MainWindow = mainWindow;
			app.ShutdownMode = shutdownMode;
			ReduxWindowBehavior.ConfigureAccessibility(reduceMotion, disableEffects);
		}
	}

	private static void PumpUntil(Func<bool> condition)
	{
		if (condition()) return;
		var frame = new DispatcherFrame();
		var elapsed = Stopwatch.StartNew();
		var timer = new DispatcherTimer(DispatcherPriority.Background) { Interval = TimeSpan.FromMilliseconds(10) };
		timer.Tick += (_, _) =>
		{
			if (condition() || elapsed.Elapsed > TimeSpan.FromSeconds(5)) frame.Continue = false;
		};
		timer.Start();
		try { Dispatcher.PushFrame(frame); }
		finally { timer.Stop(); }
		RegressionAssert.True(condition());
	}
}
