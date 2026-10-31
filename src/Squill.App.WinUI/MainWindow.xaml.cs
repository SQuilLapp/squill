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
using Squill.Presentation.Query;
using Squill.Presentation.Workspace;

namespace Squill.App.WinUI;

/// <summary>The workspace shell: sidebar, query tabs, editor with results, and status bar.</summary>
public sealed partial class MainWindow : Window
{
    private readonly Dictionary<QueryTabViewModel, TabViewItem> _items = [];
    private WorkspaceViewModel? _workspace;
    private bool _syncing;

    /// <summary>Initializes the window and sets its icon.</summary>
    public MainWindow()
    {
        InitializeComponent();
        AppWindow.SetIcon(Path.Combine(AppContext.BaseDirectory, "Assets", "squill.ico"));
        var splitter = new Splitter(true);
        Grid.SetRow(splitter, 1);
        Grid.SetColumn(splitter, 1);
        Root.Children.Add(splitter);
    }

    /// <summary>Connects the window to the shared view-models and loads the saved connections and settings.</summary>
    /// <param name="services">The shared root view-models.</param>
    public void Attach(AppServices services)
    {
        _workspace = services.Workspace;
        services.Settings.ThemeChanged += (_, _) => ApplyTheme(services.Settings.Theme);
        services.Settings.LoadCommand.Execute(null);
        services.Workspace.ConnectionList.LoadCommand.Execute(null);
        services.Workspace.ConnectionList.EditRequested += OnEditRequested;
        services.Workspace.Tabs.CollectionChanged += OnTabsChanged;
        services.Workspace.PropertyChanged += OnWorkspaceChanged;
        Sidebar.Attach(services.Workspace);
        Status.Attach(services.Workspace);
    }

    private WorkspaceViewModel Workspace => _workspace!;

    private void ApplyTheme(ThemeMode mode) => Root.RequestedTheme = mode switch
    {
        ThemeMode.Light => ElementTheme.Light,
        ThemeMode.Dark => ElementTheme.Dark,
        _ => ElementTheme.Default,
    };

    private async void OnEditRequested(object? sender, ConnectionEditorViewModel editor) =>
        await new ConnectionEditorDialog(editor, Root.XamlRoot).ShowAsync();

    private void OnWorkspaceChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(WorkspaceViewModel.ActiveTab))
            return;
        ShowActiveTab();
    }

    private void ShowActiveTab()
    {
        var tab = Workspace.ActiveTab;
        _syncing = true;
        Tabs.SelectedItem = tab is not null && _items.TryGetValue(tab, out var item) ? item : null;
        _syncing = false;
        ActiveTabView.Show(tab);
        CancelButton.Visibility = Visibility.Collapsed;
        if (tab is not null)
        {
            tab.PropertyChanged -= OnActiveTabChanged;
            tab.PropertyChanged += OnActiveTabChanged;
            ShowRunning(tab);
        }
    }

    private void OnActiveTabChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (sender is not QueryTabViewModel tab)
            return;
        if (e.PropertyName == nameof(QueryTabViewModel.IsRunning) && tab == Workspace.ActiveTab)
            ShowRunning(tab);
        else if (e.PropertyName == nameof(QueryTabViewModel.Title) && _items.TryGetValue(tab, out var item))
            item.Header = tab.Title;
    }

    private void ShowRunning(QueryTabViewModel tab) =>
        CancelButton.Visibility = tab.IsRunning ? Visibility.Visible : Visibility.Collapsed;

    private void OnTabsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        foreach (var old in e.OldItems?.OfType<QueryTabViewModel>() ?? [])
        {
            if (_items.Remove(old, out var item))
                Tabs.TabItems.Remove(item);
        }

        foreach (var added in e.NewItems?.OfType<QueryTabViewModel>() ?? [])
        {
            var item = new TabViewItem { Header = added.Title, Tag = added };
            _items[added] = item;
            Tabs.TabItems.Add(item);
        }

        ShowActiveTab();
    }

    private void OnTabSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_syncing && (Tabs.SelectedItem as TabViewItem)?.Tag is QueryTabViewModel tab)
            Workspace.ActiveTab = tab;
    }

    private void OnTabCloseRequested(TabView sender, TabViewTabCloseRequestedEventArgs args)
    {
        if (args.Tab.Tag is QueryTabViewModel tab)
            Workspace.CloseTabCommand.Execute(tab);
    }

    private void OnRunClicked(object sender, RoutedEventArgs e) => Workspace.ActiveTab?.RunCommand.Execute(null);

    private void OnRunAllClicked(object sender, RoutedEventArgs e) => Workspace.ActiveTab?.RunAllCommand.Execute(null);

    private void OnCancelClicked(object sender, RoutedEventArgs e) => Workspace.ActiveTab?.CancelCommand.Execute(null);

    private void OnFormatClicked(object sender, RoutedEventArgs e) => Workspace.ActiveTab?.FormatDocumentCommand.Execute(null);

    private void OnNewTabClicked(object sender, RoutedEventArgs e) => Workspace.NewTabCommand.Execute(null);

    private void OnCloseTabClicked(object sender, RoutedEventArgs e) => Workspace.CloseTabCommand.Execute(Workspace.ActiveTab);

    private void OnRunAccelerator(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args) => Accelerate(args, () => OnRunClicked(sender, null!));

    private void OnCancelAccelerator(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args) => Accelerate(args, () => OnCancelClicked(sender, null!));

    private void OnFormatAccelerator(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args) => Accelerate(args, () => OnFormatClicked(sender, null!));

    private void OnNewTabAccelerator(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args) => Accelerate(args, () => OnNewTabClicked(sender, null!));

    private void OnCloseTabAccelerator(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args) => Accelerate(args, () => OnCloseTabClicked(sender, null!));

    private static void Accelerate(KeyboardAcceleratorInvokedEventArgs args, Action action)
    {
        action();
        args.Handled = true;
    }
}
