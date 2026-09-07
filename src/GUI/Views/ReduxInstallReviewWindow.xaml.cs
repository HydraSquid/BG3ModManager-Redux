using DivinityModManager.Util;

using System.Windows;

namespace DivinityModManager.Views;

public enum ReduxInstallReviewTone
{
	Info,
	Success,
	Warning
}

public enum ReduxInstallReviewKind
{
	Mods,
	Saves,
	GameDirectory
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
		: this(owner, items, isSaveInstall ? ReduxInstallReviewKind.Saves : ReduxInstallReviewKind.Mods,
			destination, summaryDetail)
	{
	}

	public ReduxInstallReviewWindow(
		Window owner,
		IReadOnlyList<ReduxInstallReviewItem> items,
		ReduxInstallReviewKind kind,
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

		var isSaveInstall = kind == ReduxInstallReviewKind.Saves;
		var isGameDirectoryInstall = kind == ReduxInstallReviewKind.GameDirectory;
		var noun = isSaveInstall ? "save" : isGameDirectoryInstall ? "change" : "mod";
		var count = items?.Count ?? 0;
		Title = isSaveInstall ? "Install Saves" : isGameDirectoryInstall ? "Review Game-Directory Changes" : "Install Mods";
		HeadingText.Text = isSaveInstall ? "Install dropped saves?"
			: isGameDirectoryInstall ? "Apply these game-directory changes?" : "Install dropped mods?";
		DescriptionText.Text = isSaveInstall
			? "Review what Save Game Manager found before anything is copied into the selected profile."
			: isGameDirectoryInstall
				? "Review every destination carefully. Native DLLs run inside Baldur's Gate 3 and should only be installed from sources you trust."
				: $"Review what Redux found before anything is installed into {destination}.";
		SummaryIcon.IconKey = isSaveInstall ? "book-open" : isGameDirectoryInstall ? "blocks" : "package";
		SummaryText.Text = $"{count} {noun}{(count == 1 ? String.Empty : "s")} ready for review";
		SummaryDetailText.Text = summaryDetail;
		ReviewList.ItemsSource = items ?? [];
		FootnoteText.Text = isSaveInstall
			? "Existing saves require a separate replacement confirmation."
			: isGameDirectoryInstall
				? "Redux rechecks the archive, prerequisites, destinations, and ownership record immediately before applying anything."
				: "Packages are validated again during installation.";
		InstallButtonText.Text = isSaveInstall ? "Install Saves" : isGameDirectoryInstall ? "Apply Changes" : "Install Mods";
	}

	private void InstallButton_Click(object sender, RoutedEventArgs e)
	{
		Accepted = true;
		DialogResult = true;
	}

	private void CancelButton_Click(object sender, RoutedEventArgs e) => Close();
}
