using DivinityModManager.Util;
using DivinityModManager.ViewModels;

using System.ComponentModel;
using System.Windows;

namespace DivinityModManager.Views;

public partial class DeleteFilesConfirmationView : AdonisUI.Controls.AdonisWindow
{
	public DeleteFilesViewData ViewModel { get; }

	private double GetLongestNameWidth()
	{
		var longestName = ViewModel.Files.OrderByDescending(x => x.DisplayName.Length).FirstOrDefault()?.DisplayName ?? String.Empty;
		if (String.IsNullOrEmpty(longestName)) return 0d;

		return ElementHelper.MeasureText(FilesListView, longestName,
			FilesListView.FontFamily,
			FilesListView.FontStyle,
			FilesListView.FontWeight,
			FilesListView.FontStretch,
			FilesListView.FontSize).Width + 48;
	}

	private void ResizeColumns()
	{
		var listWidth = FilesListView.ActualWidth;
		if (listWidth <= SystemParameters.VerticalScrollBarWidth || FileListGridView.Columns.Count < 3) return;

		var selectionWidth = FileListGridView.Columns[0].ActualWidth;
		if (Double.IsNaN(selectionWidth) || selectionWidth <= 0) selectionWidth = 34;
		var availableWidth = Math.Max(260,
			listWidth - SystemParameters.VerticalScrollBarWidth - selectionWidth - 2);
		var measuredNameWidth = GetLongestNameWidth();
		var nameWidth = Math.Clamp(measuredNameWidth <= 0 ? 220 : measuredNameWidth,
			180, Math.Max(180, availableWidth * 0.42));
		var width = Math.Max(120, availableWidth - nameWidth);
		FileListGridView.Columns[1].Width = nameWidth;

		if (FileListGridView.Columns.Count > 3)
		{
			FileListGridView.Columns[2].Width = width * 0.40;
			FileListGridView.Columns[3].Width = width * 0.60;
		}
		else
		{
			FileListGridView.Columns[2].Width = width;
		}
	}

	public DeleteFilesConfirmationView(Window? owner)
	{
		InitializeComponent();
		ViewModel = new DeleteFilesViewData();
		DataContext = ViewModel;
		if (owner?.IsLoaded == true) Owner = owner;

		var settings = MainWindow.Self?.ViewModel?.Settings;
		if (settings != null)
			ReduxThemeService.Apply(Resources, settings.ColorTheme,
				ReduxThemeService.GetActiveTheme(settings), settings.UsesGeneratedGradients);

		ReduxWindowBehavior.AttachDialogTransitions(this, 40);
		ReduxWindowBehavior.AttachRoundedCorners(this);

		FilesListView.Loaded += (_, _) => Dispatcher.BeginInvoke(ResizeColumns);
		FilesListView.SizeChanged += (_, _) => ResizeColumns();
		ViewModel.Files.CollectionChanged += (_, _) => Dispatcher.BeginInvoke(ResizeColumns);
		ViewModel.WhenAnyValue(x => x.IsDeletingDuplicates).Subscribe(isDeletingDuplicates =>
		{
			if (!isDeletingDuplicates)
			{
				FileListGridView.Columns.Remove(DuplicatesColumn);
			}
			else if (!FileListGridView.Columns.Contains(DuplicatesColumn))
			{
				FileListGridView.Columns.Add(DuplicatesColumn);
			}
			Dispatcher.BeginInvoke(ResizeColumns);
		});
		ViewModel.WhenAnyValue(x => x.IsVisible)
			.Skip(1)
			.Where(isVisible => !isVisible)
			.ObserveOn(RxApp.MainThreadScheduler)
			.Subscribe(_ => Close());
		ViewModel.IsVisible = true;
	}

	protected override void OnClosing(CancelEventArgs e)
	{
		if (ViewModel?.IsRunning == true)
		{
			e.Cancel = true;
			return;
		}

		base.OnClosing(e);
	}
}
