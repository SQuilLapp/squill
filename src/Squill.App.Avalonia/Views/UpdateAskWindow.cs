using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Squill.Platform.Settings;
using Squill.Presentation.Localization;

namespace Squill.App.Avalonia.Views;

/// <summary>The one-time question about how often SQuilL may contact GitHub for updates.</summary>
internal sealed class UpdateAskWindow : Window
{
    /// <summary>Creates the window; it closes with the chosen frequency, or with <see langword="null"/> when closed without a choice.</summary>
    public UpdateAskWindow()
    {
        Title = UiText.Get("update.ask.title");
        Width = 400;
        SizeToContent = SizeToContent.Height;
        CanResize = false;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        var panel = new StackPanel { Spacing = 8, Margin = new Thickness(24) };
        panel.Children.Add(new TextBlock { Text = UiText.Get("update.ask.body"), TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 8) });
        foreach (var (frequency, key) in new[]
        {
            (UpdateCheckFrequency.Daily, "update.ask.daily"), (UpdateCheckFrequency.Weekly, "update.ask.weekly"),
            (UpdateCheckFrequency.Startup, "update.ask.startup"), (UpdateCheckFrequency.Never, "update.ask.never"),
        })
        {
            var button = new Button { Content = UiText.Get(key), HorizontalAlignment = HorizontalAlignment.Stretch };
            button.Click += (_, _) => Close(frequency);
            panel.Children.Add(button);
        }

        Content = panel;
        if (this.TryFindResource("SquillBgBaseBrush", ActualThemeVariant, out var brush) && brush is IBrush background)
            Background = background;
    }

    /// <summary>Shows the question over a window.</summary>
    /// <param name="owner">The window that owns the dialog.</param>
    /// <returns>The chosen frequency, or <see langword="null"/> when the dialog was closed without a choice (the question is asked again next start).</returns>
    public static Task<UpdateCheckFrequency?> ShowAsync(Window owner) => new UpdateAskWindow().ShowDialog<UpdateCheckFrequency?>(owner);
}
