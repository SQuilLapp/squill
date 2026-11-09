using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Squill.Platform.Settings;
using Squill.Presentation.Localization;

namespace Squill.App.WinUI.Views;

/// <summary>The one-time question about how often SQuilL may contact GitHub for updates.</summary>
internal static class UpdateAskDialog
{
    /// <summary>Shows the question over a window.</summary>
    /// <param name="root">The content of the window that owns the dialog.</param>
    /// <returns>The frequency the user picked, or <see langword="null"/> when the dialog was closed without a choice (the question is asked again next start).</returns>
    public static async Task<UpdateCheckFrequency?> ShowAsync(FrameworkElement root)
    {
        UpdateCheckFrequency? chosen = null;
        var dialog = new ContentDialog { XamlRoot = root.XamlRoot, Title = UiText.Get("update.ask.title"), RequestedTheme = root.ActualTheme };
        var panel = new StackPanel { Spacing = 8, MinWidth = 320 };
        panel.Children.Add(new TextBlock { Text = UiText.Get("update.ask.body"), TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 8) });
        foreach (var (frequency, key) in new[]
        {
            (UpdateCheckFrequency.Daily, "update.ask.daily"), (UpdateCheckFrequency.Weekly, "update.ask.weekly"),
            (UpdateCheckFrequency.Startup, "update.ask.startup"), (UpdateCheckFrequency.Never, "update.ask.never"),
        })
        {
            var button = new Button { Content = UiText.Get(key), HorizontalAlignment = HorizontalAlignment.Stretch };
            button.Click += (_, _) =>
            {
                chosen = frequency;
                dialog.Hide();
            };
            panel.Children.Add(button);
        }

        dialog.Content = panel;
        await dialog.ShowAsync();
        return chosen;
    }
}
