using DivinityModManager.AppServices;
using DivinityModManager.Util;
using DivinityModManager.ViewModels;

using System.Windows;
using System.Windows.Controls;

namespace DivinityModManager.Views;

public sealed record ReduxLoadOrderAdvisorMoveItem(
	string Name,
	int PreviousPosition,
	int NextPosition,
	string Reason,
	string IgnoreKey)
{
	public bool CanIgnore => !String.IsNullOrWhiteSpace(IgnoreKey);
}

public sealed record ReduxLoadOrderAdvisorUnresolvedItem(
	string Relationship,
	string Reason,
	string IgnoreKey);

public sealed record ReduxLoadOrderAdvisorSeparatorItem(
	string Name,
	string PlacementSummary,
	string CountSummary,
	string Color,
	string IconId)
{
	public bool HasIcon => !String.IsNullOrWhiteSpace(IconId);
}

public partial class ReduxLoadOrderAdvisorOrganizerWindow : AdonisUI.Controls.AdonisWindow
{
	private readonly MainWindowViewModel _viewModel;
	private LoadOrderAdvisorPlan _plan;
	private LoadOrderAdvisorSeparatorPolicy _selectedPolicy = LoadOrderAdvisorSeparatorPolicy.PreserveMySeparators;
	private bool _initializing;

	public ReduxLoadOrderAdvisorOrganizerWindow(Window owner, MainWindowViewModel viewModel)
	{
		InitializeComponent();
		ReduxWindowBehavior.AttachDialogTransitions(this, 40);
		ReduxWindowBehavior.AttachRoundedCorners(this);
		if (owner?.IsLoaded == true) Owner = owner;
		_viewModel = viewModel;
		if (viewModel?.Settings != null)
			ReduxThemeService.Apply(Resources, viewModel.Settings.ColorTheme,
				ReduxThemeService.GetActiveTheme(viewModel.Settings), viewModel.Settings.UsesGeneratedGradients);

		_initializing = true;
		PreservePolicyButton.IsChecked = true;
		_initializing = false;
		RefreshPlan();
	}

	private void PolicyButton_Checked(object sender, RoutedEventArgs e)
	{
		if (_initializing) return;
		if (sender == SuggestedPolicyButton)
			_selectedPolicy = LoadOrderAdvisorSeparatorPolicy.CreateSuggestedSeparators;
		else if (sender == RemovePolicyButton)
			_selectedPolicy = LoadOrderAdvisorSeparatorPolicy.RemoveSeparators;
		else
			_selectedPolicy = LoadOrderAdvisorSeparatorPolicy.PreserveMySeparators;
		RefreshPlan();
		if (_selectedPolicy == LoadOrderAdvisorSeparatorPolicy.CreateSuggestedSeparators)
		{
			PreviewTabs.SelectedItem = ResultingSeparatorsTab;
		}
	}

	private void RefreshPlan()
	{
		if (_viewModel == null) return;
		_plan = LoadOrderAdvisorOrganizer.CreatePlan(
			_viewModel.ActiveMods,
			_viewModel.Settings.VisualModListDividers,
			_selectedPolicy,
			ignoredFindingKeys: _viewModel.Settings.IgnoredLoadOrderAdvisorFindingKeys);
		if (_selectedPolicy == LoadOrderAdvisorSeparatorPolicy.CreateSuggestedSeparators)
		{
			foreach (var divider in _plan.Dividers)
			{
				divider.Color = _viewModel.GetCurrentCategoryColor(divider.Title);
				divider.IconId = _viewModel.GetCurrentCategoryIcon(divider.Title);
			}
		}
		var moves = _plan.Moves.Select(move => new ReduxLoadOrderAdvisorMoveItem(
			move.Name,
			move.PreviousPosition,
			move.NextPosition,
			move.Reason,
			move.IgnoreKey)).ToArray();
		var unresolved = _plan.UnresolvedRelationships.Select(item =>
			new ReduxLoadOrderAdvisorUnresolvedItem(
				$"{item.BeforeName} before {item.AfterName}", item.Reason, item.IgnoreKey)).ToArray();
		var positions = _plan.OrderedMods
			.Select((mod, index) => (mod, index))
			.Where(item => !String.IsNullOrWhiteSpace(item.mod.UUID))
			.GroupBy(item => item.mod.UUID, StringComparer.OrdinalIgnoreCase)
			.ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);
		var separators = _plan.Dividers.Select(divider =>
		{
			var members = (divider.MemberModUuids ?? [])
				.Where(positions.ContainsKey)
				.Select(uuid => positions[uuid])
				.OrderBy(item => item.index)
				.ToArray();
			if (members.Length == 0)
				return new ReduxLoadOrderAdvisorSeparatorItem(
					divider.Title, "Empty separator", "0 mods", divider.Color, divider.IconId);
			var first = members[0];
			var last = members[^1];
			var placement = first.index == last.index
				? $"Placed above #{first.index + 1} {first.mod.DisplayName}"
				: $"Placed above #{first.index + 1} {first.mod.DisplayName} · through #{last.index + 1} {last.mod.DisplayName}";
			return new ReduxLoadOrderAdvisorSeparatorItem(
				divider.Title,
				placement,
				$"{members.Length} mod{(members.Length == 1 ? String.Empty : "s")}",
				divider.Color,
				divider.IconId);
		}).ToArray();
		MoveList.ItemsSource = moves;
		UnresolvedList.ItemsSource = unresolved;
		SeparatorList.ItemsSource = separators;
		MoveCountText.Text = moves.Length.ToString();
		SeparatorCountText.Text = _plan.Dividers.Count.ToString();
		UnresolvedCountText.Text = unresolved.Length.ToString();
		NoMovesText.Visibility = moves.Length == 0 ? Visibility.Visible : Visibility.Collapsed;
		NoUnresolvedText.Visibility = unresolved.Length == 0 ? Visibility.Visible : Visibility.Collapsed;
		NoSeparatorsText.Visibility = separators.Length == 0 ? Visibility.Visible : Visibility.Collapsed;
		RestoreIgnoredButton.Visibility = (_viewModel.Settings.IgnoredLoadOrderAdvisorFindingKeys?.Count ?? 0) > 0
			? Visibility.Visible
			: Visibility.Collapsed;
		ApplyButton.IsEnabled = _plan.HasChanges;
	}

	private void IgnoreAdviceButton_Click(object sender, RoutedEventArgs e)
	{
		if (sender is Button { CommandParameter: string ignoreKey }
			&& _viewModel.IgnoreLoadOrderAdvisorFinding(ignoreKey))
			RefreshPlan();
	}

	private void RestoreIgnoredButton_Click(object sender, RoutedEventArgs e)
	{
		if (_viewModel.RestoreIgnoredLoadOrderAdvisorFindings() > 0) RefreshPlan();
	}

	private void ApplyButton_Click(object sender, RoutedEventArgs e)
	{
		if (!_viewModel.ApplyLoadOrderAdvisorPlan(_plan)) return;
		DialogResult = true;
		Close();
	}
}
