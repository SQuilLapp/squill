using System.ComponentModel;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Squill.Core;
using Squill.Platform.Settings;
using Squill.Presentation.Localization;
using Squill.Presentation.Settings;
using Squill.Presentation.Updates;
using Windows.System;

namespace Squill.App.WinUI.Views;

/// <summary>Window, modal to the main window, that edits the preferences; every change is applied to <see cref="SettingsViewModel"/> at once.</summary>
public sealed partial class SettingsWindow : Window
{
    private const int LogicalWidth = 1100;
    private const int LogicalHeight = 720;

    private readonly Window _owner;
    private readonly SettingsViewModel _settings;
    private readonly string _dataDirectory;
    private readonly UpdateViewModel _updates;
    private bool _loading;

    /// <summary>Creates the window bound to the settings.</summary>
    /// <param name="owner">The main window; it stays disabled while this window is open.</param>
    /// <param name="settings">The settings to show and edit.</param>
    /// <param name="dataDirectory">The directory holding profiles, settings and history, shown on the Advanced page.</param>
    /// <param name="updates">The update flow behind the status line and the check button.</param>
    public SettingsWindow(Window owner, SettingsViewModel settings, string dataDirectory, UpdateViewModel updates)
    {
        InitializeComponent();
        _owner = owner;
        _settings = settings;
        _dataDirectory = dataDirectory;
        _updates = updates;
        CaptionBar.Follow(this, Root, ((FrameworkElement)owner.Content));
        Title = UiText.Get("settings.title");
        AppWindow.SetIcon(Path.Combine(AppContext.BaseDirectory, "Assets", "squill.ico"));
        ToolTipService.SetToolTip(BackButton, UiText.Get("settings.back"));
        BuildNav();
        Root.ActualThemeChanged += (_, _) => BuildNav();
        foreach (var value in Enum.GetValues<KeywordCase>())
            KeywordCaseBox.Items.Add(value);
        foreach (var option in settings.LanguageOptions)
            LanguageBox.Items.Add(option.Label);
        foreach (var option in settings.FrequencyOptions)
            FrequencyBox.Items.Add(option.Label);
        foreach (var option in settings.ChannelOptions)
            ChannelBox.Items.Add(option.Label);

        _loading = true;
        FontSizeBox.Value = settings.EditorFontSize;
        MaxRowsBox.Value = settings.MaxGridRows;
        KeywordCaseBox.SelectedItem = settings.FormatterKeywordCase;
        LanguageBox.SelectedIndex = IndexOf(settings.Language);
        RestartNote.Visibility = Show(settings.RestartRequired);
        FrequencyBox.SelectedIndex = settings.FrequencyOptions.ToList().FindIndex(o => o.Value == settings.UpdateFrequency);
        ChannelBox.SelectedIndex = settings.ChannelOptions.ToList().FindIndex(o => o.Value == settings.UpdateChannel);
        NoDowngradeNote.Visibility = Show(settings.ShowNoDowngradeNote);
        Card(SettingsFormat.ThemeIndex(settings.Theme)).IsChecked = true;
        _loading = false;
        DataFolderPath.Text = dataDirectory;
        NavAppearance.IsChecked = true;

        ModalWindow.Place(this, owner, LogicalWidth, LogicalHeight, true);
        AutomationProperties.SetName(FrequencyBox, UiText.Get("settings.update.frequency"));
        AutomationProperties.SetName(ChannelBox, UiText.Get("settings.update.channel"));
        AutomationProperties.SetName(CheckNowButton, UiText.Get("update.button.checkNow"));
        updates.PropertyChanged += OnUpdatesChanged;
        RefreshUpdateStatus();
        Closed += (_, _) =>
        {
            updates.PropertyChanged -= OnUpdatesChanged;
            _owner.Activate();
        };
    }

    private void OnUpdatesChanged(object? sender, PropertyChangedEventArgs e) => RefreshUpdateStatus();

    private void RefreshUpdateStatus()
    {
        UpdateStatusText.Text = _updates.StatusText;
        CheckNowButton.IsEnabled = _updates.CanCheck;
    }

    private void OnCheckNow(object sender, RoutedEventArgs e) => _updates.CheckNowCommand.Execute(null);

    private void OnFrequencyChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_loading && FrequencyBox.SelectedIndex >= 0)
            _settings.UpdateFrequency = _settings.FrequencyOptions[FrequencyBox.SelectedIndex].Value;
    }

    private void OnChannelChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_loading || ChannelBox.SelectedIndex < 0)
            return;
        _settings.UpdateChannel = _settings.ChannelOptions[ChannelBox.SelectedIndex].Value;
        NoDowngradeNote.Visibility = Show(_settings.ShowNoDowngradeNote);
    }

    private RadioButton Card(int index) => index switch
    {
        1 => ThemeLight,
        2 => ThemeDark,
        _ => ThemeSystem,
    };

    // The icons and labels hold brushes of the theme they were built in, so they are built again when it changes.
    private void BuildNav()
    {
        BackButton.Content = Icons.Create("SquillIconArrowLeft", 16, Tokens.Brush(Root, "SquillFg2Brush"));
        Nav(NavAppearance, "SquillIconView", "settings.appearance");
        Nav(NavEditor, "SquillIconSnippets", "settings.editor");
        Nav(NavGrid, "SquillIconTable", "settings.grid");
        Nav(NavAdvanced, "SquillIconSettings", "settings.advanced");
    }

    private void Nav(RadioButton item, string icon, string key) =>
        item.Content = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 12,
            Children =
            {
                Icons.Create(icon, 18, Tokens.Brush(Root, "SquillFg2Brush")),
                new TextBlock { Text = UiText.Get(key), Foreground = Tokens.Brush(Root, "SquillFgBrush"), VerticalAlignment = VerticalAlignment.Center },
            },
        };

    private void OnBack(object sender, RoutedEventArgs e) => Close();

    private void OnNavChecked(object sender, RoutedEventArgs e)
    {
        var nav = (RadioButton)sender;
        PageAppearance.Visibility = Show(nav == NavAppearance);
        PageEditor.Visibility = Show(nav == NavEditor);
        PageGrid.Visibility = Show(nav == NavGrid);
        PageAdvanced.Visibility = Show(nav == NavAdvanced);
    }

    private void OnThemeChecked(object sender, RoutedEventArgs e)
    {
        if (!_loading)
            _settings.Theme = SettingsFormat.Themes[(RadioButton)sender == ThemeLight ? 1 : (RadioButton)sender == ThemeDark ? 2 : 0];
    }

    private void OnFontSizeChanged(NumberBox sender, NumberBoxValueChangedEventArgs e)
    {
        if (!_loading && !double.IsNaN(e.NewValue))
            _settings.EditorFontSize = e.NewValue;
    }

    private void OnMaxRowsChanged(NumberBox sender, NumberBoxValueChangedEventArgs e)
    {
        if (!_loading && !double.IsNaN(e.NewValue))
            _settings.MaxGridRows = (int)e.NewValue;
    }

    private void OnKeywordCaseChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_loading && KeywordCaseBox.SelectedItem is KeywordCase chosen)
            _settings.FormatterKeywordCase = chosen;
    }

    private void OnLanguageChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_loading || LanguageBox.SelectedIndex < 0)
            return;
        _settings.Language = _settings.LanguageOptions[LanguageBox.SelectedIndex].Value;
        RestartNote.Visibility = Show(_settings.RestartRequired);
    }

    private int IndexOf(AppLanguage language)
    {
        for (var i = 0; i < _settings.LanguageOptions.Count; i++)
            if (_settings.LanguageOptions[i].Value == language)
                return i;
        return 0;
    }

    private async void OnOpenDataFolder(object sender, RoutedEventArgs e)
    {
        Directory.CreateDirectory(_dataDirectory);
        await Launcher.LaunchFolderPathAsync(_dataDirectory);
    }

    private static Visibility Show(bool visible) => visible ? Visibility.Visible : Visibility.Collapsed;
}
