using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Squill.Core;
using Squill.Presentation.Designer;
using Squill.Presentation.Diagram;
using Squill.Presentation.Plans;
using Squill.Presentation.Query;
using Squill.Presentation.Terminal;
using Squill.Presentation.Workspace;

namespace Squill.App.Avalonia.Views;

/// <summary>One entry of the tab strip: a query, terminal, diagram, plan or table designer tab of the workspace.</summary>
public sealed partial class WorkspaceTab : ObservableObject
{
    /// <summary>Creates the entry.</summary>
    /// <param name="source">The tab view-model this entry stands for.</param>
    public WorkspaceTab(object source)
    {
        Source = source;
        Refresh();
    }

    /// <summary>Gets the tab view-model behind the entry.</summary>
    public object Source { get; }

    /// <summary>Gets the title shown on the tab.</summary>
    [ObservableProperty]
    public partial string Title { get; private set; } = "";

    /// <summary>Gets the muted text after the title (the localized " · Plan" of a plan tab), empty for other tabs.</summary>
    [ObservableProperty]
    public partial string Suffix { get; private set; } = "";

    /// <summary>Gets whether <see cref="Suffix"/> is shown.</summary>
    public bool HasSuffix => Suffix.Length > 0;

    /// <summary>Gets the environment of the tab's connection, <see cref="ConnectionEnvironment.None"/> for terminals.</summary>
    public ConnectionEnvironment Environment { get; private set; }

    /// <summary>Gets whether the tab holds unsaved work (modified script, undeployed table design).</summary>
    [ObservableProperty]
    public partial bool IsDirty { get; private set; }

    /// <summary>Gets whether the tab's connection is a production one.</summary>
    public bool IsProd => Environment == ConnectionEnvironment.Production;

    /// <summary>Gets whether the tab's connection is a staging one.</summary>
    public bool IsStaging => Environment == ConnectionEnvironment.Staging;

    /// <summary>Gets whether the tab's connection is a development one.</summary>
    public bool IsDev => Environment == ConnectionEnvironment.Development;

    /// <summary>Gets whether the tab's connection is a local one.</summary>
    public bool IsLocal => Environment == ConnectionEnvironment.Local;

    /// <summary>Re-reads the title and the dirty flag from the view-model.</summary>
    internal void Refresh()
    {
        (Title, Environment, IsDirty) = Source switch
        {
            QueryTabViewModel q => (q.Title, q.Session.Profile.Environment, q.IsModified),
            TableDesignerViewModel d => (d.Title, d.Session.Profile.Environment, d.HasChanges),
            DiagramTabViewModel g => (g.Title, g.Session.Profile.Environment, false),
            PlanTabViewModel p => (p.Source.Title, p.Environment, false),
            TerminalTabViewModel t => (t.Title, ConnectionEnvironment.None, false),
            _ => ("", ConnectionEnvironment.None, false),
        };
        Suffix = Source is PlanTabViewModel plan && plan.Title.StartsWith(Title, StringComparison.Ordinal) ? plan.Title[Title.Length..] : "";
        OnPropertyChanged(nameof(HasSuffix));
    }
}

/// <summary>
/// The tab lists of the workspace merged into one for the tab strip: queries, terminals, diagrams, plans, then designers.
/// Keeps the selection in step with the workspace's active tab, designer and diagram.
/// </summary>
public sealed partial class WorkspaceTabList : ObservableObject
{
    private readonly WorkspaceViewModel _workspace;
    private readonly Dictionary<object, WorkspaceTab> _bySource = [];

    /// <summary>Creates the list and fills it from the workspace.</summary>
    /// <param name="workspace">The workspace whose tabs are merged.</param>
    public WorkspaceTabList(WorkspaceViewModel workspace)
    {
        ArgumentNullException.ThrowIfNull(workspace);
        _workspace = workspace;
        workspace.Tabs.CollectionChanged += OnSourcesChanged;
        workspace.Terminals.CollectionChanged += OnSourcesChanged;
        workspace.Diagrams.CollectionChanged += OnSourcesChanged;
        workspace.Plans.CollectionChanged += OnSourcesChanged;
        workspace.Designers.CollectionChanged += OnSourcesChanged;
        workspace.PropertyChanged += OnWorkspaceChanged;
        Sync(null);
    }

    /// <summary>Gets the merged tabs in strip order.</summary>
    public ObservableCollection<WorkspaceTab> Items { get; } = [];

    /// <summary>Gets or sets the selected tab; setting it activates the matching query, designer or diagram in the workspace.</summary>
    [ObservableProperty]
    public partial WorkspaceTab? Selected { get; set; }

    partial void OnSelectedChanged(WorkspaceTab? value)
    {
        _workspace.ShowTab(value?.Source);
        switch (value?.Source)
        {
            case QueryTabViewModel query:
                _workspace.ActiveTab = query;
                _workspace.ActivePlan = null; // lets re-opening the same plan tab raise a change
                break;
            case PlanTabViewModel plan:
                _workspace.ActivePlan = plan;
                break;
            case TableDesignerViewModel designer:
                _workspace.ActiveDesigner = designer;
                break;
            case DiagramTabViewModel diagram:
                _workspace.ActiveDiagram = diagram;
                break;
        }
    }

    /// <summary>Closes a tab through the workspace's own close command, which may ask for confirmation.</summary>
    /// <param name="tab">The tab to close.</param>
    public void Close(WorkspaceTab tab)
    {
        switch (tab.Source)
        {
            case QueryTabViewModel query:
                _workspace.CloseTabCommand.Execute(query);
                break;
            case TableDesignerViewModel designer:
                _workspace.CloseDesignerCommand.Execute(designer);
                break;
            case DiagramTabViewModel diagram:
                _workspace.CloseDiagramCommand.Execute(diagram);
                break;
            case PlanTabViewModel plan:
                plan.CloseCommand.Execute(null);
                break;
            case TerminalTabViewModel terminal:
                _workspace.CloseTerminalCommand.Execute(terminal);
                break;
        }
    }

    private void OnWorkspaceChanged(object? sender, PropertyChangedEventArgs e)
    {
        object? source = e.PropertyName switch
        {
            nameof(WorkspaceViewModel.ActiveTab) => _workspace.ActiveTab,
            nameof(WorkspaceViewModel.ActiveDesigner) => _workspace.ActiveDesigner,
            nameof(WorkspaceViewModel.ActiveDiagram) => _workspace.ActiveDiagram,
            nameof(WorkspaceViewModel.ActivePlan) => _workspace.ActivePlan,
            _ => null,
        };
        if (source is not null && _bySource.TryGetValue(source, out var tab))
            Selected = tab;
    }

    private void OnSourcesChanged(object? sender, NotifyCollectionChangedEventArgs e) =>
        Sync(e.NewItems?.OfType<TerminalTabViewModel>().LastOrDefault());

    private void OnSourcePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (sender is not null && _bySource.TryGetValue(sender, out var tab))
            tab.Refresh();
    }

    private void Sync(TerminalTabViewModel? addedTerminal)
    {
        object[] desired =
        [
            .. _workspace.Tabs,
            .. _workspace.Terminals,
            .. _workspace.Diagrams,
            .. _workspace.Plans,
            .. _workspace.Designers,
        ];
        foreach (var stale in _bySource.Keys.Where(k => !desired.Contains(k)).ToList())
        {
            var tab = _bySource[stale];
            Detach(stale);
            _bySource.Remove(stale);
            Items.Remove(tab);
        }

        for (var i = 0; i < desired.Length; i++)
        {
            if (!_bySource.TryGetValue(desired[i], out var tab))
            {
                tab = new WorkspaceTab(desired[i]);
                _bySource[desired[i]] = tab;
                Attach(desired[i]);
                Items.Insert(i, tab);
            }
            else if (Items.IndexOf(tab) is var at && at != i)
            {
                Items.Move(at, i);
            }
        }

        if (addedTerminal is not null)
            Selected = _bySource[addedTerminal];
        else if (Selected is not null && !Items.Contains(Selected))
            Selected = _workspace.ActiveTab is { } active && _bySource.GetValueOrDefault(active) is { } current ? current : Items.LastOrDefault();
    }


    private void Attach(object source)
    {
        if (source is INotifyPropertyChanged changed)
            changed.PropertyChanged += OnSourcePropertyChanged;
        if (source is QueryTabViewModel query)
            query.Editor.TextChanged += (_, _) => _bySource.GetValueOrDefault(query)?.Refresh();
    }

    private void Detach(object source)
    {
        if (source is INotifyPropertyChanged changed)
            changed.PropertyChanged -= OnSourcePropertyChanged;
    }
}
