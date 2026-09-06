using DivinityModManager.Util;

using System.Windows;

namespace DivinityModManager.Views;

public enum ReduxInstallReviewTone
{
	Info,
	Success,
	Warning
}

public sealed record ReduxInstallReviewItem(
	string Name,
	string Detail,
	string Status,
	ReduxInstallReviewTone Tone);

public partial class ReduxInstallReviewWindow : AdonisUI.Controls.AdonisWindow
{
	public bool Accepted { get; private set; }

	public ReduxInstallReviewWindow(
		Window owner,
		IReadOnlyList<ReduxInstallReviewItem> items,
		bool isSaveInstall,
		string destination,
		string summaryDetail)
	{
		InitializeComponent();
		ReduxWindowBehavior.AttachDialogTransitions(this, 40);
		ReduxWindowBehavior.AttachRoundedCorners(this);
		if (owner?.IsLoaded == true) Owner = owner;

		var settings = MainWindow.Self?.ViewModel?.Settings;
		if (settings != null)
			ReduxThemeService.Apply(Resources, settings.ColorTheme,
				ReduxThemeService.GetActiveTheme(settings), settings.UsesGeneratedGradients);

		var noun = isSaveInstall ? "save" : "mod";
		var count = items?.Count ?? 0;
		Title = isSaveInstall ? "Install Saves" : "Install Mods";
		HeadingText.Text = isSaveInstall ? "Install dropped saves?" : "Install dropped mods?";
		DescriptionText.Text = isSaveInstall
			? "Review what Save Game Manager found before anything is copied into the selected profile."
			: $"Review what Redux found before anything is installed into {destination}.";
		SummaryIcon.IconKey = isSaveInstall ? "book-open" : "package";
		SummaryText.Text = $"{count} {noun}{(count == 1 ? String.Empty : "s")} ready for review";
		SummaryDetailText.Text = summaryDetail;
		ReviewList.ItemsSource = items ?? [];
		FootnoteText.Text = isSaveInstall
			? "Existing saves require a separate replacement confirmation."
			: "Packages are validated again during installation.";
		InstallButtonText.Text = isSaveInstall ? "Install Saves" : "Install Mods";
	}

	private void InstallButton_Click(object sender, RoutedEventArgs e)
	{
		Accepted = true;
		DialogResult = true;
	}

	private void CancelButton_Click(object sender, RoutedEventArgs e) => Close();
}
