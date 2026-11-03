using System.Runtime.InteropServices;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Squill.Core;
using Squill.Presentation.Localization;
using Squill.Presentation.Settings;
using Windows.Graphics;
using Windows.System;
using WinRT.Interop;

namespace Squill.App.WinUI.Views;

/// <summary>Window, modal to the main window, that edits the preferences; every change is applied to <see cref="SettingsViewModel"/> at once.</summary>
public sealed partial class SettingsWindow : Window
{
    private const int LogicalWidth = 1100;
    private const int LogicalHeight = 720;
    private const int OwnerIndex = -8; // GWLP_HWNDPARENT

    private readonly Window _owner;
    private readonly SettingsViewModel _settings;
    private readonly string _dataDirectory;
    private bool _loading;

    /// <summary>Creates the window bound to the settings.</summary>
    /// <param name="owner">The main window; it stays disabled while this window is open.</param>
    /// <param name="theme">The theme of the main window.</param>
    /// <param name="settings">The settings to show and edit.</param>
    /// <param name="dataDirectory">The directory holding profiles, settings and history, shown on the Advanced page.</param>
    public SettingsWindow(Window owner, ElementTheme theme, SettingsViewModel settings, string dataDirectory)
    {
        InitializeComponent();
        _owner = owner;
        _settings = settings;
        _dataDirectory = dataDirectory;
        Root.RequestedTheme = theme;
        Title = UiText.Get("settings.title");
        AppWindow.SetIcon(Path.Combine(AppContext.BaseDirectory, "Assets", "squill.ico"));
        BackButton.Content = Icons.Create("SquillIconArrowLeft", 16, Tokens.Brush(Root, "SquillFg2Brush"));
        ToolTipService.SetToolTip(BackButton, UiText.Get("settings.back"));
        Nav(NavAppearance, "SquillIconView", "settings.appearance");
        Nav(NavEditor, "SquillIconSnippets", "settings.editor");
        Nav(NavGrid, "SquillIconTable", "settings.grid");
        Nav(NavAdvanced, "SquillIconSettings", "settings.advanced");
        foreach (var value in Enum.GetValues<KeywordCase>())
            KeywordCaseBox.Items.Add(value);

        _loading = true;
        FontSizeBox.Value = settings.EditorFontSize;
        MaxRowsBox.Value = settings.MaxGridRows;
        KeywordCaseBox.SelectedItem = settings.FormatterKeywordCase;
        Card(SettingsFormat.ThemeIndex(settings.Theme)).IsChecked = true;
        _loading = false;
        DataFolderPath.Text = dataDirectory;
        NavAppearance.IsChecked = true;

        PlaceModal(owner);
        Closed += (_, _) => _owner.Activate();
    }

    private RadioButton Card(int index) => index switch
    {
        1 => ThemeLight,
        2 => ThemeDark,
        _ => ThemeSystem,
    };

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

    private async void OnOpenDataFolder(object sender, RoutedEventArgs e)
    {
        Directory.CreateDirectory(_dataDirectory);
        await Launcher.LaunchFolderPathAsync(_dataDirectory);
    }

    private static Visibility Show(bool visible) => visible ? Visibility.Visible : Visibility.Collapsed;

    private void PlaceModal(Window owner)
    {
        var ownerHandle = WindowNative.GetWindowHandle(owner);
        var handle = WindowNative.GetWindowHandle(this);
        Win32.SetWindowLongPtr(handle, OwnerIndex, ownerHandle);
        var scale = Win32.GetDpiForWindow(handle) / 96.0;
        var size = new SizeInt32((int)(LogicalWidth * scale), (int)(LogicalHeight * scale));
        AppWindow.Resize(size);
        var at = owner.AppWindow.Position;
        var around = owner.AppWindow.Size;
        AppWindow.Move(new PointInt32(at.X + Math.Max(0, (around.Width - size.Width) / 2), at.Y + Math.Max(0, (around.Height - size.Height) / 2)));
        if (AppWindow.Presenter is OverlappedPresenter presenter)
        {
            presenter.IsMinimizable = false;
            presenter.IsModal = true;
        }
    }

    private static partial class Win32
    {
        [LibraryImport("user32.dll", EntryPoint = "SetWindowLongPtrW")]
        internal static partial nint SetWindowLongPtr(nint window, int index, nint value);

        [LibraryImport("user32.dll")]
        internal static partial uint GetDpiForWindow(nint window);
    }
}
