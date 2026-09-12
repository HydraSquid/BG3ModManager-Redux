using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;

using DivinityModManager.Models;

namespace DivinityModManager.Util;

public static class WindowPlacementPolicy
{
	public static Rect Restore(WindowSettings saved, IReadOnlyList<Rect> workAreas,
		Rect fallbackArea, Size defaultSize)
	{
		var width = Double.IsFinite(saved.Width) && saved.Width > 0 ? saved.Width : defaultSize.Width;
		var height = Double.IsFinite(saved.Height) && saved.Height > 0 ? saved.Height : defaultSize.Height;
		var hasBounds = Double.IsFinite(saved.X) && Double.IsFinite(saved.Y)
			&& Double.IsFinite(saved.Width) && saved.Width > 0
			&& Double.IsFinite(saved.Height) && saved.Height > 0;
		var bounds = hasBounds ? new Rect(saved.X, saved.Y, width, height) : Rect.Empty;
		var area = hasBounds ? workAreas.FirstOrDefault(candidate => candidate.IntersectsWith(bounds)) : Rect.Empty;
		if (area.IsEmpty || area.Width <= 0 || area.Height <= 0) area = fallbackArea;
		width = Math.Min(width, area.Width);
		height = Math.Min(height, area.Height);
		var x = hasBounds && area.IntersectsWith(bounds) ? saved.X : area.Left + (area.Width - width) / 2;
		var y = hasBounds && area.IntersectsWith(bounds) ? saved.Y : area.Top + (area.Height - height) / 2;
		return new Rect(Math.Clamp(x, area.Left, area.Right - width),
			Math.Clamp(y, area.Top, area.Bottom - height), width, height);
	}
}
