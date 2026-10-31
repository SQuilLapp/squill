using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Squill.Platform.Settings;
using Squill.Presentation.Settings;
using Squill.Core;

namespace Squill.App.WinUI.Views;

/// <summary>The settings editor: every change is applied to <see cref="SettingsViewModel"/> immediately.</summary>
public sealed partial class SettingsDialog : ContentDialog
{
    private static readonly ThemeMode[] Themes = [ThemeMode.System, ThemeMode.Light, ThemeMode.Dark];

    /// <summary>Creates the dialog bound to the settings.</summary>
    /// <param name="settings">The settings to show and edit.</param>
    /// <param name="root">The XAML root hosting the dialog.</param>
    /// <param name="theme">The theme the dialog is shown in.</param>
    public SettingsDialog(SettingsViewModel settings, XamlRoot root, ElementTheme theme)
    {
        XamlRoot = root;
        RequestedTheme = theme;
        Title = "Settings";
        CloseButtonText = "Close";
        var themes = new RadioButtons { Header = "Theme", Items = { "System", "Light", "Dark" }, SelectedIndex = Array.IndexOf(Themes, settings.Theme) };
        themes.SelectionChanged += (_, _) =>
        {
            if (themes.SelectedIndex >= 0)
                settings.Theme = Themes[themes.SelectedIndex];
        };
        var fontSize = Number("Editor font size", 8, 32, 1, settings.EditorFontSize, value => settings.EditorFontSize = value);
        var maxRows = Number("Maximum grid rows", 1000, 10_000_000, 1000, settings.MaxGridRows, value => settings.MaxGridRows = (int)value);
        var keywordCase = new ComboBox { Header = "Formatter keyword case", HorizontalAlignment = HorizontalAlignment.Stretch };
        foreach (var value in Enum.GetValues<KeywordCase>())
            keywordCase.Items.Add(value);
        keywordCase.SelectedItem = settings.FormatterKeywordCase;
        keywordCase.SelectionChanged += (_, _) =>
        {
            if (keywordCase.SelectedItem is KeywordCase chosen)
                settings.FormatterKeywordCase = chosen;
        };
        Content = new StackPanel { Spacing = 12, MinWidth = 320, Children = { themes, fontSize, maxRows, keywordCase } };
    }

    private static NumberBox Number(string header, double min, double max, double step, double value, Action<double> apply)
    {
        var box = new NumberBox
        {
            Header = header,
            Minimum = min,
            Maximum = max,
            SmallChange = step,
            Value = value,
            SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Inline,
        };
        box.ValueChanged += (_, e) =>
        {
            if (!double.IsNaN(e.NewValue))
                apply(e.NewValue);
        };
        return box;
    }
}
