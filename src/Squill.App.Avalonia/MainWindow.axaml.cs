using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using CommunityToolkit.Mvvm.Input;
using Squill.App.Avalonia.Views;
using Squill.Presentation;
using Squill.Presentation.Query;
using Squill.Presentation.Settings;
using Squill.Presentation.Terminal;
using Squill.Presentation.Workspace;

namespace Squill.App.Avalonia;

/// <summary>The workspace shell: sidebar, query tabs, editor with results, and status bar.</summary>
public sealed partial class MainWindow : Window
{
    private SettingsViewModel? _settings;

    /// <summary>Creates the window.</summary>
    public MainWindow()
    {
        InitializeComponent();
        KeyBindings.Add(new KeyBinding { Gesture = new KeyGesture(Key.OemComma, KeyModifiers.Control), Command = new RelayCommand(OpenSettings) });
    }

    /// <summary>Connects the history, snippets and settings view-models and the data directory shown in the status bar.</summary>
    /// <param name="services">The shared root view-models.</param>
    /// <param name="dataDirectory">The directory the status bar link opens.</param>
    public void Attach(AppServices services, string dataDirectory)
    {
        Sidebar.Attach(services.History, services.Snippets);
        AttachTerminals(services.Workspace);
        services.Workspace.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(WorkspaceViewModel.ActiveTab))
                ShowQueryArea();
        };
        QueryStrip.AddHandler(PointerPressedEvent, (_, _) => ShowQueryArea(), RoutingStrategies.Tunnel);
        Status.DataDirectory = dataDirectory;
        _settings = services.Settings;
        _settings.PropertyChanged += OnSettingsPropertyChanged;
        ActiveTabView.EditorFontSize = _settings.EditorFontSize;
    }

    private void OnSettingsPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(SettingsViewModel.EditorFontSize) && _settings is not null)
            ActiveTabView.EditorFontSize = _settings.EditorFontSize;
    }

    private void OpenSettings()
    {
        if (_settings is not null)
            new SettingsWindow { DataContext = _settings }.Show(this);
    }

    private void OnSettingsClicked(object? sender, RoutedEventArgs e) => OpenSettings();

    private void ShowQueryArea()
    {
        TerminalArea.IsVisible = false;
        TerminalStrip.SelectedItem = null;
    }

    private void OnTerminalSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (TerminalStrip.SelectedItem is TerminalTabViewModel terminal)
            ShowTerminal(terminal);
    }

    private void OnCloseTerminalClicked(object? sender, RoutedEventArgs e)
    {
        if (DataContext is WorkspaceViewModel workspace && (sender as Control)?.DataContext is TerminalTabViewModel terminal)
            workspace.CloseTerminalCommand.Execute(terminal);
    }

    private void OnCloseTabClicked(object? sender, RoutedEventArgs e)
    {
        if (DataContext is WorkspaceViewModel workspace && (sender as Control)?.DataContext is QueryTabViewModel tab)
            workspace.CloseTabCommand.Execute(tab);
    }
}
