using Avalonia.Controls;
using Avalonia.Interactivity;
using Squill.Core;
using Squill.Platform.Settings;
using Squill.Presentation.Settings;

namespace Squill.App.Avalonia.Views;

/// <summary>The preferences window; every edit is applied and saved immediately by the view-model.</summary>
public sealed partial class SettingsWindow : Window
{
    /// <summary>Creates the window.</summary>
    public SettingsWindow()
    {
        InitializeComponent();
        KeywordCaseBox.ItemsSource = Enum.GetValues<KeywordCase>();
    }

    private void OnThemeChecked(object? sender, RoutedEventArgs e)
    {
        if (DataContext is SettingsViewModel settings && sender is RadioButton { IsChecked: true, Tag: ThemeMode mode })
            settings.Theme = mode;
    }
}
