using System.Collections.Specialized;
using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using Squill.Presentation.Connections;
using Squill.Presentation.Explorer;
using Squill.Presentation.Localization;
using Squill.Presentation.Panels;
using Squill.Presentation.Workspace;
using Windows.System;

namespace Squill.App.WinUI.Views;

/// <summary>The explorer tree of connections and objects, the history and the snippets, one at a time.</summary>
public sealed partial class SidebarView : UserControl
{
    private WorkspaceViewModel? _workspace;

    /// <summary>Creates the view.</summary>
    public SidebarView()
    {
        InitializeComponent();
        Tree.AddHandler(PointerReleasedEvent, new PointerEventHandler(OnTreePointerReleased), true);
        TitleText.Text = UiText.Get("explorer.title");
        Filter.PlaceholderText = UiText.Get("explorer.filter");
        ToolTipService.SetToolTip(NewConnectionButton, UiText.Get("explorer.newConnection"));
        ToolTipService.SetToolTip(RefreshButton, UiText.Get("explorer.refresh"));
        ToolTipService.SetToolTip(CollapseButton, UiText.Get("explorer.collapseAll"));
    }

    /// <summary>Raised when the selection of the object tree changes; the argument is the selected node or <see langword="null"/>.</summary>
    public event EventHandler<ExplorerNode?>? SelectedNodeChanged;

    /// <summary>Connects the view to the workspace, history and snippets it shows.</summary>
    /// <param name="workspace">The workspace view-model.</param>
    /// <param name="history">The history view-model.</param>
    /// <param name="snippets">The snippets view-model.</param>
    public void Attach(WorkspaceViewModel workspace, HistoryViewModel history, SnippetsViewModel snippets)
    {
        _workspace = workspace;
        HistoryPanel.Attach(history);
        SnippetsPanel.Attach(snippets);
        workspace.ConnectionList.Profiles.CollectionChanged += OnProfilesChanged;
        Tree.ItemsSource = workspace.Explorer.Roots;
        OnProfilesChanged(null, null);
    }

    /// <summary>Shows one of the left panels and loads its content, as the activity rail asks.</summary>
    /// <param name="panel">The panel to show.</param>
    public void ShowPanel(SidebarPanel panel)
    {
        ExplorerPanel.Visibility = ExplorerGlyphs.Show(panel == SidebarPanel.Explorer);
        HistoryPanel.Visibility = ExplorerGlyphs.Show(panel == SidebarPanel.History);
        SnippetsPanel.Visibility = ExplorerGlyphs.Show(panel == SidebarPanel.Snippets);
        if (panel == SidebarPanel.History)
            HistoryPanel.Reload();
        else if (panel == SidebarPanel.Snippets)
            SnippetsPanel.Reload();
    }

    private ExplorerNode? _lastSelected;
    private ExplorerNode? _anchor;
    private List<ExplorerNode> _model = [];

    private ConnectionListViewModel List => _workspace!.ConnectionList;

    private void OnProfilesChanged(object? sender, NotifyCollectionChangedEventArgs? e) =>
        EmptyHint.Visibility = List.Profiles.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

    private void OnTreeKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (_workspace is null)
            return;
        var explorer = _workspace.Explorer;
        // Navigation keys only (a modifier key is a KeyDown too): the native selection is copied without the children WinUI selects with a parent.
        if (e.Key is VirtualKey.Up or VirtualKey.Down or VirtualKey.Left or VirtualKey.Right or VirtualKey.Space or VirtualKey.Home or VirtualKey.End or VirtualKey.PageUp or VirtualKey.PageDown)
            DispatcherQueue.TryEnqueue(() =>
            {
                var native = Tree.SelectedItems.OfType<ExplorerNode>().ToList();
                var descendants = new HashSet<ExplorerNode>();
                foreach (var n in native)
                    AddDescendants(n, descendants);
                _model = [.. native.Where(n => !descendants.Contains(n))];
                SyncSelection();
            });
        if (e.Key == VirtualKey.Delete && explorer.DropSelectedTablesCommand.CanExecute(null))
        {
            explorer.DropSelectedTablesCommand.Execute(null);
            e.Handled = true;
        }
        else if (e.Key == VirtualKey.Enter && _lastSelected is { Kind: ExplorerNodeKind.Connection } node)
        {
            explorer.ActivateCommand.Execute(node);
            e.Handled = true;
        }
    }

    private void OnRefreshClicked(object sender, RoutedEventArgs e) => _workspace?.Explorer.RefreshCommand.Execute(null);

    private void OnCollapseAllClicked(object sender, RoutedEventArgs e)
    {
        foreach (var root in _workspace?.Explorer.Roots ?? [])
            Collapse(root);
    }

    private static void Collapse(ExplorerNode node)
    {
        node.IsExpanded = false;
        foreach (var child in node.Children)
            Collapse(child);
    }

    private void OnTreeSelectionChanged(TreeView sender, TreeViewSelectionChangedEventArgs e)
    {
        var node = e.AddedItems.OfType<ExplorerNode>().LastOrDefault();
        if (node is not null)
            _workspace!.Explorer.SelectedNode = node;
        _lastSelected = node ?? Tree.SelectedItems.OfType<ExplorerNode>().LastOrDefault();
        SelectedNodeChanged?.Invoke(this, node);
    }

    // In Multiple mode WinUI selects a clicked row on press and drops it on release, so the mouse selection is rebuilt from the
    // button release: plain click = that row alone, Ctrl = toggle it, Shift = range from the anchor. _model is the selection the
    // user built; the keyboard stays native and is copied into it.
    private void OnTreePointerReleased(object sender, PointerRoutedEventArgs e)
    {
        if (_workspace is null || e.GetCurrentPoint(Tree).Properties.PointerUpdateKind != Microsoft.UI.Input.PointerUpdateKind.LeftButtonReleased)
            return;
        ExplorerNode? node = null;
        for (var d = e.OriginalSource as DependencyObject; d is not null && node is null; d = Microsoft.UI.Xaml.Media.VisualTreeHelper.GetParent(d))
        {
            if (d is FrameworkElement { Name: "ExpandCollapseChevron" })
                return;
            node = (d as FrameworkElement)?.DataContext as ExplorerNode;
            if (d == Tree)
                return;
        }
        if (node is null)
            return;
        var ctrl = IsDown(VirtualKey.Control);
        var shift = IsDown(VirtualKey.Shift);
        List<ExplorerNode> next;
        if (shift && _anchor is not null)
        {
            var rows = new List<ExplorerNode>();
            Flatten(_workspace.Explorer.Roots, rows);
            int a = rows.IndexOf(_anchor), b = rows.IndexOf(node);
            next = a >= 0 && b >= 0 ? rows.GetRange(Math.Min(a, b), Math.Abs(a - b) + 1) : [node];
        }
        else if (ctrl)
        {
            next = [.. _model];
            if (!next.Remove(node))
                next.Add(node);
            _anchor = node;
        }
        else
        {
            next = [node];
            _anchor = node;
        }
        DispatcherQueue.TryEnqueue(() => SetSelection(next));
    }

    private void SetSelection(List<ExplorerNode> nodes)
    {
        _model = nodes;
        Tree.SelectedItems.Clear();
        foreach (var n in nodes)
            Tree.SelectedItems.Add(n);
        SyncSelection();
    }

    private static void AddDescendants(ExplorerNode node, HashSet<ExplorerNode> into)
    {
        foreach (var child in node.Children)
        {
            into.Add(child);
            AddDescendants(child, into);
        }
    }

    private static void Flatten(IEnumerable<ExplorerNode> nodes, List<ExplorerNode> rows)
    {
        foreach (var n in nodes.Where(n => n.IsVisible))
        {
            rows.Add(n);
            if (n.IsExpanded)
                Flatten(n.Children, rows);
        }
    }

    // The view-model gets the selection the user built (_model), not Tree.SelectedItems, to which WinUI may add parent nodes.
    private void SyncSelection()
    {
        _lastSelected = _model.LastOrDefault();
        _workspace?.Explorer.SetSelectedNodes(_model);
        if (_lastSelected is not null)
            _workspace!.Explorer.SelectedNode = _lastSelected;
    }

    private static bool IsDown(VirtualKey key) =>
        InputKeyboardSource.GetKeyStateForCurrentThread(key).HasFlag(Windows.UI.Core.CoreVirtualKeyStates.Down);

    private void OnNewClicked(object sender, RoutedEventArgs e) => List.NewCommand.Execute(null);

    private void OnFilterChanged(object sender, TextChangedEventArgs e)
    {
        if (_workspace is not null)
            _workspace.Explorer.FilterText = Filter.Text;
    }

    private void OnTreeDoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
    {
        if (_workspace is null || _lastSelected is not { } node)
            return;
        if (node.Kind is ExplorerNodeKind.View or ExplorerNodeKind.MaterializedView or ExplorerNodeKind.Function or ExplorerNodeKind.Procedure or ExplorerNodeKind.Trigger)
            _workspace.Explorer.OpenDefinitionCommand.Execute(node);
        else
            _workspace.Explorer.ActivateCommand.Execute(node);
    }

    // The flyout is built per request: connection nodes get the connection commands, tables Design/Drop, other nodes no menu.
    private void OnNodeContextRequested(UIElement sender, ContextRequestedEventArgs e)
    {
        if (_workspace is null || (sender as FrameworkElement)?.DataContext is not ExplorerNode node)
            return;
        var explorer = _workspace.Explorer;
        var menu = new MenuFlyout();
        void Add(string text, Action action)
        {
            var item = new MenuFlyoutItem { Text = text };
            item.Click += (_, _) => action();
            menu.Items.Add(item);
        }

        // A right click does not select the node, and the commands below act on the selection.
        // A right click inside a multi-selection keeps it.
        if (node.Kind is ExplorerNodeKind.Connection or ExplorerNodeKind.Table && !Tree.SelectedItems.Contains(node))
            SetSelection([node]);
        if (node.Kind == ExplorerNodeKind.Connection)
        {
            Add(UiText.Get("explorer.newConnection"), () => List.NewCommand.Execute(null));
            Add(UiText.Get("explorer.edit"), () => List.EditCommand.Execute(null));
            Add(UiText.Get("explorer.delete"), () => List.DeleteCommand.Execute(null));
            if (node.IsConnected)
            {
                Add(UiText.Get("explorer.compareSchemas"), () => _workspace.OpenSchemaCompareCommand.Execute(node.Profile?.Id));
                var compare = (MenuFlyoutItem)menu.Items[^1];
                compare.IsEnabled = _workspace.OpenSchemaCompareCommand.CanExecute(node.Profile?.Id);
                if (!compare.IsEnabled)
                    ToolTipService.SetToolTip(compare, _workspace.SchemaCompareDisabledReason);
                Add(UiText.Get("explorer.sessionsMonitor"), () => _workspace.OpenSessionsMonitorCommand.Execute(null));
                var monitor = (MenuFlyoutItem)menu.Items[^1];
                monitor.IsEnabled = _workspace.OpenSessionsMonitorCommand.CanExecute(null);
                if (!monitor.IsEnabled)
                    ToolTipService.SetToolTip(monitor, _workspace.SessionsMonitorDisabledReason);
                Add(UiText.Get("explorer.security"), () => _workspace.OpenSecurityCommand.Execute(null));
                var security = (MenuFlyoutItem)menu.Items[^1];
                security.IsEnabled = _workspace.OpenSecurityCommand.CanExecute(null);
                if (!security.IsEnabled)
                    ToolTipService.SetToolTip(security, _workspace.SecurityDisabledReason);
                Add(UiText.Get("explorer.disconnect"), () => _workspace.DisconnectCommand.Execute(null));
            }
            else
                Add(UiText.Get("explorer.connect"), () => _workspace.ConnectCommand.Execute(node.Profile));
        }
        else if (node is { Kind: ExplorerNodeKind.Database or ExplorerNodeKind.Schema, Session: not null })
        {
            Add(UiText.Get("explorer.chooseTables"), () => explorer.ChooseTablesCommand.Execute(node));
            if (node is { Kind: ExplorerNodeKind.Database, HiddenCount: > 0 })
                Add(UiText.Get("explorer.showAllTables"), () => explorer.ShowAllTablesCommand.Execute(node));
            if (node.Kind == ExplorerNodeKind.Database)
                Add(UiText.Get("explorer.refresh"), () => explorer.RefreshDatabaseCommand.Execute(node));
        }
        else if (node.Kind == ExplorerNodeKind.Table)
        {
            var single = explorer.SelectedTableCount <= 1;
            Add(UiText.Get("explorer.designTable"), () => explorer.DesignCommand.Execute(node));
            menu.Items[^1].IsEnabled = single;
            Add(UiText.Get("explorer.scriptCreateTable"), () => explorer.ScriptCreateTableCommand.Execute(node));
            menu.Items[^1].IsEnabled = single;
            Add(explorer.DropMenuText(node), () => explorer.DropCommand.Execute(node));
            menu.Items[^1].IsEnabled = explorer.CanDropMenu(node);
            Add(UiText.Get("explorer.hideTable"), () => explorer.HideTableCommand.Execute(node));
        }
        else if (node.Kind is ExplorerNodeKind.View or ExplorerNodeKind.MaterializedView or ExplorerNodeKind.Function or ExplorerNodeKind.Procedure or ExplorerNodeKind.Trigger)
        {
            Add(UiText.Get("explorer.openDefinition"), () => explorer.OpenDefinitionCommand.Execute(node));
            if (node.Table is not null)
                Add(UiText.Get("explorer.hideTable"), () => explorer.HideTableCommand.Execute(node));
        }
        else
        {
            return;
        }
        if (e.TryGetPosition(sender, out var position))
            menu.ShowAt(sender, new FlyoutShowOptions { Position = position });
        else
            menu.ShowAt((FrameworkElement)sender);
        e.Handled = true;
    }
}
