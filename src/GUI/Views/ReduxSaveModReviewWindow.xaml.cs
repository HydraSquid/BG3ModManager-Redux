using DivinityModManager.AppServices;
using DivinityModManager.Models;
using DivinityModManager.Util;
using System.ComponentModel;
using System.Windows;

namespace DivinityModManager.Views;

public sealed class SaveModReviewChoice : INotifyPropertyChanged
{
    public SaveModRequirement Requirement { get; }
    public string DisplayName { get; }
    public string MetadataText { get; }
    public string IdentityText => $"Recorded as: {Requirement.Name}\nUUID: {Requirement.UUID}";
    public bool NeedsAttention => Requirement.Status != SaveModStatus.Active;
    private bool _selected;
    public bool Selected
    {
        get => _selected;
        set { var allowed = value && Requirement.CanActivate; if (_selected == allowed) return; _selected = allowed; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Selected))); }
    }
    public event PropertyChangedEventHandler PropertyChanged;
    public SaveModReviewChoice(SaveModRequirement requirement)
    {
        Requirement = requirement; _selected = requirement.CanActivate;
        var project = ReduxModDatabaseService.TryResolveModuleUuid(requirement.UUID)?.Project;
        DisplayName = String.IsNullOrWhiteSpace(project?.Name) ? requirement.Name : project.Name;
        MetadataText = project == null ? "" : String.Join(" · ", new[] {
            "Nexus Mods", project.Authors?.Count > 0 ? "by " + String.Join(", ", project.Authors) : null,
            project.Categories?.Count > 0 ? String.Join(", ", project.Categories) : null
        }.Where(value => !String.IsNullOrWhiteSpace(value)));
    }
}

public partial class ReduxSaveModReviewWindow : AdonisUI.Controls.AdonisWindow
{
    private readonly SaveModReviewChoice[] _choices;
    public IReadOnlyList<string> SelectedIds { get; private set; } = Array.Empty<string>();
    public ReduxSaveModReviewWindow(Window owner, string saveName, IReadOnlyList<SaveModRequirement> requirements, DivinityModManagerSettings settings)
    {
        InitializeComponent();
        if (owner?.IsLoaded == true) Owner = owner;
        ReduxWindowBehavior.AttachDialogTransitions(this, 40);
        ReduxWindowBehavior.AttachRoundedCorners(this);
        if (settings != null) ReduxThemeService.Apply(Resources, settings.ColorTheme, ReduxThemeService.GetActiveTheme(settings), settings.UsesGeneratedGradients);
        SaveNameText.Text = saveName;
        _choices = requirements.Select(r => new SaveModReviewChoice(r)).ToArray();
        RequirementsGrid.ItemsSource = _choices;
        foreach (var choice in _choices) choice.PropertyChanged += (_, _) => RefreshSelection();
        RefreshSelection();
        Loaded += (_, _) => RequirementsGrid.Focus();
    }
    private void RefreshSelection()
    {
        ApplyButton.IsEnabled = _choices.Any(c => c.Selected);
        SummaryText.Text = _choices.Length == 0 ? "No non-built-in mods are recorded in this save." :
            $"{_choices.Count(c => c.Requirement.Status == SaveModStatus.Active)} active · {_choices.Count(c => c.Requirement.Status == SaveModStatus.Inactive)} inactive · {_choices.Count(c => c.Requirement.Status == SaveModStatus.Missing)} missing · {_choices.Count(c => c.Requirement.Status == SaveModStatus.Unavailable)} need attention · {_choices.Count(c => c.Selected)} selected";
    }
    private void Apply_Click(object sender, RoutedEventArgs e)
    {
        SelectedIds = SaveModReviewService.SelectedActivationIds(_choices.Select(c => c.Requirement), _choices.Where(c => c.Selected).Select(c => c.Requirement.UUID));
        if (SelectedIds.Count > 0) DialogResult = true;
    }
}
