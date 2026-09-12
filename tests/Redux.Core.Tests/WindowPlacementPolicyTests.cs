using System;
using System.Windows;
using DivinityModManager.Models;
using DivinityModManager.Util;

namespace Redux.Core.Tests;

public sealed class WindowPlacementPolicyTests
{
	public void SavedBoundsRemainVisibleAcrossMonitorChanges()
	{
		var primary = new Rect(0, 0, 1920, 1040);
		var left = new Rect(-1920, 0, 1920, 1040);
		var above = new Rect(0, -1080, 1920, 1040);
		foreach (var area in new[] { primary, left, above })
		{
			var saved = new WindowSettings { X = area.X + 100, Y = area.Y + 50, Width = 1440, Height = 900 };
			var result = WindowPlacementPolicy.Restore(saved, new[] { primary, left, above }, primary, new Size(1440, 900));
			RegressionAssert.Equal(new Rect(saved.X, saved.Y, 1440, 900), result);
			var restoredAgain = WindowPlacementPolicy.Restore(new WindowSettings
				{ X = result.X, Y = result.Y, Width = result.Width, Height = result.Height },
				new[] { primary, left, above }, primary, new Size(1440, 900));
			RegressionAssert.Equal(result, restoredAgain);
		}
		foreach (var saved in new[] {
			new WindowSettings(),
			new WindowSettings { X = -32000, Y = -32000, Width = 1440, Height = 900 },
			new WindowSettings { X = 2500, Y = 100, Width = 1440, Height = 900 },
			new WindowSettings { X = Double.NaN, Y = Double.PositiveInfinity, Width = Double.NaN, Height = 900 },
			new WindowSettings { X = 1800, Y = 1000, Width = 3000, Height = 2000 } })
		{
			var result = WindowPlacementPolicy.Restore(saved, new[] { primary }, primary, new Size(1440, 900));
			RegressionAssert.True(primary.Contains(result));
		}
		// A maximized window is positioned on its selected screen before maximizing.
		var maximized = new WindowSettings { Maximized = true, X = 100, Y = 100, Width = 1440, Height = 900 };
		RegressionAssert.True(left.Contains(WindowPlacementPolicy.Restore(maximized,
			new[] { left }, left, new Size(1440, 900))));
		// Work areas expressed in DIPs shrink at increased Windows scaling.
		var scaledArea = new Rect(0, 0, 1280, 680);
		RegressionAssert.True(scaledArea.Contains(WindowPlacementPolicy.Restore(maximized,
			new[] { scaledArea }, scaledArea, new Size(1440, 900))));
	}
}
