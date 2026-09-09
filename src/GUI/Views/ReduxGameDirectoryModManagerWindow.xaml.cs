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
	public string ManagementNote => CanAdopt
		? "Redux recognizes this exact reviewed DLL. Manage it without changing the installed file."
		: IsExternalReplacement
			? "This mod already replaced BG3 files, so Redux has no trusted originals to restore.\nRemove it, verify BG3's files in Steam or GOG, then install it through Redux."
			: Status == ReduxGameDirectoryModStatus.External
				? "Redux cannot manage this installation because its DLL does not match a reviewed version. Remove it manually before installing a reviewed archive."
				: Status is ReduxGameDirectoryModStatus.Changed or ReduxGameDirectoryModStatus.Missing
					? "Redux owns this installation, but its managed DLL changed or is missing. Reinstall the reviewed release to repair it."
					: StatusText;
	public bool HasSource => !String.IsNullOrWhiteSpace(SourceUrl);
}

public partial class ReduxGameDirectoryModManagerWindow : AdonisUI.Controls.AdonisWindow
{
	private const long ScriptExtenderNexusModId = 2172;
	private readonly MainWindowViewModel _viewModel;
	private readonly ReduxGameDirectoryInstallService _installer;
	private readonly Dictionary<long, NexusModsModData> _sourceDetails = new();
	private readonly CancellationTokenSource _sourceDetailsCancellation = new();

	public ReduxGameDirectoryModManagerWindow(MainWindow owner, MainWindowViewModel viewModel, bool focusScriptExtender = false)
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
		Loaded += async (_, _) =>
		{
			await LoadSourceDetailsAsync();
			if (focusScriptExtender)
			{
				ScriptExtenderButton.BringIntoView();
				ScriptExtenderButton.Focus();
			}
		};
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
			var existingEntry = installer.GetInstalledMods().FirstOrDefault(entry =>
				entry.Status != ReduxGameDirectoryModStatus.External
					&& String.Equals(entry.PackageId, inspection.Definition.PackageId, StringComparison.OrdinalIgnoreCase));
			var isUpdate = existingEntry != null;
			var isRepair = existingEntry?.Status is ReduxGameDirectoryModStatus.Changed or ReduxGameDirectoryModStatus.Missing;
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
					preserve ? "Keep existing settings" : replacesExisting ? "Replace file · protect original" : isRepair ? "Repair managed file" : isUpdate ? "Update managed file" : "Install managed file",
					isRepair ? ReduxInstallReviewTone.Warning : preserve || replacesExisting ? ReduxInstallReviewTone.Info
						: isUpdate ? ReduxInstallReviewTone.Success : ReduxInstallReviewTone.Info);
			}).ToList();
			reviewItems.AddRange(inspection.PackageEntries.Select(package => new ReduxInstallReviewItem(
				Path.GetFileName(package),
				"Companion PAK · installs through Redux's normal Mods-folder workflow",
				"Install as inactive mod",
				ReduxInstallReviewTone.Info)));

			var summary = $"{(isRepair ? "Managed repair" : isUpdate ? "Managed update" : "New managed install")} · {inspection.Definition.Name} · {inspection.LayoutName} · {inspection.FileCount} files · "
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
		// BG3's full build is in ProductVersion; FileVersion can be 1.0.0.0.
		var version = ParseNativeGameVersion(versionInfo.ProductVersion);
		return new ReduxGameDirectoryInstallService(
			Path.GetDirectoryName(Path.GetFullPath(executable))!,
			DivinityApp.GetAppDirectory("Data", "NativeInstalls"),
			version);
	}

	internal static Version ParseNativeGameVersion(string productVersion) =>
		Version.TryParse(productVersion?.Trim(), out var version) ? version : null;

	private void RefreshList()
	{
		try
		{
			var entries = _installer.GetInstalledMods().Select(CreateListItem).ToArray();
			var scriptExtender = entries.FirstOrDefault(item => item.Entry.NexusModId == ScriptExtenderNexusModId);
			UpdateScriptExtenderAction(scriptExtender,
				entries.Any(item => item.Status == ReduxGameDirectoryModStatus.RecoveryRequired));
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

	public static async Task InstallReviewedArchiveWithoutReviewAsync(
		MainWindowViewModel viewModel,
		string archivePath,
		NexusModManagerLink nexusSource = null)
	{
		var inspection = await Task.Run(() => ReduxGameDirectoryInstallService.TryInspectKnownArchive(archivePath))
			?? throw new InvalidDataException("This archive no longer matches a reviewed game-directory package.");
		var installer = CreateInstaller(viewModel);
		await using var transaction = await installer.StageAsync(inspection.Definition.NexusModId, archivePath);
		await transaction.CommitAsync();
		if (inspection.PackageEntries.Count > 0
			&& !await viewModel.ImportModsWithoutReviewAsync([archivePath], false, nexusSource))
			throw new InvalidDataException("The game-directory files installed, but the companion PAK could not be installed.");
	}

	public static async Task<ReduxNativeLoaderStatus> PreflightReviewedArchiveWithoutReviewAsync(
		MainWindowViewModel viewModel,
		long nexusModId,
		string archivePath)
	{
		var installer = CreateInstaller(viewModel);
		await installer.InspectArchiveAsync(nexusModId, archivePath);
		return installer.DetectLoader();
	}

	private async void ScriptExtenderButton_Click(object sender, RoutedEventArgs e)
	{
		if (String.IsNullOrWhiteSpace(_viewModel.PathwayData.ScriptExtenderLatestReleaseUrl))
		{
			ProcessHelper.TryOpenUrl(DivinityApp.EXTENDER_LATEST_URL);
			return;
		}

		string archivePath = null;
		ScriptExtenderButton.IsEnabled = false;
		try
		{
			archivePath = await _viewModel.DownloadScriptExtenderArchiveAsync();
			if (!String.IsNullOrWhiteSpace(archivePath)
				&& await ReviewAndInstallAsync(this, _viewModel, archivePath))
			{
				RefreshList();
			}
		}
		catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException or InvalidOperationException)
		{
			ReduxMessageBox.Show(this,
				$"Redux could not download Script Extender.\n\n{ex.Message}",
				"Script Extender Download Stopped", MessageBoxButton.OK, MessageBoxImage.Error, MessageBoxResult.OK);
		}
		finally
		{
			if (!String.IsNullOrWhiteSpace(archivePath) && File.Exists(archivePath))
			{
				try { File.Delete(archivePath); }
				catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
				{
					DivinityApp.Log($"Could not remove Script Extender intake file: {ex.Message}");
				}
			}
			RefreshList();
		}
	}

	private void UpdateScriptExtenderAction(ReduxGameDirectoryModListItem scriptExtender, bool recoveryRequired)
	{
		var hasRelease = !String.IsNullOrWhiteSpace(_viewModel.PathwayData.ScriptExtenderLatestReleaseUrl);
		var latestVersion = ParseLeadingVersion(_viewModel.PathwayData.ScriptExtenderLatestReleaseVersion);
		var installedVersion = ParseLeadingVersion(scriptExtender?.Entry.DetectedVersion);
		if (installedVersion < 0 && _viewModel.Settings.ExtenderUpdaterSettings.UpdaterVersion > 0)
			installedVersion = _viewModel.Settings.ExtenderUpdaterSettings.UpdaterVersion;

		if (recoveryRequired)
		{
			SetScriptExtenderAction("Resolve recovery first", false,
				"Finish the pending game-directory recovery before changing Script Extender.");
			return;
		}
		if (scriptExtender == null)
		{
			SetScriptExtenderAction("Install Script Extender", true,
				hasRelease ? "Download, review, and install the latest Script Extender release."
					: "Open the Script Extender releases page because Redux could not resolve the latest archive.");
			return;
		}
		if (scriptExtender.Status is ReduxGameDirectoryModStatus.Changed or ReduxGameDirectoryModStatus.Missing)
		{
			SetScriptExtenderAction("Reinstall Script Extender", hasRelease,
				hasRelease ? "Repair the changed or missing Redux-managed DLL with the latest reviewed release."
					: "Redux could not resolve the latest reviewed release. Try refreshing release information first.");
			return;
		}
		if (scriptExtender.Status == ReduxGameDirectoryModStatus.External)
		{
			if (!scriptExtender.CanAdopt)
			{
				SetScriptExtenderAction("Unverified Script Extender", false, scriptExtender.ManagementNote);
				return;
			}
			if (latestVersion < 0)
			{
				SetScriptExtenderAction("Manage Script Extender first", false,
					"Redux recognizes this installed version. Choose Manage with Redux while release information refreshes.");
				return;
			}
			if (latestVersion > installedVersion && installedVersion >= 0)
			{
				SetScriptExtenderAction("Manage before updating", false,
					"Choose Manage with Redux first. Redux can then update this older reviewed installation safely.");
				return;
			}
			SetScriptExtenderAction("Script Extender is up to date", false,
				$"Installed outside Redux{FormatVersion(scriptExtender.Entry.DetectedVersion)}. Choose Manage with Redux if you want Redux to own its removal and future updates.");
			return;
		}
		if (latestVersion >= 0 && installedVersion >= latestVersion)
		{
			SetScriptExtenderAction("Script Extender is up to date", false,
				$"Redux manages the current release{FormatVersion(scriptExtender.Entry.DetectedVersion)}.");
			return;
		}
		if (latestVersion > installedVersion && installedVersion >= 0)
		{
			SetScriptExtenderAction("Update Script Extender", hasRelease,
				$"Update the Redux-managed installation from v{installedVersion} to v{latestVersion}.");
			return;
		}

		SetScriptExtenderAction("Reinstall Script Extender", hasRelease,
			hasRelease ? "Redux could not confirm the installed version. Reinstall the latest reviewed release."
				: "Redux could not confirm the installed version or resolve the latest reviewed release.");
	}

	private void SetScriptExtenderAction(string text, bool enabled, string toolTip)
	{
		ScriptExtenderButtonText.Text = text;
		ScriptExtenderButton.IsEnabled = enabled;
		ScriptExtenderButton.ToolTip = toolTip;
	}

	private static int ParseLeadingVersion(string value)
	{
		if (String.IsNullOrWhiteSpace(value)) return -1;
		var digits = new string(value.TrimStart().TrimStart('v', 'V').TakeWhile(Char.IsDigit).ToArray());
		return Int32.TryParse(digits, out var version) ? version : -1;
	}

	private static string FormatVersion(string version) => String.IsNullOrWhiteSpace(version)
		? String.Empty : $" · v{version}";

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
