using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using CommunityToolkit.Mvvm.Input;
using Squill.App.Avalonia.Views;
using Squill.Presentation;
using Squill.Presentation.Designer;
using Squill.Presentation.Diagram;
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
            switch (e.PropertyName)
            {
                case nameof(WorkspaceViewModel.ActiveTab):
                    ShowQueryArea();
                    break;
                case nameof(WorkspaceViewModel.ActiveDiagram) when services.Workspace.ActiveDiagram is { } diagram:
                    DiagramStrip.SelectedItem = diagram;
                    break;
                case nameof(WorkspaceViewModel.ActiveDesigner) when services.Workspace.ActiveDesigner is { } designer:
                    DesignerStrip.SelectedItem = designer;
                    break;
            }
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
        DiagramStrip.SelectedItem = null;
        DesignerStrip.SelectedItem = null;
    }

    private void OnExtraSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        // One strip at a time: selecting a diagram or designer clears the other strips, then the matching area shows.
        if (sender is ListBox { SelectedItem: not null } strip)
        {
            foreach (var other in new[] { DiagramStrip, DesignerStrip, TerminalStrip })
            {
                if (other != strip)
                    other.SelectedItem = null;
            }

            TerminalArea.IsVisible = false;
        }

        var diagram = DiagramStrip.SelectedItem as DiagramTabViewModel;
        DiagramArea.DataContext = diagram;
        DiagramArea.IsVisible = diagram is not null;
        var designer = DesignerStrip.SelectedItem as TableDesignerViewModel;
        DesignerArea.DataContext = designer;
        DesignerArea.IsVisible = designer is not null;
    }

    private void OnCloseDiagramClicked(object? sender, RoutedEventArgs e)
    {
        if (DataContext is WorkspaceViewModel workspace && (sender as Control)?.DataContext is DiagramTabViewModel diagram)
            workspace.CloseDiagramCommand.Execute(diagram);
    }

    private void OnCloseDesignerClicked(object? sender, RoutedEventArgs e)
    {
        if (DataContext is WorkspaceViewModel workspace && (sender as Control)?.DataContext is TableDesignerViewModel designer)
            workspace.CloseDesignerCommand.Execute(designer);
    }

    private void OnTerminalSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (TerminalStrip.SelectedItem is TerminalTabViewModel terminal)
        {
            DiagramStrip.SelectedItem = null;
            DesignerStrip.SelectedItem = null;
            ShowTerminal(terminal);
        }
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
