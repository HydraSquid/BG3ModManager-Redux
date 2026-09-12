using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using DivinityModManager.Util;

namespace Redux.Core.Tests;

public sealed class WindowDismissalTests
{
	public void ExitKeepsContentTransparentUntilDismissed()
	{
		var reduced = ReduxWindowBehavior.ReduceMotion;
		var effects = ReduxWindowBehavior.BackgroundEffectsDisabled;
		var content = new Border();
		var window = CreateWindow(content);
		try
		{
			ReduxWindowBehavior.ConfigureAccessibility(false, effects);
			window.Show();
			var completed = false;
			double opacityAtDismissal = -1;
			ReduxWindowBehavior.AnimateExit(window, () =>
			{
				opacityAtDismissal = content.Opacity;
				window.Hide();
				completed = true;
			});
			PumpUntil(() => completed);
			if (SystemParameters.ClientAreaAnimation) RegressionAssert.Equal(0d, opacityAtDismissal);
			RegressionAssert.Equal(1d, content.Opacity);
			RegressionAssert.False(window.IsVisible);
		}
		finally
		{
			window.Close();
			ReduxWindowBehavior.ConfigureAccessibility(reduced, effects);
		}
	}

	public void RepeatedCloseWaitsForOneDismissalEvenWhenMotionChanges()
	{
		var reduced = ReduxWindowBehavior.ReduceMotion;
		var effects = ReduxWindowBehavior.BackgroundEffectsDisabled;
		var window = CreateWindow(new Border());
		var closed = 0;
		try
		{
			ReduxWindowBehavior.ConfigureAccessibility(false, effects);
			ReduxWindowBehavior.AttachDialogTransitions(window, dimOwner: false);
			ReduxWindowBehavior.AttachDialogTransitions(window, dimOwner: false);
			window.Closed += (_, _) => closed++;
			window.Show();
			window.Close();
			if (SystemParameters.ClientAreaAnimation)
			{
				RegressionAssert.Equal(0, closed);
				ReduxWindowBehavior.ConfigureAccessibility(true, effects);
				window.Close();
				RegressionAssert.Equal(0, closed);
			}
			PumpUntil(() => closed > 0);
			RegressionAssert.Equal(1, closed);
		}
		finally
		{
			if (closed == 0) window.Hide();
			if (closed == 0) window.Close();
			ReduxWindowBehavior.ConfigureAccessibility(reduced, effects);
		}
	}

	public void CanceledCloseDoesNotStartExitAnimation()
	{
		var window = CreateWindow(new Border());
		var cancel = true;
		var attempts = 0;
		window.Closing += (_, e) => { attempts++; e.Cancel = cancel; };
		ReduxWindowBehavior.AttachDialogTransitions(window, dimOwner: false);
		try
		{
			window.Show();
			window.Close();
			// Pump beyond both entrance and exit durations to catch deferred re-closes.
			var timerDone = false;
			var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
			timer.Tick += (_, _) => { timer.Stop(); timerDone = true; };
			timer.Start();
			PumpUntil(() => timerDone);
			RegressionAssert.True(window.IsVisible);
			RegressionAssert.Equal(1, attempts);
			RegressionAssert.Equal(1d, ((Border)window.Content).Opacity);
		}
		finally { cancel = false; window.Hide(); window.Close(); }
	}

	private static Window CreateWindow(Border content) => new()
	{
		Content = content, Width = 200, Height = 100, Left = -10000, Top = -10000,
		ShowActivated = false, ShowInTaskbar = false, WindowStartupLocation = WindowStartupLocation.Manual
	};

	private static void PumpUntil(Func<bool> done)
	{
		var deadline = DateTime.UtcNow.AddSeconds(5);
		var frame = new DispatcherFrame();
		var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(10) };
		timer.Tick += (_, _) => { if (done() || DateTime.UtcNow >= deadline) frame.Continue = false; };
		timer.Start();
		try { Dispatcher.PushFrame(frame); }
		finally { timer.Stop(); }
		RegressionAssert.True(done());
	}
}
