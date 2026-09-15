using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Threading;

namespace DivinityModManager.Util;

/// <summary>Shared feedback for external file drops on manager windows.</summary>
public static class ReduxExternalDropFeedback
{
    public static void Attach(Window window, Func<string[], bool> accepts, string caption, string iconKey, string detail)
    {
        if (window.Content is not Grid root) return;
        var overlay = new Grid { IsHitTestVisible = false, Visibility = Visibility.Collapsed };
        Grid.SetRowSpan(overlay, Math.Max(1, root.RowDefinitions.Count));
        Grid.SetColumnSpan(overlay, Math.Max(1, root.ColumnDefinitions.Count));
        Panel.SetZIndex(overlay, 2000);
        var scrim = new Border { Opacity = .62 };
        scrim.SetResourceReference(Border.BackgroundProperty, "ReduxAppBackgroundBrush");
        var outline = new Border { Margin = new Thickness(2), BorderThickness = new Thickness(2), CornerRadius = new CornerRadius(6) };
        outline.SetResourceReference(Border.BorderBrushProperty, "ReduxAccentHoverBrush");
        var card = new Border { Padding = new Thickness(28, 22, 28, 22), Margin = new Thickness(24),
            HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center,
            BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(12), MaxWidth = 440 };
        card.SetResourceReference(Border.BackgroundProperty, "ReduxSurfaceElevatedBrush");
        card.SetResourceReference(Border.BorderBrushProperty, "ReduxAccentHoverBrush");
        var content = new StackPanel();
        var badge = new Border { Width = 52, Height = 52, CornerRadius = new CornerRadius(26), HorizontalAlignment = HorizontalAlignment.Center };
        badge.SetResourceReference(Border.BackgroundProperty, "ReduxAccentPillBackground");
        var icon = new DivinityModManager.Controls.ReduxIcon { Width = 22, Height = 22 };
        icon.SetResourceReference(DivinityModManager.Controls.ReduxIcon.StrokeDataProperty, iconKey);
        icon.SetResourceReference(Control.ForegroundProperty, "ReduxAccentHoverBrush");
        icon.SetResourceReference(FrameworkElement.StyleProperty, "ReduxOptionalInterfaceIconStyle");
        badge.Child = icon;
        content.Children.Add(badge);
        var label = new TextBlock { Text = caption, TextWrapping = TextWrapping.Wrap, TextAlignment = TextAlignment.Center,
            Margin = new Thickness(0, 12, 0, 0), FontWeight = FontWeights.SemiBold };
        label.SetResourceReference(TextBlock.ForegroundProperty, "ReduxTextPrimaryBrush");
        label.SetResourceReference(TextBlock.FontSizeProperty, "Redux.FontSize.20");
        content.Children.Add(label);
        var subtitle = new TextBlock { Text = detail, TextWrapping = TextWrapping.Wrap, TextAlignment = TextAlignment.Center, Margin = new Thickness(0, 5, 0, 0) };
        subtitle.SetResourceReference(TextBlock.ForegroundProperty, "ReduxTextSecondaryBrush");
        content.Children.Add(subtitle);
        card.Child = content;
        overlay.Children.Add(scrim);
        overlay.Children.Add(outline);
        overlay.Children.Add(card);
        root.Children.Add(overlay);
        var lastDrag = DateTime.MinValue;
        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(120) };
        var backgrounds = new System.Collections.Generic.Dictionary<UIElement, (Effect Effect, double Opacity)>();
        void RestoreBackground()
        {
            foreach (var entry in backgrounds)
            {
                entry.Key.Effect = entry.Value.Effect;
                entry.Key.Opacity = entry.Value.Opacity;
            }
            backgrounds.Clear();
        }
        void Hide()
        {
            overlay.Visibility = Visibility.Collapsed;
            RestoreBackground();
            timer.Stop();
        }
        timer.Tick += (_, _) => { if (DateTime.UtcNow - lastDrag > TimeSpan.FromMilliseconds(240)) Hide(); };
        void Over(object sender, DragEventArgs e)
        {
            if (!e.Data.GetDataPresent(DataFormats.FileDrop)) return;
            var allowed = !ReduxWindowBehavior.HasActiveChild(window)
                && e.Data.GetData(DataFormats.FileDrop) is string[] paths && paths.Length > 0 && accepts(paths);
            e.Effects = allowed ? DragDropEffects.Copy : DragDropEffects.None;
            e.Handled = true;
            if (!allowed) { Hide(); return; }
            scrim.Visibility = ReduxWindowBehavior.BackgroundEffectsDisabled ? Visibility.Collapsed : Visibility.Visible;
            if (ReduxWindowBehavior.BackgroundEffectsDisabled) RestoreBackground();
            else if (backgrounds.Count == 0)
            {
                // Blur the content, keeping the drop border and caption sharp.
                foreach (UIElement child in root.Children)
                {
                    if (ReferenceEquals(child, overlay)) continue;
                    backgrounds.Add(child, (child.Effect, child.Opacity));
                    child.Effect = new BlurEffect { Radius = 2.5, RenderingBias = RenderingBias.Performance };
                    child.Opacity *= .88;
                }
            }
            if (overlay.Visibility != Visibility.Visible)
            {
                overlay.Visibility = Visibility.Visible;
                if (!ReduxWindowBehavior.ReduceMotion)
                    overlay.BeginAnimation(UIElement.OpacityProperty, new System.Windows.Media.Animation.DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(130)));
            }
            lastDrag = DateTime.UtcNow;
            timer.Start();
        }
        window.PreviewDragEnter += Over;
        window.PreviewDragOver += Over;
        window.AddHandler(DragDrop.PreviewDropEvent, new DragEventHandler((_, _) => Hide()), true);
        window.Closed += (_, _) => Hide();
        window.Deactivated += (_, _) => Hide();
    }
}
