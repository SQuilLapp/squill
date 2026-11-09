using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using Avalonia.Interactivity;
using Squill.Core;
using Squill.Platform.Settings;
using Squill.Presentation.Settings;
using Squill.Presentation.Updates;

namespace Squill.App.Avalonia.Views;

/// <summary>The preferences window; every edit is applied and saved immediately by the view-model.</summary>
public sealed partial class SettingsWindow : Window
{
    private string? _dataDirectory;
    private UpdateViewModel? _updates;

    /// <summary>Creates the window.</summary>
    public SettingsWindow()
    {
        InitializeComponent();
        KeywordCaseBox.ItemsSource = Enum.GetValues<KeywordCase>();
    }

    /// <summary>Gets or sets the data directory shown on the Advanced page and opened by its button.</summary>
    public string? DataDirectory
    {
        get => _dataDirectory;
        set
        {
            _dataDirectory = value;
            DataFolderPath.Text = value;
            OpenDataFolderButton.IsEnabled = value is not null;
        }
    }

    /// <summary>Gets or sets the update flow behind the status line and the check button.</summary>
    public UpdateViewModel? Updates
    {
        get => _updates;
        set
        {
            if (_updates is { } old)
                old.PropertyChanged -= OnUpdatesChanged;
            _updates = value;
            if (value is not null)
                value.PropertyChanged += OnUpdatesChanged;
            RefreshUpdateStatus();
        }
    }

    /// <inheritdoc />
    protected override void OnClosed(EventArgs e)
    {
        Updates = null;
        base.OnClosed(e);
    }

    private void OnUpdatesChanged(object? sender, PropertyChangedEventArgs e) => RefreshUpdateStatus();

    private void RefreshUpdateStatus()
    {
        UpdateStatusText.Text = _updates?.StatusText;
        CheckNowButton.IsEnabled = _updates?.CanCheck ?? false;
    }

    private void OnCheckNow(object? sender, RoutedEventArgs e) => _updates?.CheckNowCommand.Execute(null);

    private void OnBack(object? sender, RoutedEventArgs e) => Close();

    private void OnNavChecked(object? sender, RoutedEventArgs e)
    {
        if (sender is not RadioButton { IsChecked: true } nav)
            return;
        PageAppearance.IsVisible = nav == NavAppearance;
        PageEditor.IsVisible = nav == NavEditor;
        PageGrid.IsVisible = nav == NavGrid;
        PageAdvanced.IsVisible = nav == NavAdvanced;
    }

    private void OnThemeChecked(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not SettingsViewModel settings || sender is not RadioButton { IsChecked: true } card)
            return;
        settings.Theme = card == ThemeLight ? ThemeMode.Light : card == ThemeDark ? ThemeMode.Dark : ThemeMode.System;
    }

    private async void OnOpenDataFolder(object? sender, RoutedEventArgs e)
    {
        if (_dataDirectory is null)
            return;
        Directory.CreateDirectory(_dataDirectory);
        await Launcher.LaunchDirectoryInfoAsync(new DirectoryInfo(_dataDirectory));
    }
}
