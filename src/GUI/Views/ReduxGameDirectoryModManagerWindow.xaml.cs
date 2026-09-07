using DivinityModManager.AppServices;
using DivinityModManager.Models;
using DivinityModManager.Models.NexusMods;
using DivinityModManager.Util;
using DivinityModManager.ViewModels;

using Microsoft.Win32;

using System.Diagnostics;
using System.Windows;

namespace DivinityModManager.Views;

public sealed record ReduxGameDirectoryModListItem(
	ReduxGameDirectoryModEntry Entry,
	string FileSummary,
	string SourceUrl,
	string Summary,
	string DetailsText,
	string ThumbnailUrl)
{
	public string Name { get; init; } = Entry.Name;
	public ReduxGameDirectoryModStatus Status => Entry.Status;
	public string StatusText => Entry.StatusText;
	public bool CanRestore => Entry.CanRestore;
	public bool CanAdopt => Entry.CanAdopt;
	public bool IsAdopted => Entry.ArchiveName == "Adopted external installation";
	public bool IsExternalReplacement => Entry.Status == ReduxGameDirectoryModStatus.External
		&& ReduxGameDirectoryModCatalog.Find(Entry.NexusModId)?.ReplacesExistingGameFiles == true;
	public string ManagementNote => IsExternalReplacement
		? "This mod already replaced BG3 files, so Redux has no trusted originals to restore.\nRemove it, verify BG3's files in Steam or GOG, then install it through Redux."
		: String.Empty;
	public bool HasSource => !String.IsNullOrWhiteSpace(SourceUrl);
}

public partial class ReduxGameDirectoryModManagerWindow : AdonisUI.Controls.AdonisWindow
{
	private readonly MainWindowViewModel _viewModel;
	private readonly ReduxGameDirectoryInstallService _installer;
	private readonly Dictionary<long, NexusModsModData> _sourceDetails = new();
	private readonly CancellationTokenSource _sourceDetailsCancellation = new();

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
		Loaded += async (_, _) => await LoadSourceDetailsAsync();
		Closed += (_, _) => _sourceDetailsCancellation.Cancel();
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
				var replacesExisting = inspection.Definition.ReplacesExistingGameFiles && File.Exists(destination) && !preserve;
				return new ReduxInstallReviewItem(
					Path.GetFileName(file.DestinationPath),
					$"Destination: BG3\\bin\\{relative.Replace('/', '\\')}",
					preserve ? "Keep existing settings" : replacesExisting ? "Replace file · protect original" : isUpdate ? "Update managed file" : "Install managed file",
					preserve || replacesExisting ? ReduxInstallReviewTone.Info
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
			var entries = _installer.GetInstalledMods().Select(CreateListItem).ToArray();
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

	private ReduxGameDirectoryModListItem CreateListItem(ReduxGameDirectoryModEntry entry)
	{
		var definition = ReduxGameDirectoryModCatalog.Find(entry.NexusModId);
		var metadata = ResolveSourceDetails(entry.NexusModId);
		var sourceUrl = !String.IsNullOrWhiteSpace(entry.SourceUrl) ? entry.SourceUrl : definition?.SourceUrl ?? String.Empty;
		var summary = !String.IsNullOrWhiteSpace(metadata?.Summary) ? metadata.Summary.Trim()
			: definition?.Requirements ?? (entry.Status == ReduxGameDirectoryModStatus.RecoveryRequired
				? "Redux found an interrupted game-directory operation that needs attention."
				: "Native files detected in the game directory.");
		var kind = definition?.Kind switch
		{
			ReduxGameDirectoryModKind.NativeLoader => "Native loader",
			ReduxGameDirectoryModKind.NativePlugin => "Native plugin",
			ReduxGameDirectoryModKind.ScriptExtender => "Script Extender",
			_ => "Game-directory files"
		};
		var creator = !String.IsNullOrWhiteSpace(metadata?.Author) ? metadata.Author : metadata?.UploadedBy;
		var displayVersion = !String.IsNullOrWhiteSpace(entry.DetectedVersion)
			? entry.DetectedVersion : metadata?.Version;
		var details = String.Join(" · ", new[]
		{
			kind,
			String.IsNullOrWhiteSpace(sourceUrl) ? null : "Nexus Mods",
			String.IsNullOrWhiteSpace(creator) ? null : $"by {creator}",
			String.IsNullOrWhiteSpace(displayVersion) ? null : $"v{displayVersion}",
			metadata?.UpdatedAt is DateTime updated ? $"Updated {updated:g}" : null
		}.Where(value => !String.IsNullOrWhiteSpace(value)));
		var files = entry.Files.Count == 0 ? "No installed-file details are available"
			: String.Join(" · ", new[]
			{
				String.IsNullOrWhiteSpace(entry.ArchiveName) ? null : entry.ArchiveName,
				String.Join(", ", entry.Files.Select(path => Path.GetFileName(path)))
			}.Where(value => !String.IsNullOrWhiteSpace(value)));
		return new ReduxGameDirectoryModListItem(entry, files, sourceUrl, summary, details,
			metadata?.PreviewImageUrl ?? String.Empty)
		{
			Name = !String.IsNullOrWhiteSpace(metadata?.Name) ? metadata.Name.Trim() : entry.Name
		};
	}

	private NexusModsModData ResolveSourceDetails(long nexusModId)
	{
		if (nexusModId < DivinityApp.NEXUSMODS_MOD_ID_START) return null;
		if (_sourceDetails.TryGetValue(nexusModId, out var loaded)) return loaded;
		return _viewModel.UpdateHandler.Nexus.CacheData.Mods.Values
			.Where(metadata => metadata?.ModId == nexusModId)
			.OrderByDescending(metadata => metadata.IsUpdated)
			.FirstOrDefault();
	}

	private async Task LoadSourceDetailsAsync()
	{
		if (!_viewModel.Modules.SourceIntegrationsEnabled || !_viewModel.UpdateHandler.Nexus.IsEnabled
			|| !NexusModsDataLoader.CanFetchData) return;
		var projectIds = _installer.GetInstalledMods()
			.Select(entry => entry.NexusModId)
			.Where(id => id >= DivinityApp.NEXUSMODS_MOD_ID_START && ResolveSourceDetails(id) == null)
			.Distinct().ToArray();
		if (projectIds.Length == 0) return;

		var probes = projectIds.Select(id =>
		{
			var mod = new DivinityModData { UUID = $"redux-game-directory-{id}" };
			mod.NexusModsData.SetModVersion(id);
			return mod;
		}).ToArray();
		var result = await NexusModsDataLoader.LoadAllModsDataAsync(probes, _sourceDetailsCancellation.Token);
		if (_sourceDetailsCancellation.IsCancellationRequested || !IsLoaded) return;
		foreach (var mod in result.UpdatedMods.Where(mod => mod?.NexusModsData?.ModId >= DivinityApp.NEXUSMODS_MOD_ID_START))
			_sourceDetails[mod.NexusModsData.ModId] = mod.NexusModsData;
		if (_sourceDetails.Count > 0) RefreshList();
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
		var detail = item.IsAdopted
			? "Redux will remove only the adopted DLL files, and only if each still matches the version you adopted. Settings files and companion content remain untouched."
			: "Files Redux added will be removed, and any files it replaced will be restored. Redux will only continue if every managed file still matches its installation record.";
		var answer = ReduxMessageBox.Show(this,
			$"Delete Redux's managed installation of {item.Name}? {detail}",
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

	private async void AdoptButton_Click(object sender, RoutedEventArgs e)
	{
		if (sender is not FrameworkElement { DataContext: ReduxGameDirectoryModListItem item } || !item.CanAdopt) return;
		var answer = ReduxMessageBox.Show(this,
			$"Manage the installed {item.Name} DLL with Redux?\n\nRedux verified this exact binary against its reviewed catalog. No game files will change now. If you delete it later, Redux will remove only unchanged adopted DLLs; settings files and companion PAKs remain yours.",
			"Manage Existing Installation", MessageBoxButton.YesNo, MessageBoxImage.Information, MessageBoxResult.No);
		if (answer != MessageBoxResult.Yes) return;
		try
		{
			await _installer.AdoptExternalAsync(item.Entry.NexusModId);
			_viewModel.ShowAlert($"Redux now manages {item.Name}.", AlertType.Success, 20);
			RefreshList();
		}
		catch (Exception ex) when (ex is IOException or InvalidDataException or InvalidOperationException or UnauthorizedAccessException)
		{
			ReduxMessageBox.Show(this, ex.Message, "Could Not Manage Installation",
				MessageBoxButton.OK, MessageBoxImage.Error, MessageBoxResult.OK);
		}
	}

	private void SourceButton_Click(object sender, RoutedEventArgs e)
	{
		if (sender is FrameworkElement { DataContext: ReduxGameDirectoryModListItem item } && item.HasSource)
			ProcessHelper.TryOpenUrl(item.SourceUrl);
	}

	private void OpenGameFolderButton_Click(object sender, RoutedEventArgs e) =>
		ProcessHelper.TryOpenPath(_installer.GameBin, Directory.Exists);

	private void RefreshButton_Click(object sender, RoutedEventArgs e) => RefreshList();

	private static string FormatBytes(long bytes) => bytes >= 1024 * 1024
		? $"{bytes / (1024d * 1024d):0.#} MB"
		: $"{Math.Max(1, bytes / 1024d):0.#} KB";
}
