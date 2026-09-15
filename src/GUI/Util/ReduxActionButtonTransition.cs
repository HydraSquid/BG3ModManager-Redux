using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace DivinityModManager.Util;

/// <summary>Shared semantic action availability transition, retaining live theme resources.</summary>
public static class ReduxActionButtonTransition
{
    private sealed class State { public string Key; public int Version; }
    private static readonly ConditionalWeakTable<Button, State> States = new();

    public static void Apply(Button button, bool enabled, string background, string border, string foreground, bool reduceMotion)
    {
        var state = States.GetOrCreateValue(button);
        button.ApplyTemplate();
        var chrome = button.Template?.FindName("ButtonChrome", button) as FrameworkElement;
        var wasEnabled = button.IsEnabled;
        var previousOpacity = chrome?.Opacity ?? (wasEnabled ? 1 : 0.45);
        button.IsEnabled = enabled;
        if (chrome != null && wasEnabled != enabled)
        {
            chrome.BeginAnimation(UIElement.OpacityProperty, null);
            if (!reduceMotion && !ReduxWindowBehavior.ReduceMotion)
                chrome.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(previousOpacity, enabled ? 1 : 0.45, TimeSpan.FromMilliseconds(180)) { FillBehavior = FillBehavior.Stop });
        }
        var key = $"{enabled}|{background}|{border}|{foreground}";
        if (state.Key == key) return;
        state.Key = key;
        var version = ++state.Version;
        foreach (var pair in new[] { (Control.BackgroundProperty, background), (Control.BorderBrushProperty, border), (Control.ForegroundProperty, foreground) })
        {
            var old = button.GetValue(pair.Item1) as SolidColorBrush;
            var target = button.TryFindResource(pair.Item2) as SolidColorBrush;
            if (old == null || target == null || reduceMotion || ReduxWindowBehavior.ReduceMotion)
            { button.SetResourceReference(pair.Item1, pair.Item2); continue; }
            var brush = new SolidColorBrush(old.Color);
            button.SetValue(pair.Item1, brush);
            var animation = new ColorAnimation(old.Color, target.Color, TimeSpan.FromMilliseconds(180));
            animation.Completed += (_, _) => { if (version == state.Version) button.SetResourceReference(pair.Item1, pair.Item2); };
            brush.BeginAnimation(SolidColorBrush.ColorProperty, animation);
        }
    }
}
