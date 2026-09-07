using DivinityModManager.AppServices;
using DivinityModManager.Util;
using DivinityModManager.ViewModels;

using Microsoft.VisualBasic.FileIO;

using Ookii.Dialogs.Wpf;

using System.Windows.Automation;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace DivinityModManager.Views;

public sealed record ReduxSaveGameItem(Bg3SaveGameEntry Save)
{
	public string FolderPath => Save.FolderPath;
	public string FolderName => Save.FolderName;
	public string DisplayName => Save.DisplayName;
	public string CampaignName => String.IsNullOrWhiteSpace(Save.CampaignName) ? "Story save" : Save.CampaignName;
	public string CampaignGroupName => GetCampaignDisplayName(Save.CampaignName);
	public string ThumbnailPath => Save.ThumbnailPath;
	public Bg3SaveDifficulty Difficulty => Save.Difficulty;
	public bool HasDifficulty => Difficulty != Bg3SaveDifficulty.Unknown;
	public bool IsHonourMode => Difficulty == Bg3SaveDifficulty.Honour;
	public bool IsTacticianMode => Difficulty == Bg3SaveDifficulty.Tactician;
	public string DifficultyLabel => Difficulty switch
	{
		Bg3SaveDifficulty.Explorer => "Explorer",
		Bg3SaveDifficulty.Balanced => "Balanced",
		Bg3SaveDifficulty.Tactician => "Tactician",
		Bg3SaveDifficulty.Honour => "Honour",
		Bg3SaveDifficulty.Custom => "Custom",
		_ => String.Empty
	};
	public string ModifiedText => Save.ModifiedUtc.ToLocalTime().ToString("g");
	public string SizeText => FormatSize(Save.SizeBytes);

	private static string FormatSize(long size) => size >= 1024L * 1024L
		? $"{size / (1024d * 1024d):0.0} MB"
		: $"{Math.Max(1, size / 1024d):0} KB";

	private static string GetCampaignDisplayName(string campaignName)
	{
		if (String.IsNullOrWhiteSpace(campaignName)) return "Other saves";
		var suffix = campaignName.LastIndexOf('-');
		var displayName = suffix > 0 && campaignName[(suffix + 1)..].All(Char.IsDigit)
			? campaignName[..suffix]
			: campaignName;
		return displayName.Replace('_', ' ');
	}
}

public partial class ReduxSaveManagerWindow : AdonisUI.Controls.AdonisWindow
{
	private readonly MainWindowViewModel _viewModel;
	private readonly string _storyFolder;
	private bool _isImporting;
	private readonly Dictionary<FrameworkElement, int> _campaignAnimationVersions = new();
	private readonly HashSet<Expander> _restoringCampaignExpanders = new();
	private readonly HashSet<Expander> _campaignExpanders = new();
	private string[] _campaignKeys = [];
	private bool _isBulkCampaignUpdate;

	public ReduxSaveManagerWindow(Window owner, MainWindowViewModel viewModel, IEnumerable<string> pendingImportPaths = null)
	{
		InitializeComponent();
		ReduxWindowBehavior.AttachDialogTransitions(this, 40);
		ReduxWindowBehavior.AttachRoundedCorners(this);
		if (owner?.IsLoaded == true) Owner = owner;
		_viewModel = viewModel;
		_storyFolder = viewModel?.SelectedProfile?.Folder == null
			? null
			: Path.Combine(viewModel.SelectedProfile.Folder, "Savegames", "Story");
		if (viewModel?.Settings != null)
			ReduxThemeService.Apply(Resources, viewModel.Settings.ColorTheme,
				ReduxThemeService.GetActiveTheme(viewModel.Settings), viewModel.Settings.UsesGeneratedGradients);
		ProfilePathText.Text = _storyFolder ?? "No player profile is selected.";
		RefreshSaves();
		var pendingPaths = (pendingImportPaths ?? []).Where(path => !String.IsNullOrWhiteSpace(path)).ToArray();
		if (pendingPaths.Length > 0)
			Loaded += async (_, _) =>
			{
				foreach (var path in pendingPaths) await ImportPathAsync(path);
			};
	}

	private void RefreshSaves()
	{
		var selectedPath = (SaveList.SelectedItem as ReduxSaveGameItem)?.FolderPath;
		var saves = Bg3SaveGameService.Discover(_storyFolder).Select(save => new ReduxSaveGameItem(save)).ToArray();
		var groupedSaves = new ListCollectionView(saves);
		groupedSaves.GroupDescriptions.Add(new PropertyGroupDescription(nameof(ReduxSaveGameItem.CampaignGroupName)));
		_campaignKeys = saves.Select(save => save.CampaignGroupName)
			.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
		SaveList.ItemsSource = groupedSaves;
		if (!String.IsNullOrWhiteSpace(selectedPath))
			SaveList.SelectedItem = saves.FirstOrDefault(save => save.FolderPath.Equals(selectedPath, StringComparison.OrdinalIgnoreCase));
		EmptyState.Visibility = saves.Length == 0 ? Visibility.Visible : Visibility.Collapsed;
		DeleteButton.IsEnabled = SaveList.SelectedItem != null;
		UpdateCampaignBulkToggleButton();
	}

	private async void ImportArchiveButton_Click(object sender, RoutedEventArgs e)
	{
		var selectedPath = ChooseSaveFile(this);
		if (!String.IsNullOrWhiteSpace(selectedPath)) await ImportPathAsync(selectedPath);
	}

	private async void ImportFolderButton_Click(object sender, RoutedEventArgs e)
	{
		var selectedPath = ChooseSaveFolder(this);
		if (!String.IsNullOrWhiteSpace(selectedPath)) await ImportPathAsync(selectedPath);
	}

	public static string ChooseSaveFile(Window owner)
	{
		var dialog = new Microsoft.Win32.OpenFileDialog
		{
			Title = "Install BG3 Save",
			Filter = "BG3 saves and archives (*.lsv;*.zip;*.7z;*.rar;*.tar;*.gz)|*.lsv;*.zip;*.7z;*.7zip;*.rar;*.tar;*.tar.gz;*.tgz;*.gz;*.gzip|BG3 saves (*.lsv)|*.lsv|Archives (*.zip;*.7z;*.rar;*.tar;*.gz)|*.zip;*.7z;*.7zip;*.rar;*.tar;*.tar.gz;*.tgz;*.gz;*.gzip",
			CheckFileExists = true,
			Multiselect = false
		};
		return dialog.ShowDialog(owner) == true ? dialog.FileName : null;
	}

	public static string ChooseSaveFolder(Window owner)
	{
		var dialog = new VistaFolderBrowserDialog
		{
			Description = "Choose the folder containing the BG3 .lsv save.",
			UseDescriptionForTitle = true
		};
		return dialog.ShowDialog(owner) == true ? dialog.SelectedPath : null;
	}

	private async Task ImportPathAsync(string sourcePath)
	{
		if (_isImporting) return;
		if (String.IsNullOrWhiteSpace(_storyFolder))
		{
			ShowMessage("Select a BG3 player profile before installing saves.", "Save Games", MessageBoxImage.Warning);
			return;
		}
		try
		{
			var names = Bg3SaveGameService.GetImportFolderNames(sourcePath);
			if (names.Count == 0) throw new InvalidDataException("No BG3 .lsv save was found.");
			var collisions = names.Where(name => Directory.Exists(Path.Combine(_storyFolder, name))).ToArray();
			var replace = false;
			if (collisions.Length > 0)
			{
				var result = ReduxMessageBox.Show(this,
					$"{collisions.Length} save{(collisions.Length == 1 ? "" : "s")} already exist. Replace the existing save folders?",
					"Replace Existing Saves?", MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No);
				if (result != MessageBoxResult.Yes) return;
				replace = true;
			}
			_isImporting = true;
			Cursor = Cursors.Wait;
			SaveList.IsEnabled = false;
			DeleteButton.IsEnabled = false;
			var imported = await Task.Run(() => Bg3SaveGameService.Import(sourcePath, _storyFolder, replace));
			RefreshSaves();
			_viewModel?.ShowAlert($"Installed {imported.Count} save{(imported.Count == 1 ? "" : "s")}.", AlertType.Success, 12);
		}
		catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException
			or InvalidOperationException or SharpCompress.Common.InvalidFormatException)
		{
			ShowMessage(ex.Message, "Install Save", MessageBoxImage.Error);
		}
		finally
		{
			_isImporting = false;
			Cursor = null;
			SaveList.IsEnabled = true;
			DeleteButton.IsEnabled = SaveList.SelectedItem != null;
		}
	}

	private void DeleteButton_Click(object sender, RoutedEventArgs e)
	{
		if (_isImporting) return;
		if (SaveList.SelectedItem is not ReduxSaveGameItem selected) return;
		var result = ReduxMessageBox.Show(this,
			$"Move '{selected.DisplayName}' to the Recycle Bin?",
			"Delete Save?", MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No);
		if (result != MessageBoxResult.Yes) return;
		try
		{
			FileSystem.DeleteDirectory(selected.FolderPath, UIOption.OnlyErrorDialogs, RecycleOption.SendToRecycleBin);
			RefreshSaves();
		}
		catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or OperationCanceledException)
		{
			ShowMessage(ex.Message, "Delete Save", MessageBoxImage.Error);
		}
	}

	private void OpenFolderButton_Click(object sender, RoutedEventArgs e)
	{
		if (String.IsNullOrWhiteSpace(_storyFolder)) return;

		try
		{
			Directory.CreateDirectory(_storyFolder);
			ProcessHelper.TryOpenPath(_storyFolder, Directory.Exists);
		}
		catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
		{
			ShowMessage(ex.Message, "Open Save Folder", MessageBoxImage.Error);
		}
	}

	private void RefreshButton_Click(object sender, RoutedEventArgs e) => RefreshSaves();
	private void SaveList_SelectionChanged(object sender, SelectionChangedEventArgs e) => DeleteButton.IsEnabled = SaveList.SelectedItem != null;

	private void CampaignExpander_Loaded(object sender, RoutedEventArgs e)
	{
		if (sender is not Expander expander) return;
		_campaignExpanders.Add(expander);
		var key = GetCampaignKey(expander);
		var collapsed = !String.IsNullOrWhiteSpace(key)
			&& (_viewModel?.Settings?.CollapsedSaveGameCampaigns?.Contains(key, StringComparer.OrdinalIgnoreCase) ?? false);
		_restoringCampaignExpanders.Add(expander);
		try { expander.IsExpanded = !collapsed; }
		finally { _restoringCampaignExpanders.Remove(expander); }
		if (expander.Template.FindName("ExpandSite", expander) is FrameworkElement content)
		{
			content.BeginAnimation(HeightProperty, null);
			content.BeginAnimation(OpacityProperty, null);
			var translate = EnsureWritableCampaignTransform(content);
			translate.BeginAnimation(TranslateTransform.YProperty, null);
			translate.Y = 0;
			content.Height = Double.NaN;
			content.Opacity = 1;
			content.Visibility = collapsed ? Visibility.Collapsed : Visibility.Visible;
		}
		UpdateCampaignBulkToggleButton();
	}

	private void CampaignExpander_Unloaded(object sender, RoutedEventArgs e)
	{
		if (sender is not Expander expander) return;
		_campaignExpanders.Remove(expander);
		if (expander.Template.FindName("ExpandSite", expander) is FrameworkElement content)
			_campaignAnimationVersions.Remove(content);
	}

	private async void CampaignExpander_Expanded(object sender, RoutedEventArgs e)
	{
		if (sender is not Expander expander || expander.Template.FindName("ExpandSite", expander) is not FrameworkElement content) return;
		RememberCampaignState(expander, collapsed: false);
		if (_restoringCampaignExpanders.Contains(expander))
		{
			content.Visibility = Visibility.Visible;
			return;
		}
		var version = NextCampaignAnimationVersion(content);
		content.BeginAnimation(HeightProperty, null);
		content.BeginAnimation(OpacityProperty, null);
		var translate = EnsureWritableCampaignTransform(content);
		translate.BeginAnimation(TranslateTransform.YProperty, null);
		translate.Y = 0;
		content.Visibility = Visibility.Visible;
		content.Opacity = 1;
		content.Height = Double.NaN;
		if (ShouldReduceCampaignMotion())
		{
			UpdateCampaignBulkToggleButton();
			return;
		}

		await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.Loaded);
		if (!expander.IsExpanded || !IsCurrentCampaignAnimation(content, version)) return;
		content.Measure(new Size(Math.Max(1, expander.ActualWidth), Double.PositiveInfinity));
		var targetHeight = Math.Max(content.ActualHeight, content.DesiredSize.Height);
		if (targetHeight <= 0) return;
		content.Height = 0;
		content.Opacity = 0;
		translate.Y = -5;
		var duration = TimeSpan.FromMilliseconds(165);
		var easing = new QuadraticEase { EasingMode = EasingMode.EaseOut };
		var height = new DoubleAnimation(targetHeight, duration) { EasingFunction = easing };
		height.Completed += (_, _) =>
		{
			if (!expander.IsExpanded || !IsCurrentCampaignAnimation(content, version)) return;
			content.BeginAnimation(HeightProperty, null);
			content.Height = Double.NaN;
		};
		content.BeginAnimation(HeightProperty, height);
		content.BeginAnimation(OpacityProperty, new DoubleAnimation(1, TimeSpan.FromMilliseconds(125)));
		translate.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(0, duration) { EasingFunction = easing });
		UpdateCampaignBulkToggleButton();
	}

	private void CampaignExpander_Collapsed(object sender, RoutedEventArgs e)
	{
		if (sender is not Expander expander || expander.Template.FindName("ExpandSite", expander) is not FrameworkElement content) return;
		RememberCampaignState(expander, collapsed: true);
		if (_restoringCampaignExpanders.Contains(expander))
		{
			content.Visibility = Visibility.Collapsed;
			return;
		}
		var version = NextCampaignAnimationVersion(content);
		if (ShouldReduceCampaignMotion())
		{
			var translate = EnsureWritableCampaignTransform(content);
			translate.BeginAnimation(TranslateTransform.YProperty, null);
			translate.Y = 0;
			content.Visibility = Visibility.Collapsed;
			content.Height = Double.NaN;
			content.Opacity = 1;
			UpdateCampaignBulkToggleButton();
			return;
		}

		content.BeginAnimation(HeightProperty, null);
		content.Height = Math.Max(0, content.ActualHeight);
		var duration = TimeSpan.FromMilliseconds(145);
		var easing = new QuadraticEase { EasingMode = EasingMode.EaseIn };
		var height = new DoubleAnimation(0, duration) { EasingFunction = easing };
		height.Completed += (_, _) =>
		{
			if (expander.IsExpanded || !IsCurrentCampaignAnimation(content, version)) return;
			content.Visibility = Visibility.Collapsed;
			content.BeginAnimation(HeightProperty, null);
			content.BeginAnimation(OpacityProperty, null);
			content.Height = Double.NaN;
			content.Opacity = 1;
		};
		content.BeginAnimation(HeightProperty, height);
		content.BeginAnimation(OpacityProperty, new DoubleAnimation(0, TimeSpan.FromMilliseconds(110)));
		var translateTransform = EnsureWritableCampaignTransform(content);
		translateTransform.BeginAnimation(TranslateTransform.YProperty,
			new DoubleAnimation(-4, duration) { EasingFunction = easing });
		UpdateCampaignBulkToggleButton();
	}

	private static TranslateTransform EnsureWritableCampaignTransform(FrameworkElement content)
	{
		if (content.RenderTransform is TranslateTransform { IsFrozen: false } writable) return writable;
		var replacement = content.RenderTransform is TranslateTransform transform
			? transform.CloneCurrentValue()
			: new TranslateTransform();
		content.RenderTransform = replacement;
		return replacement;
	}

	private bool ShouldReduceCampaignMotion() =>
		_viewModel?.Settings?.ReduceMotion == true
		|| ReduxWindowBehavior.ReduceMotion
		|| !SystemParameters.ClientAreaAnimation;

	private void CampaignBulkToggleButton_MouseEnter(object sender, MouseEventArgs e) =>
		UpdateCampaignBulkToggleButton();

	private void CampaignBulkToggleButton_Click(object sender, RoutedEventArgs e)
	{
		if (_campaignKeys.Length < 2 || _viewModel?.Settings == null) return;
		var collapse = !AreAllCampaignsCollapsed();
		var saved = _viewModel.Settings.CollapsedSaveGameCampaigns ??= [];
		if (collapse)
		{
			foreach (var key in _campaignKeys)
				if (!saved.Contains(key, StringComparer.OrdinalIgnoreCase)) saved.Add(key);
		}
		else
		{
			saved.RemoveAll(savedKey => _campaignKeys.Contains(savedKey, StringComparer.OrdinalIgnoreCase));
		}

		_isBulkCampaignUpdate = true;
		try
		{
			foreach (var expander in _campaignExpanders.Where(expander => expander.IsLoaded).ToArray())
				expander.IsExpanded = !collapse;
		}
		finally
		{
			_isBulkCampaignUpdate = false;
		}
		_viewModel.SaveSettings();
		UpdateCampaignBulkToggleButton();
	}

	private bool AreAllCampaignsCollapsed()
	{
		var saved = _viewModel?.Settings?.CollapsedSaveGameCampaigns;
		return _campaignKeys.Length > 0 && saved != null
			&& _campaignKeys.All(key => saved.Contains(key, StringComparer.OrdinalIgnoreCase));
	}

	private void UpdateCampaignBulkToggleButton()
	{
		if (CampaignBulkToggleButton == null) return;
		var available = _campaignKeys.Length >= 2;
		CampaignBulkToggleButton.Visibility = available ? Visibility.Visible : Visibility.Collapsed;
		CampaignBulkToggleButton.IsEnabled = available;
		if (!available) return;
		var expand = AreAllCampaignsCollapsed();
		var label = expand ? "Expand all campaigns" : "Collapse all campaigns";
		CampaignBulkToggleButton.ToolTip = label;
		AutomationProperties.SetName(CampaignBulkToggleButton, label);
		CampaignBulkToggleIcon.StrokeData = FindResource(expand
			? "Redux.Icon.ChevronDownStroke" : "Redux.Icon.ChevronUpStroke") as Geometry;
	}

	private int NextCampaignAnimationVersion(FrameworkElement content)
	{
		var version = _campaignAnimationVersions.TryGetValue(content, out var current) ? current + 1 : 1;
		_campaignAnimationVersions[content] = version;
		return version;
	}

	private bool IsCurrentCampaignAnimation(FrameworkElement content, int version) =>
		_campaignAnimationVersions.TryGetValue(content, out var current) && current == version;

	private static string GetCampaignKey(Expander expander) =>
		(expander?.DataContext as CollectionViewGroup)?.Name?.ToString()?.Trim() ?? String.Empty;

	private void RememberCampaignState(Expander expander, bool collapsed)
	{
		if (_isBulkCampaignUpdate || _restoringCampaignExpanders.Contains(expander) || _viewModel?.Settings == null) return;
		var key = GetCampaignKey(expander);
		if (String.IsNullOrWhiteSpace(key)) return;
		var saved = _viewModel.Settings.CollapsedSaveGameCampaigns ??= [];
		var existing = saved.FindIndex(name => name.Equals(key, StringComparison.OrdinalIgnoreCase));
		if (collapsed && existing < 0) saved.Add(key);
		else if (!collapsed && existing >= 0) saved.RemoveAt(existing);
		else return;
		_viewModel.SaveSettings();
		UpdateCampaignBulkToggleButton();
	}

	private void Window_DragOver(object sender, DragEventArgs e)
	{
		e.Effects = TryGetDroppedPaths(e.Data, out _) ? DragDropEffects.Copy : DragDropEffects.None;
		e.Handled = true;
	}

	private async void Window_Drop(object sender, DragEventArgs e)
	{
		if (TryGetDroppedPaths(e.Data, out var paths))
		{
			try
			{
				var names = paths.SelectMany(Bg3SaveGameService.GetImportFolderNames).ToArray();
				if (names.Length > 0 && ConfirmDroppedSaveInstall(this, names, _storyFolder))
					foreach (var path in paths) await ImportPathAsync(path);
			}
			catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException
				or InvalidOperationException or SharpCompress.Common.InvalidFormatException)
			{
				ShowMessage(ex.Message, "Install Save", MessageBoxImage.Error);
			}
		}
		e.Handled = true;
	}

	private static bool TryGetDroppedPaths(IDataObject data, out string[] paths)
	{
		paths = [];
		if (!data.GetDataPresent(DataFormats.FileDrop) || data.GetData(DataFormats.FileDrop) is not string[] droppedPaths
			|| droppedPaths.Length == 0 || droppedPaths.Any(path => !Bg3SaveGameService.IsSupportedSaveInput(path))) return false;
		paths = droppedPaths;
		return true;
	}

	public static bool ConfirmDroppedSaveInstall(Window owner, IReadOnlyList<string> saveFolderNames, string storyFolder = null)
	{
		var names = (saveFolderNames ?? []).Where(name => !String.IsNullOrWhiteSpace(name)).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
		if (names.Length == 0) return false;
		var items = names.Select(name =>
		{
			var split = name.IndexOf("__", StringComparison.Ordinal);
			var campaign = split > 0 ? name[..split] : "Story save";
			var saveName = split >= 0 && split + 2 < name.Length ? name[(split + 2)..] : name;
			var exists = !String.IsNullOrWhiteSpace(storyFolder) && Directory.Exists(Path.Combine(storyFolder, name));
			return new ReduxInstallReviewItem(
				saveName.Replace('_', ' '),
				$"Campaign: {campaign.Replace('_', ' ')}",
				exists ? "Already installed" : "New save",
				exists ? ReduxInstallReviewTone.Warning : ReduxInstallReviewTone.Info);
		}).ToArray();
		var existingCount = items.Count(item => item.Tone == ReduxInstallReviewTone.Warning);
		var detail = existingCount == 0
			? "All saves are new to the selected profile"
			: $"{items.Length - existingCount} new · {existingCount} already installed";
		var dialog = new ReduxInstallReviewWindow(owner, items, true, "Save Game Manager", detail);
		return dialog.ShowDialog() == true || dialog.Accepted;
	}

	private void ShowMessage(string message, string caption, MessageBoxImage image) =>
		ReduxMessageBox.Show(this, message, caption, MessageBoxButton.OK, image, MessageBoxResult.OK);
}
