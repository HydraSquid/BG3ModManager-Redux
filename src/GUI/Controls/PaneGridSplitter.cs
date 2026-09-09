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
		var splitterColumn = Grid.GetColumn(this);
		if (splitterColumn <= 0 || splitterColumn >= grid.ColumnDefinitions.Count - 1) return;
		_grid = grid;
		_widths = grid.ColumnDefinitions.Select(column => column.Width).ToArray();
		_minimums = grid.ColumnDefinitions.Select(column => column.MinWidth).ToArray();
		var sizes = grid.ColumnDefinitions.Select(column => column.ActualWidth).ToArray();
		for (var index = 0; index < sizes.Length; index++)
		{
			var adjacent = index == splitterColumn - 1 || index == splitterColumn + 1;
			var column = grid.ColumnDefinitions[index];
			// Only the adjacent pair shares flexible space during a drag. Preserve any
			// smaller existing size so beginning a drag cannot itself move a divider.
			if (adjacent) column.MinWidth = Math.Max(column.MinWidth, Math.Min(180, sizes[index]));
			column.Width = new GridLength(sizes[index], adjacent ? GridUnitType.Star : GridUnitType.Pixel);
		}
	}

	private void EndResize(bool canceled)
	{
		if (_widths == null) return;
		_grid.UpdateLayout();
		var sizes = _grid.ColumnDefinitions.Select(column => column.ActualWidth).ToArray();
		for (var index = 0; index < _widths.Length; index++)
		{
			var column = _grid.ColumnDefinitions[index];
			column.MinWidth = _minimums[index];
			column.Width = canceled ? _widths[index]
				: new GridLength(sizes[index], _widths[index].IsStar ? GridUnitType.Star : GridUnitType.Pixel);
		}
		_widths = null;
		_minimums = null;
		_grid = null;
	}
}
