using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace DivinityModManager.Controls;

public sealed class PaneGridSplitter : GridSplitter
{
	private Grid _grid;
	private GridLength[] _widths;
	private double[] _minimums;

	public PaneGridSplitter()
	{
		ResizeDirection = GridResizeDirection.Columns;
		ResizeBehavior = GridResizeBehavior.PreviousAndNext;
		DragCompleted += (_, e) => EndResize(e.Canceled);
	}

	protected override void OnPreviewMouseLeftButtonDown(MouseButtonEventArgs e)
	{
		BeginResize();
		base.OnPreviewMouseLeftButtonDown(e);
	}

	protected override void OnKeyDown(KeyEventArgs e)
	{
		if (IsDragging || e.Key is not (Key.Left or Key.Right))
		{
			base.OnKeyDown(e);
			return;
		}
		BeginResize();
		try { base.OnKeyDown(e); }
		finally { EndResize(false); }
	}

	private void BeginResize()
	{
		if (_widths != null || Parent is not Grid grid) return;
		var index = Grid.GetColumn(this);
		if (index <= 0 || index >= grid.ColumnDefinitions.Count - 1) return;
		_grid = grid;
		_widths = grid.ColumnDefinitions.Select(column => column.Width).ToArray();
		_minimums = grid.ColumnDefinitions.Select(column => column.MinWidth).ToArray();
		var sizes = grid.ColumnDefinitions.Select(column => column.ActualWidth).ToArray();
		for (var i = 0; i < sizes.Length; i++)
		{
			var adjacent = i == index - 1 || i == index + 1;
			var column = grid.ColumnDefinitions[i];
			// Only the adjacent pair shares flexible space during a drag. Preserve any
			// smaller existing size so beginning a drag cannot itself move a divider.
			if (adjacent) column.MinWidth = Math.Max(column.MinWidth, Math.Min(180, sizes[i]));
			column.Width = new GridLength(sizes[i], adjacent ? GridUnitType.Star : GridUnitType.Pixel);
		}
	}

	private void EndResize(bool canceled)
	{
		if (_widths == null) return;
		_grid.UpdateLayout();
		var sizes = _grid.ColumnDefinitions.Select(column => column.ActualWidth).ToArray();
		for (var i = 0; i < _widths.Length; i++)
		{
			var column = _grid.ColumnDefinitions[i];
			column.MinWidth = _minimums[i];
			column.Width = canceled ? _widths[i]
				: new GridLength(sizes[i], _widths[i].IsStar ? GridUnitType.Star : GridUnitType.Pixel);
		}
		_widths = null;
		_minimums = null;
		_grid = null;
	}
}
