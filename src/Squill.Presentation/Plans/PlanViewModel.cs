using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Squill.Core;
using Squill.Core.Plans;
using Squill.Presentation.Localization;

namespace Squill.Presentation.Plans;

/// <summary>Everything the plan screen shows, as state both heads bind to: summary figures, warning pills, table rows, selected node details and raw text.</summary>
public sealed partial class PlanViewModel : ViewModelBase
{
    private const double MinZoom = 0.25;
    private const double MaxZoom = 2;
    private const double ZoomStep = 0.1;

    private PlanViewModel(QueryPlan plan, PlanAnalysisResult analysis)
    {
        Plan = plan;
        Analysis = analysis;
        var rows = new List<PlanRowViewModel>();
        Collect(plan.Root, 0);
        Rows = rows;
        ViewMode = PlanViewMode.Graph;
        SelectedRow = rows.Where(r => r.RelativeShare is > 0).MaxBy(r => r.RelativeShare) ?? rows[0];
        Details = new PlanDetailsViewModel(SelectedRow, plan);

        var summary = new List<PlanFigure>();
        if (plan.ExecutionTimeMs is { } time)
            summary.Add(new PlanFigure(UiText.Get("plan.figure.duration"), PlanText.Milliseconds(time)));
        if (plan.Root.EstimatedCost is { } cost)
            summary.Add(new PlanFigure(UiText.Get("plan.figure.cost"), PlanText.Cost(cost)));
        if (plan.BuffersReadBytes is { } bytes)
            summary.Add(new PlanFigure(UiText.Get("plan.figure.buffers"), PlanText.Size(bytes)));
        Summary = summary;

        CommandText = (plan.Dialect, plan.Mode) switch
        {
            (DatabaseDialect.PostgreSql, PlanMode.Estimated) => "EXPLAIN (FORMAT JSON)",
            (DatabaseDialect.PostgreSql, _) => "EXPLAIN (ANALYZE, BUFFERS)",
            (DatabaseDialect.MySql, PlanMode.Estimated) => "EXPLAIN FORMAT=JSON",
            (DatabaseDialect.MySql, _) => "EXPLAIN ANALYZE",
            (DatabaseDialect.SqlServer, PlanMode.Estimated) => "SHOWPLAN_XML",
            (DatabaseDialect.SqlServer, _) => "STATISTICS XML",
            (DatabaseDialect.Sqlite, _) => "EXPLAIN QUERY PLAN",
            _ => "EXPLAIN",
        };
        CriticalCountText = analysis.CriticalCount > 0 ? UiText.FormatPlural("plan.critical", analysis.CriticalCount) : "";
        WarningCountText = analysis.WarningCount > 0 ? UiText.FormatPlural("status.warnings", analysis.WarningCount) : "";
        NodeCountText = UiText.FormatPlural("plan.nodes", rows.Count);
        StatusLine = plan.Mode == PlanMode.Estimated ? UiText.Get("plan.status.estimated")
            : UiText.Format(plan.RolledBack ? "plan.status.rolledBack" : "plan.status.analyzed", CommandText);

        void Collect(PlanNode node, int depth)
        {
            rows.Add(new PlanRowViewModel(node, depth, analysis.Nodes[node.Id]));
            foreach (var child in node.Children)
                Collect(child, depth + 1);
        }
    }

    /// <summary>Gets the plan shown.</summary>
    public QueryPlan Plan { get; }

    /// <summary>Gets the analysis of the plan (shares, severities, counters), computed once.</summary>
    public PlanAnalysisResult Analysis { get; }

    /// <summary>Gets the representation shown; <see cref="PlanViewMode.Tree"/> and <see cref="PlanViewMode.Graph"/> both show the table, <see cref="PlanViewMode.Text"/> shows <see cref="SourceText"/>.</summary>
    [ObservableProperty]
    public partial PlanViewMode ViewMode { get; set; }

    /// <summary>Gets one row per node in pre-order, helpers included.</summary>
    public IReadOnlyList<PlanRowViewModel> Rows { get; }

    /// <summary>Gets or sets the selected row; the default is the node with the highest share, else the root.</summary>
    [ObservableProperty]
    public partial PlanRowViewModel SelectedRow { get; set; }

    /// <summary>Gets the details of <see cref="SelectedRow"/>.</summary>
    [ObservableProperty]
    public partial PlanDetailsViewModel Details { get; private set; }

    /// <summary>Gets the figures of the toolbar; each one the plan lacks is omitted.</summary>
    public IReadOnlyList<PlanFigure> Summary { get; }

    /// <summary>Gets the engine statement family that produced the plan (<c>EXPLAIN (ANALYZE, BUFFERS)</c>).</summary>
    public string CommandText { get; }

    /// <summary>Gets the critical-node pill text (<c>1 nœud critique</c>), empty when there is none.</summary>
    public string CriticalCountText { get; }

    /// <summary>Gets the warning pill text (<c>2 avertissements</c>), empty when there is none.</summary>
    public string WarningCountText { get; }

    /// <summary>Gets a value indicating whether the critical pill is shown.</summary>
    public bool HasCritical => CriticalCountText.Length > 0;

    /// <summary>Gets a value indicating whether the warning pill is shown.</summary>
    public bool HasWarnings => WarningCountText.Length > 0;

    /// <summary>Gets the node count text (<c>10 nœuds</c>).</summary>
    public string NodeCountText { get; }

    /// <summary>Gets the status bar sentence: estimated plan, analyzed in a rolled-back transaction, or analyzed.</summary>
    public string StatusLine { get; }

    /// <summary>Gets the raw engine output for the text view.</summary>
    public string SourceText => Plan.SourceText;

    /// <summary>Gets or sets the zoom of the graph, kept between 0.25 and 2.</summary>
    public double Zoom
    {
        get;
        set
        {
            if (SetProperty(ref field, Math.Clamp(Math.Round(value, 2), MinZoom, MaxZoom)))
                OnPropertyChanged(nameof(ZoomText));
        }
    } = 1;

    /// <summary>Gets the zoom as a percentage (<c>100 %</c>).</summary>
    public string ZoomText => string.Create(UiText.Culture, $"{Zoom * 100:0} %");

    /// <summary>Builds the view-model of a plan; the analysis runs once.</summary>
    /// <param name="plan">The plan read from the engine.</param>
    /// <returns>The view-model.</returns>
    public static PlanViewModel Create(QueryPlan plan)
    {
        ArgumentNullException.ThrowIfNull(plan);
        return new PlanViewModel(plan, PlanAnalysis.Analyze(plan));
    }

    partial void OnSelectedRowChanged(PlanRowViewModel value) => Details = new PlanDetailsViewModel(value, Plan);

    [RelayCommand]
    private void ZoomIn() => Zoom += ZoomStep;

    [RelayCommand]
    private void ZoomOut() => Zoom -= ZoomStep;

    [RelayCommand]
    private void ResetZoom() => Zoom = 1;
}
