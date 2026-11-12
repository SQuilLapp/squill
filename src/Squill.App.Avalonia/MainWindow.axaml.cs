using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.Input;
using Squill.App.Avalonia.Views;
using Squill.Presentation;
using Squill.Presentation.Designer;
using Squill.Presentation.Diagram;
using Squill.Core.Plans;
using Squill.Presentation.Plans;
using Squill.Presentation.Query;
using Squill.Presentation.Settings;
using Squill.Presentation.Terminal;
using Squill.Presentation.Workspace;

namespace Squill.App.Avalonia;

/// <summary>The workspace shell: title bar with menu, activity rail, sidebar, query tabs, editor with results, and status bar.</summary>
public sealed partial class MainWindow : Window
{
    private SettingsViewModel? _settings;
    private string? _dataDirectory;
    private WorkspaceTabList? _tabs;
    private bool _exitConfirmed;
    private bool _askingToExit;

    /// <summary>Creates the window.</summary>
    public MainWindow()
    {
        InitializeComponent();
        InitializeTitleLayout();
        InitializeMacChrome();
        Closing += OnWindowClosing;
        KeyBindings.Add(new KeyBinding { Gesture = new KeyGesture(Key.OemComma, KeyModifiers.Control), Command = new RelayCommand(OpenSettings) });
        KeyBindings.Add(new KeyBinding { Gesture = new KeyGesture(Key.E, KeyModifiers.Control), Command = new RelayCommand(OnExplainKey) });
        KeyBindings.Add(new KeyBinding { Gesture = new KeyGesture(Key.E, KeyModifiers.Control | KeyModifiers.Shift), Command = new RelayCommand(OnAnalyzeKey) });
        KeyBindings.Add(new KeyBinding { Gesture = new KeyGesture(Key.C, KeyModifiers.Control | KeyModifiers.Alt), Command = new RelayCommand(() => RunTransactionCommand(q => q.CommitCommand)) });
        KeyBindings.Add(new KeyBinding { Gesture = new KeyGesture(Key.Z, KeyModifiers.Control | KeyModifiers.Alt), Command = new RelayCommand(() => RunTransactionCommand(q => q.RollbackCommand)) });
    }

    // An open transaction needs an answer before the window goes away: cancel this close, ask, and close again once the user decided.
    private void OnWindowClosing(object? sender, WindowClosingEventArgs e)
    {
        if (!NeedsExitQuestion())
            return;
        e.Cancel = true;
        _ = AskThenCloseAsync();
    }

    /// <summary>Asks about open transactions when the application is asked to quit (the macOS application-menu Quit does not always close the main window first).</summary>
    /// <param name="sender">The desktop lifetime.</param>
    /// <param name="e">The shutdown request; cancelled while the question is open.</param>
    internal void OnShutdownRequested(object? sender, ShutdownRequestedEventArgs e)
    {
        if (!NeedsExitQuestion())
            return;
        e.Cancel = true;
        _ = AskThenCloseAsync();
    }

    private bool NeedsExitQuestion() => !_exitConfirmed && DataContext is WorkspaceViewModel { HasOpenTransaction: true };

    private async Task AskThenCloseAsync()
    {
        if (_askingToExit || DataContext is not WorkspaceViewModel workspace)
            return;
        _askingToExit = true;
        try
        {
            if (!await workspace.PrepareExitAsync(CancellationToken.None))
                return;
        }
        finally
        {
            _askingToExit = false;
        }

        _exitConfirmed = true;
        Close();
    }

    // Only while a query tab is active (a plan tab or the designer has no transaction group).
    private void RunTransactionCommand(Func<QueryTabViewModel, IAsyncRelayCommand> pick)
    {
        if (!PlanArea.IsVisible && _tabs?.Selected?.Source is QueryTabViewModel { } query && pick(query) is { } command && command.CanExecute(null))
            command.Execute(null);
    }

    /// <summary>Connects the history, snippets and settings view-models and the data directory the File menu opens.</summary>
    /// <param name="services">The shared root view-models.</param>
    /// <param name="dataDirectory">The directory the File menu opens.</param>
    public void Attach(AppServices services, string dataDirectory)
    {
        Sidebar.Attach(services.History, services.Snippets);
        services.Workspace.Explorer.Picker = new Services.AvaloniaTablePicker(() => this);
        ActiveTabView.AttachPanels(services.History, services.Snippets);
        services.Workspace.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(WorkspaceViewModel.ActiveEnvironment))
                ActiveTabView.SetEnvironment(services.Workspace.ActiveEnvironment);
        };
        ActiveTabView.SetEnvironment(services.Workspace.ActiveEnvironment);
        _tabs = new WorkspaceTabList(services.Workspace);
        TabStrip.ItemsSource = _tabs.Items;
        TabStrip.SelectionChanged += OnTabSelectionChanged;
        _tabs.PropertyChanged += OnTabsChanged;
        AttachTerminals(services.Workspace);
        Sidebar.SelectedNodeChanged += (_, node) => Properties.Show(node, services.Workspace);
        _dataDirectory = dataDirectory;
        _settings = services.Settings;
        _settings.PropertyChanged += OnSettingsPropertyChanged;
        ActiveTabView.EditorFontSize = _settings.EditorFontSize;
        AttachUpdates(services.Updates, services.About);
        InitializeMacMenu();
    }

    private void OnSettingsPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(SettingsViewModel.EditorFontSize) && _settings is not null)
            ActiveTabView.EditorFontSize = _settings.EditorFontSize;
    }

    private void OpenSettings()
    {
        if (_settings is not null)
            new SettingsWindow { DataContext = _settings, DataDirectory = _dataDirectory, Updates = _updates }.Show(this);
    }

    private void OnSettingsClicked(object? sender, RoutedEventArgs e) => OpenSettings();

    private void OnRailChecked(object? sender, RoutedEventArgs e)
    {
        if (sender is RadioButton { IsChecked: true } rail)
            Sidebar.ShowPanel(rail == RailHistory ? SidebarPanel.History : rail == RailSnippets ? SidebarPanel.Snippets : SidebarPanel.Explorer);
    }

    private async void OnOpenDataFolder(object? sender, RoutedEventArgs e)
    {
        if (_dataDirectory is null)
            return;
        Directory.CreateDirectory(_dataDirectory);
        await Launcher.LaunchDirectoryInfoAsync(new DirectoryInfo(_dataDirectory));
    }

    private void OnTabSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_tabs is not null && TabStrip.SelectedItem is WorkspaceTab tab)
            _tabs.Selected = tab;
    }

    private void OnTabsChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(WorkspaceTabList.Selected) || _tabs is null)
            return;
        TabStrip.SelectedItem = _tabs.Selected;
        ShowArea(_tabs.Selected?.Source);
    }

    // The query view always sits underneath; a diagram, plan, designer or terminal area overlays it.
    private void ShowArea(object? source)
    {
        TerminalArea.IsVisible = false;
        var diagram = source as DiagramTabViewModel;
        DiagramArea.DataContext = diagram;
        DiagramArea.IsVisible = diagram is not null;
        var plan = source as PlanTabViewModel;
        PlanArea.DataContext = plan;
        PlanArea.IsVisible = plan is not null;
        Status.ShowPlan(plan);
        var designer = source as TableDesignerViewModel;
        DesignerArea.DataContext = designer;
        DesignerArea.IsVisible = designer is not null;
        if (source is TerminalTabViewModel terminal)
            ShowTerminal(terminal);
    }

    // Ctrl+E and Ctrl+Shift+E reach the plan commands only while a query tab or a plan tab is shown; elsewhere the keys stay free.
    private void OnExplainKey()
    {
        if (PlanArea.IsVisible)
        {
            if (PlanArea.DataContext is PlanTabViewModel { Mode: PlanMode.Estimated } plan)
                plan.RerunCommand.Execute(null);
        }
        else if (_tabs?.Selected?.Source is QueryTabViewModel query)
            query.ExplainCommand.Execute(null);
    }

    private void OnAnalyzeKey()
    {
        if (PlanArea.IsVisible)
            (PlanArea.DataContext as PlanTabViewModel)?.RerunCommand.Execute(null);
        else if (_tabs?.Selected?.Source is QueryTabViewModel query)
            query.AnalyzeCommand.Execute(null);
    }

    private void OnCloseTabClicked(object? sender, RoutedEventArgs e)
    {
        if (_tabs is not null && (sender as Control)?.DataContext is WorkspaceTab tab)
            _tabs.Close(tab);
    }
}