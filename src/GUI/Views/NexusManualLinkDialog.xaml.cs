using AdonisUI.Controls;

using DivinityModManager.Util;

using System.Windows;

namespace DivinityModManager.Views;

public partial class NexusManualLinkDialog : AdonisWindow
{
	public string ModPageLink => ModPageLinkTextBox.Text?.Trim();

	public NexusManualLinkDialog(string currentLink = null)
	{
		InitializeComponent();
		ReduxWindowBehavior.AttachDialogTransitions(this, 40);
		ModPageLinkTextBox.Text = currentLink ?? String.Empty;
		Loaded += (_, _) =>
		{
			ModPageLinkTextBox.Focus();
			ModPageLinkTextBox.SelectAll();
		};
	}

	private void Link_Click(object sender, RoutedEventArgs e)
	{
		if (!String.IsNullOrWhiteSpace(ModPageLink)) DialogResult = true;
	}
}
