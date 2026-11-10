using System.Collections.Specialized;
using System.ComponentModel;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Squill.App.WinUI.Controls;
using Squill.App.WinUI.Views;
using Squill.Platform.Settings;
using Squill.Presentation;
using Squill.Presentation.Connections;
using Squill.Presentation.Designer;
using Squill.Presentation.Diagram;
using Squill.Presentation.Plans;
using Squill.Presentation.Query;
using Squill.Presentation.Settings;
using Squill.Presentation.Terminal;
using Squill.Presentation.Workspace;
using Windows.System;

namespace Squill.App.WinUI;

/// <summary>The workspace shell: title bar with menu, activity rail, sidebar, query tabs, editor with results, and status bar.</summary>
public sealed partial class MainWindow : Window
{
    private readonly Dictionary<QueryTabViewModel, TabViewItem> _items = [];
    private WorkspaceViewModel? _workspace;
    private SettingsViewModel? _settings;
    private AppServices? _services;
    private AboutWindow? _aboutWindow;
    private ConnectionEditorWindow? _editorWindow;
    private SettingsWindow? _settingsWindow;
    private bool _syncing;

    /// <summary>Initializes the window and sets its icon.</summary>
    public MainWindow()
    {
        InitializeComponent();
        InitializeChrome();
        AppWindow.SetIcon(Path.Combine(AppContext.BaseDirectory, "Assets", "squill.ico"));
        var splitter = new Splitter(true);
        Grid.SetRow(splitter, 2);
        Grid.SetColumn(splitter, 2);
        Root.Children.Add(splitter);
    }

    /// <summary>Connects the window to the shared view-models and loads the saved connections and settings.</summary>
    /// <param name="services">The shared root view-models.</param>
    /// <param name="dataDirectory">The directory holding profiles, settings and history, opened from the File menu.</param>
    /// <param name="dialogs">The service that reports grid paste errors.</param>
    public void Attach(AppServices services, string dataDirectory, IDialogService dialogs)
    {
        ActiveTabView.Dialogs = dialogs;
        _workspace = services.Workspace;
        _settings = services.Settings;
        _services = services;
        _dataDirectory = dataDirectory;
        Root.KeyboardAccelerators.Add(SettingsAccelerator());
        services.Settings.ThemeChanged += (_, _) => ApplyTheme(services.Settings.Theme);
        services.Settings.LoadCommand.Execute(null);
        services.Workspace.ConnectionList.LoadCommand.Execute(null);
        services.Workspace.ConnectionList.EditRequested += OnEditRequested;
        services.Workspace.Tabs.CollectionChanged += OnTabsChanged;
        services.Workspace.PropertyChanged += OnWorkspaceChanged;
        AttachDiagrams(services.Workspace);
        AttachTerminals(services.Workspace);
        Sidebar.Attach(services.Workspace, services.History, services.Snippets);
        ActiveTabView.AttachPanels(services.History, services.Snippets);
        ActiveTabView.SetEnvironment(services.Workspace.ActiveEnvironment);
        Sidebar.SelectedNodeChanged += (_, node) => Properties.Show(node, services.Workspace);
        Status.Attach(services.Workspace);
        AttachMenu(services.Workspace);
        AttachUpdates();
    }

    private WorkspaceViewModel Workspace => _workspace!;

    // XAML has no name for the comma key, so Ctrl+, is built from its virtual-key code (VK_OEM_COMMA).
    private KeyboardAccelerator SettingsAccelerator()
    {
        var accelerator = new KeyboardAccelerator { Key = (VirtualKey)188, Modifiers = VirtualKeyModifiers.Control };
        accelerator.Invoked += (_, args) => Accelerate(args, () => OnSettingsClicked(null!, null!));
        return accelerator;
    }

    private void OnSettingsClicked(object sender, RoutedEventArgs e)
    {
        if (_settingsWindow is { } open)
        {
            open.Activate();
            return;
        }

        var window = new SettingsWindow(this, _settings!, _dataDirectory!, _services!.Updates);
        _settingsWindow = window;
        window.Closed += (_, _) => _settingsWindow = null;
        window.Activate();
    }

    private void OnAboutClicked(object sender, RoutedEventArgs e)
    {
        if (_aboutWindow is { } open)
        {
            open.Activate();
            return;
        }

        var window = new AboutWindow(this, _services!.About);
        _aboutWindow = window;
        window.Closed += (_, _) => _aboutWindow = null;
        window.Activate();
    }

    private void ApplyTheme(ThemeMode mode) => Root.RequestedTheme = mode switch
    {
        ThemeMode.Light => ElementTheme.Light,
        ThemeMode.Dark => ElementTheme.Dark,
        _ => ElementTheme.Default,
    };

    private void OnEditRequested(object? sender, ConnectionEditorViewModel editor)
    {
        if (_editorWindow is { } open)
        {
            open.SetEditor(editor);
            open.Activate();
            return;
        }

        var window = new ConnectionEditorWindow(this, Workspace.ConnectionList, editor);
        _editorWindow = window;
        window.Closed += (_, _) => _editorWindow = null;
        window.Activate();
    }

    private void OnWorkspaceChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(WorkspaceViewModel.ActiveEnvironment):
                ActiveTabView.SetEnvironment(Workspace.ActiveEnvironment);
                break;
            case nameof(WorkspaceViewModel.ActiveTab):
                ShowActiveTab();
                break;
            case nameof(WorkspaceViewModel.ActiveDiagram) when Workspace.ActiveDiagram is { } diagram:
                SelectExtraTab(diagram);
                break;
            case nameof(WorkspaceViewModel.ActivePlan) when Workspace.ActivePlan is { } plan:
                SelectExtraTab(plan);
                break;
            case nameof(WorkspaceViewModel.ActiveDesigner) when Workspace.ActiveDesigner is { } designer:
                SelectExtraTab(designer);
                break;
        }
    }

    private void ShowActiveTab()
    {
        var tab = Workspace.ActiveTab;
        _syncing = true;
        Tabs.SelectedItem = tab is not null && _items.TryGetValue(tab, out var item) ? item : null;
        _syncing = false;
        ShowQueryArea();
        ActiveTabView.Show(tab);
    }

    private void OnTabsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        foreach (var old in e.OldItems?.OfType<QueryTabViewModel>() ?? [])
        {
            if (_items.Remove(old, out var item))
                Tabs.TabItems.Remove(item);
        }

        foreach (var added in e.NewItems?.OfType<QueryTabViewModel>() ?? [])
        {
            var item = TabHeader.Create(added);
            _items[added] = item;
            Tabs.TabItems.Add(item);
        }

        ShowActiveTab();
    }

    private void OnTabSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_syncing)
            return;
        switch ((Tabs.SelectedItem as TabViewItem)?.Tag)
        {
            case QueryTabViewModel tab:
                Workspace.ActiveTab = tab;
                Workspace.ActivePlan = null;
                ShowQueryArea();
                break;
            case DiagramTabViewModel diagram:
                Workspace.ActiveDiagram = diagram;
                ShowDiagram(diagram);
                break;
            case TableDesignerViewModel designer:
                Workspace.ActiveDesigner = designer;
                ShowDesigner(designer);
                break;
            case PlanTabViewModel plan:
                Workspace.ActivePlan = plan;
                ShowPlan(plan);
                break;
            case TerminalTabViewModel terminal:
                ShowTerminal(terminal);
                break;
        }
    }

    private void OnTabCloseRequested(TabView sender, TabViewTabCloseRequestedEventArgs args)
    {
        if (args.Tab.Tag is QueryTabViewModel tab)
            Workspace.CloseTabCommand.Execute(tab);
        else if (args.Tab.Tag is TerminalTabViewModel terminal)
            Workspace.CloseTerminalCommand.Execute(terminal);
        else if (args.Tab.Tag is DiagramTabViewModel diagram)
            Workspace.CloseDiagramCommand.Execute(diagram);
        else if (args.Tab.Tag is PlanTabViewModel plan)
            plan.CloseCommand.Execute(null);
        else if (args.Tab.Tag is TableDesignerViewModel designer)
            Workspace.CloseDesignerCommand.Execute(designer);
    }

    private void OnDiagramClicked(object sender, RoutedEventArgs e) => Workspace.OpenDiagramCommand.Execute(null);

    private void OnNewTableClicked(object sender, RoutedEventArgs e) => Workspace.NewTableCommand.Execute(null);

    private void OnApplyDesignerAccelerator(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        if (DesignerArea.Visibility == Visibility.Visible)
            Accelerate(args, () => Workspace.ActiveDesigner?.ApplyCommand.Execute(null));
    }

    private void OnRunClicked(object sender, RoutedEventArgs e) => Workspace.ActiveTab?.RunCommand.Execute(null);

    private void OnRunAllClicked(object sender, RoutedEventArgs e) => Workspace.ActiveTab?.RunAllCommand.Execute(null);

    private void OnCancelClicked(object sender, RoutedEventArgs e) => Workspace.ActiveTab?.CancelCommand.Execute(null);

    private void OnFormatClicked(object sender, RoutedEventArgs e) => Workspace.ActiveTab?.FormatDocumentCommand.Execute(null);

    private void OnNewTabClicked(object sender, RoutedEventArgs e) => Workspace.NewTabCommand.Execute(null);

    private void OnCloseTabClicked(object sender, RoutedEventArgs e)
    {
        var selected = (Tabs.SelectedItem as TabViewItem)?.Tag;
        if (selected is TerminalTabViewModel terminal)
            Workspace.CloseTerminalCommand.Execute(terminal);
        else if (selected is DiagramTabViewModel diagram)
            Workspace.CloseDiagramCommand.Execute(diagram);
        else if (selected is PlanTabViewModel plan)
            plan.CloseCommand.Execute(null);
        else if (selected is TableDesignerViewModel designer)
            Workspace.CloseDesignerCommand.Execute(designer);
        else
            Workspace.CloseTabCommand.Execute(Workspace.ActiveTab);
    }

    private void OnRunAccelerator(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args) => Accelerate(args, () => OnRunClicked(sender, null!));

    private void OnCancelAccelerator(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args) => Accelerate(args, () => OnCancelClicked(sender, null!));

    private void OnFormatAccelerator(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args) => Accelerate(args, () => OnFormatClicked(sender, null!));

    private void OnNewTabAccelerator(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args) => Accelerate(args, () => OnNewTabClicked(sender, null!));

    private void OnCloseTabAccelerator(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args) => Accelerate(args, () => OnCloseTabClicked(sender, null!));

    // Ctrl+E and Ctrl+Shift+E reach the plan commands only while a query tab or a plan tab is shown; elsewhere the keys stay free.
    private void OnExplainAccelerator(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        if (PlanArea.Visibility == Visibility.Visible)
        {
            if (Workspace.ActivePlan is { Mode: Squill.Core.Plans.PlanMode.Estimated } plan)
                Accelerate(args, () => plan.RerunCommand.Execute(null));
        }
        else if (ActiveTabView.Visibility == Visibility.Visible)
            Accelerate(args, () => Workspace.ActiveTab?.ExplainCommand.Execute(null));
    }

    private void OnAnalyzeAccelerator(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        if (PlanArea.Visibility == Visibility.Visible)
            Accelerate(args, () => Workspace.ActivePlan?.RerunCommand.Execute(null));
        else if (ActiveTabView.Visibility == Visibility.Visible)
            Accelerate(args, () => Workspace.ActiveTab?.AnalyzeCommand.Execute(null));
    }

    private static void Accelerate(KeyboardAcceleratorInvokedEventArgs args, Action action)
    {
        action();
        args.Handled = true;
    }
}
