using AdonisUI.Controls;

using DivinityModManager.Util;

using System.Windows;

namespace DivinityModManager.Views;

public partial class ModioManualLinkDialog : AdonisWindow
{
	public string ModioLink => ModioLinkTextBox.Text?.Trim();

	public ModioManualLinkDialog(string currentLink = null)
	{
		InitializeComponent();
		ReduxWindowBehavior.AttachDialogTransitions(this, 40);
		ModioLinkTextBox.Text = currentLink ?? String.Empty;
		Loaded += (_, _) =>
		{
			ModioLinkTextBox.Focus();
			ModioLinkTextBox.SelectAll();
		};
	}

	private void Link_Click(object sender, RoutedEventArgs e)
	{
		if (!String.IsNullOrWhiteSpace(ModioLink)) DialogResult = true;
	}
}
