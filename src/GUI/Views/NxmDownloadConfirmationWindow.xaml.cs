using DivinityModManager.Models.NexusMods;
using DivinityModManager.Util;
using DivinityModManager.ViewModels;

using System.Windows;

namespace DivinityModManager.Views;

public partial class NxmDownloadConfirmationWindow : AdonisUI.Controls.AdonisWindow
{
	private readonly NxmDownloadConfirmationViewModel _viewModel;
	public bool SkipFutureCleanConfirmations => SkipCleanConfirmationCheckBox.IsChecked == true;

	public NxmDownloadConfirmationWindow(Window owner, NxmDownloadDescriptor descriptor)
	{
		InitializeComponent();
		ReduxWindowBehavior.AttachDialogTransitions(this, 40);
		ReduxWindowBehavior.AttachRoundedCorners(this);
		if (owner?.IsLoaded == true) Owner = owner;
		var settings = MainWindow.Self?.ViewModel?.Settings;
		if (settings != null)
			ReduxThemeService.Apply(Resources, settings.ColorTheme, ReduxThemeService.GetActiveTheme(settings));
		_viewModel = new NxmDownloadConfirmationViewModel(descriptor);
		DataContext = _viewModel;
		Loaded += (_, _) => CancelButton.Focus();
	}

	private void NexusPage_Click(object sender, RoutedEventArgs e) => ProcessHelper.TryOpenUrl(_viewModel.NexusPage.ToString());

	private void Install_Click(object sender, RoutedEventArgs e)
	{
		DialogResult = true;
		Close();
	}
}
