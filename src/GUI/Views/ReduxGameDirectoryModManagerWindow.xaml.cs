using DivinityModManager.AppServices;
using DivinityModManager.Util;
using DivinityModManager.ViewModels;

using Microsoft.Win32;

using System.Diagnostics;
using System.Windows;

namespace DivinityModManager.Views;

public sealed record ReduxGameDirectoryModListItem(
	ReduxGameDirectoryModEntry Entry,
	string FileSummary,
	bool HasSource)
{
	public string Name => Entry.Name;
	public ReduxGameDirectoryModStatus Status => Entry.Status;
	public string StatusText => Entry.StatusText;
	public bool CanRestore => Entry.CanRestore;
}

public partial class ReduxGameDirectoryModManagerWindow : AdonisUI.Controls.AdonisWindow
{
	private readonly MainWindowViewModel _viewModel;
	private readonly ReduxGameDirectoryInstallService _installer;

	public ReduxGameDirectoryModManagerWindow(MainWindow owner, MainWindowViewModel viewModel)
	{
		InitializeComponent();
		Owner = owner;
		_viewModel = viewModel;
		ReduxWindowBehavior.AttachDialogTransitions(this, 40);
		ReduxWindowBehavior.AttachRoundedCorners(this);
		ReduxThemeService.Apply(Resources, viewModel.Settings.ColorTheme,
			ReduxThemeService.GetActiveTheme(viewModel.Settings), viewModel.Settings.UsesGeneratedGradients);
		_installer = CreateInstaller(viewModel);
		GamePathText.Text = _installer.GameBin;
		RefreshList();
	}

	public static bool CanOpen(MainWindowViewModel viewModel) =>
		viewModel?.Settings != null
		&& !String.IsNullOrWhiteSpace(viewModel.Settings.GameExecutablePath)
		&& File.Exists(Environment.ExpandEnvironmentVariables(viewModel.Settings.GameExecutablePath));

	public static async Task<bool> ReviewAndInstallAsync(
		Window owner,
		MainWindowViewModel viewModel,
		string archivePath)
	{
		ReduxGameDirectoryArchiveInspection inspection;
		try
		{
			inspection = await Task.Run(() => ReduxGameDirectoryInstallService.TryInspectKnownArchive(archivePath));
			if (inspection == null)
			{
				ReduxMessageBox.Show(owner,
					"This archive does not match a reviewed game-directory package. Redux did not change any files.",
					"Package Not Recognized", MessageBoxButton.OK, MessageBoxImage.Warning, MessageBoxResult.OK);
				return false;
			}
			if (!inspection.Definition.SupportsGuardedInstall)
			{
				ReduxMessageBox.Show(owner,
					$"{inspection.Definition.Name} is handled by Redux's existing Script Extender installer. This downloaded archive was not copied into the game directory.",
					"Use the Existing Redux Installer", MessageBoxButton.OK, MessageBoxImage.Information, MessageBoxResult.OK);
				return false;
			}
		}
		catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
		{
			ReduxMessageBox.Show(owner, ex.Message, "Could Not Inspect Archive",
				MessageBoxButton.OK, MessageBoxImage.Error, MessageBoxResult.OK);
			return false;
		}

		ReduxGameDirectoryInstallService installer;
		ReduxGameDirectoryInstallTransaction transaction;
		try
		{
			installer = CreateInstaller(viewModel);
			transaction = await installer.StageAsync(inspection.Definition.NexusModId, archivePath);
		}
		catch (Exception ex) when (ex is IOException or InvalidDataException or InvalidOperationException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
		{
			ReduxMessageBox.Show(owner, ex.Message, "Could Not Stage Game-Directory Changes",
				MessageBoxButton.OK, MessageBoxImage.Error, MessageBoxResult.OK);
			return false;
		}

		await using (transaction)
		{
			var isUpdate = installer.GetInstalledMods().Any(entry =>
				entry.Status == ReduxGameDirectoryModStatus.Managed
				&& String.Equals(entry.PackageId, inspection.Definition.PackageId, StringComparison.OrdinalIgnoreCase));
			var reviewItems = inspection.ManagedFiles.Select(file =>
			{
				var relative = file.DestinationPath.StartsWith("bin/", StringComparison.OrdinalIgnoreCase)
					? file.DestinationPath[4..] : file.DestinationPath;
				var destination = Path.Combine(installer.GameBin, relative.Replace('/', Path.DirectorySeparatorChar));
				var preserve = file.PreserveExisting && File.Exists(destination);
				return new ReduxInstallReviewItem(
					Path.GetFileName(file.DestinationPath),
					$"Destination: BG3\\bin\\{relative.Replace('/', '\\')}",
					preserve ? "Keep existing settings" : isUpdate ? "Update managed file" : "Install managed file",
					preserve ? ReduxInstallReviewTone.Info
						: isUpdate ? ReduxInstallReviewTone.Success : ReduxInstallReviewTone.Info);
			}).ToList();
			reviewItems.AddRange(inspection.PackageEntries.Select(package => new ReduxInstallReviewItem(
				Path.GetFileName(package),
				"Companion PAK · installs through Redux's normal Mods-folder workflow",
				"Install as inactive mod",
				ReduxInstallReviewTone.Info)));

			var summary = $"{(isUpdate ? "Managed update" : "New managed install")} · {inspection.Definition.Name} · {inspection.LayoutName} · {inspection.FileCount} files · "
				+ $"{FormatBytes(inspection.ExpandedBytes)} expanded";
			var dialog = new ReduxInstallReviewWindow(owner, reviewItems,
				ReduxInstallReviewKind.GameDirectory, installer.GameBin, summary);
			if (dialog.ShowDialog() != true && !dialog.Accepted) return false;

			try
			{
				await transaction.CommitAsync();
				if (inspection.PackageEntries.Count > 0)
					viewModel.ImportMods([archivePath], false);
				viewModel.ShowAlert($"Installed {inspection.Definition.Name}.", AlertType.Success, 20);
				return true;
			}
			catch (Exception ex) when (ex is IOException or InvalidDataException or InvalidOperationException or UnauthorizedAccessException)
			{
				var title = ex is ReduxGameDirectoryRecoveryException
					? "Recovery Required" : "Game-Directory Install Stopped";
				ReduxMessageBox.Show(owner, ex.Message, title,
					MessageBoxButton.OK, MessageBoxImage.Error, MessageBoxResult.OK);
				return false;
			}
		}
	}

	private static ReduxGameDirectoryInstallService CreateInstaller(MainWindowViewModel viewModel)
	{
		var executable = Environment.ExpandEnvironmentVariables(viewModel.Settings.GameExecutablePath);
		if (!File.Exists(executable))
			throw new FileNotFoundException("Configure a valid Baldur's Gate 3 executable before managing game-directory mods.", executable);
		var versionInfo = FileVersionInfo.GetVersionInfo(executable);
		var version = new Version(versionInfo.FileMajorPart, versionInfo.FileMinorPart,
			versionInfo.FileBuildPart, versionInfo.FilePrivatePart);
		return new ReduxGameDirectoryInstallService(
			Path.GetDirectoryName(Path.GetFullPath(executable))!,
			DivinityApp.GetAppDirectory("Data", "GameDirectoryInstalls"),
			version);
	}

	private void RefreshList()
	{
		try
		{
			var entries = _installer.GetInstalledMods().Select(entry => new ReduxGameDirectoryModListItem(
				entry,
				entry.Files.Count == 0 ? "Open Recovery Files for details"
					: String.Join(" · ", new[]
					{
						String.IsNullOrWhiteSpace(entry.ArchiveName) ? null : entry.ArchiveName,
						String.Join(", ", entry.Files.Select(path => Path.GetFileName(path)))
					}.Where(value => !String.IsNullOrWhiteSpace(value))),
				Uri.TryCreate(entry.SourceUrl, UriKind.Absolute, out _))).ToArray();
			InstalledList.ItemsSource = entries;
			InstalledList.Visibility = entries.Length == 0 ? Visibility.Collapsed : Visibility.Visible;
			EmptyText.Visibility = entries.Length == 0 ? Visibility.Visible : Visibility.Collapsed;
		}
		catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException)
		{
			InstalledList.ItemsSource = null;
			InstalledList.Visibility = Visibility.Collapsed;
			EmptyText.Visibility = Visibility.Visible;
			EmptyText.Text = $"Redux could not safely read game-directory install state.\n{ex.Message}";
		}
	}

	private async void InstallButton_Click(object sender, RoutedEventArgs e)
	{
		var dialog = new OpenFileDialog
		{
			Title = "Choose a Reviewed Game-Directory Mod Archive",
			Filter = "Supported archives (*.zip;*.7z;*.7zip;*.rar)|*.zip;*.7z;*.7zip;*.rar|All files (*.*)|*.*",
			Multiselect = false,
			CheckFileExists = true
		};
		if (dialog.ShowDialog(this) != true) return;
		if (await ReviewAndInstallAsync(this, _viewModel, dialog.FileName)) RefreshList();
	}

	private async void Window_Drop(object sender, DragEventArgs e)
	{
		if (!e.Data.GetDataPresent(DataFormats.FileDrop)
			|| e.Data.GetData(DataFormats.FileDrop) is not string[] { Length: 1 } paths) return;
		e.Handled = true;
		if (await ReviewAndInstallAsync(this, _viewModel, paths[0])) RefreshList();
	}

	private async void DeleteButton_Click(object sender, RoutedEventArgs e)
	{
		if (sender is not FrameworkElement { DataContext: ReduxGameDirectoryModListItem item } || !item.CanRestore) return;
		var answer = ReduxMessageBox.Show(this,
			$"Delete Redux's managed installation of {item.Name}? Files Redux added will be removed, and any files it replaced will be restored. Redux will only continue if every managed file still matches its installation record.",
			"Delete Game-Directory Mod", MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No);
		if (answer != MessageBoxResult.Yes) return;
		try
		{
			await _installer.RestoreAsync(item.Entry.NexusModId);
			_viewModel.ShowAlert($"Removed {item.Name} and restored any files it replaced.", AlertType.Success, 20);
			RefreshList();
		}
		catch (Exception ex) when (ex is IOException or InvalidDataException or InvalidOperationException or UnauthorizedAccessException)
		{
			ReduxMessageBox.Show(this, ex.Message, "Delete Stopped",
				MessageBoxButton.OK, MessageBoxImage.Error, MessageBoxResult.OK);
		}
	}

	private void SourceButton_Click(object sender, RoutedEventArgs e)
	{
		if (sender is FrameworkElement { DataContext: ReduxGameDirectoryModListItem item } && item.HasSource)
			ProcessHelper.TryOpenUrl(item.Entry.SourceUrl);
	}

	private void OpenGameFolderButton_Click(object sender, RoutedEventArgs e) =>
		ProcessHelper.TryOpenPath(_installer.GameBin, Directory.Exists);

	private void RefreshButton_Click(object sender, RoutedEventArgs e) => RefreshList();

	private static string FormatBytes(long bytes) => bytes >= 1024 * 1024
		? $"{bytes / (1024d * 1024d):0.#} MB"
		: $"{Math.Max(1, bytes / 1024d):0.#} KB";
}
