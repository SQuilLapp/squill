using Squill.Core;
using Squill.Core.Plans;
using Squill.Presentation.Localization;
using Squill.Presentation.Plans;
using Xunit;

namespace Squill.Presentation.Tests.Plans;

public sealed class PlanViewModelTests
{
    private static PlanNode Node(
        string operation, PlanNodeKind kind, string? target = null, double? estRows = null, double? cost = null, double? actualRows = null, double? time = null, long? loops = null,
        IReadOnlyList<PlanProperty>? properties = null, IReadOnlyList<PlanWarning>? warnings = null, string? detail = null, IReadOnlyList<PlanNode>? children = null) =>
        new(0, operation, kind, target, detail, estRows, cost, null, null, actualRows, time, loops, properties ?? [], warnings ?? [], false, children ?? []);

    // PostgreSQL-shaped: Limit > Sort > Hash Join > (Seq Scan on lignes_commande 64 %, Seq Scan on clients 0.4 %).
    private static QueryPlan Postgres(bool rolledBack = true) => new(
        DatabaseDialect.PostgreSql, PlanMode.Actual, "select 1",
        PlanIds.Assign(Node("Limit", PlanNodeKind.Limit, cost: 1234.5, time: 1000, actualRows: 5, loops: 1, children:
            [Node("Sort", PlanNodeKind.Sort, time: 990, actualRows: 5, loops: 1, children:
                [Node("Hash Join", PlanNodeKind.Join, time: 970, actualRows: 5, loops: 1, children:
                [
                    Node("Seq Scan", PlanNodeKind.TableScan, "lignes_commande", estRows: 10, actualRows: 5900, time: 640, loops: 1, detail: "(prix > 3)",
                        properties: [new PlanProperty("Rows Removed by Filter", "12"), new PlanProperty("Workers", "2"), new PlanProperty("Foo", "bar"), new PlanProperty("Filter", "(prix > 3)")]),
                    Node("Seq Scan", PlanNodeKind.TableScan, "clients", estRows: 4, actualRows: 4, time: 4, loops: 1),
                ])])])),
        [], [], 0.3, 1182, 431L * 1024 * 1024, rolledBack, PlanSourceFormat.Json, "{}");

    // SQLite-shaped: the engine gives no cost, estimate or timing at all.
    private static QueryPlan Sqlite() => new(
        DatabaseDialect.Sqlite, PlanMode.Estimated, "select 1",
        PlanIds.Assign(Node("SCAN", PlanNodeKind.Result, children: [Node("SEARCH", PlanNodeKind.IndexSeek, "orders"), Node("SCAN", PlanNodeKind.TableScan, "customers")])),
        [], [], null, null, null, false, PlanSourceFormat.Text, "SCAN");

    private static QueryPlan SqlServer() => new(
        DatabaseDialect.SqlServer, PlanMode.Estimated, "select 1",
        PlanIds.Assign(Node("Clustered Index Scan", PlanNodeKind.TableScan, "[sample].[orders]", estRows: 21, cost: 1.02)),
        [new PlanWarning(PlanMessageCodes.MissingIndex, ["99.6775"]), new PlanWarning(PlanMessageCodes.ImplicitConversion, ["CONVERT_IMPLICIT(int,[a],0)"])],
        [new PlanSuggestion("Missing index", "CREATE INDEX [IX_a] ON [sample].[orders] ([total]);")], 0, null, null, false, PlanSourceFormat.Xml, "<x/>");

    [Fact]
    public void Default_SelectsTheNodeWithTheHighestShare_AndTheGraphMode()
    {
        var vm = PlanViewModel.Create(Postgres());
        Assert.Equal(PlanViewMode.Graph, vm.ViewMode);
        Assert.Equal("lignes_commande", vm.SelectedRow.Target);
        Assert.Equal(PlanSeverity.Critical, vm.SelectedRow.Severity);
        Assert.Equal("{}", vm.SourceText);
    }

    [Fact]
    public void ColumnsShown_HidesColumnsWithoutFigures_AndCellsFollowTheColumnOrder()
    {
        var sqlite = PlanViewModel.Create(Sqlite());
        Assert.Equal([true, false, false, false, false, false, false], sqlite.ColumnsShown);

        var postgres = PlanViewModel.Create(Postgres());
        Assert.True(postgres.ColumnsShown[1] && postgres.ColumnsShown[2] && postgres.ColumnsShown[5]);
        var row = postgres.Rows[0];
        Assert.Equal([row.TotalCost, row.EstimatedRows, row.ActualRows, row.Time, row.Loops], row.NumericCells);
    }

    [Fact]
    public void NoFigures_SelectsTheRoot_InventsNothing_AndShowsNoPills()
    {
        var vm = PlanViewModel.Create(Sqlite());
        Assert.Same(vm.Rows[0], vm.SelectedRow);
        Assert.Empty(vm.Summary);
        Assert.False(vm.HasCritical);
        Assert.False(vm.HasWarnings);
        Assert.Equal("EXPLAIN QUERY PLAN", vm.CommandText);
        Assert.All(vm.Rows, r =>
        {
            Assert.Equal(PlanText.Missing, r.RelativeText);
            Assert.Null(r.RelativeShare);
            Assert.Equal(PlanText.Missing, r.TotalCost);
            Assert.Equal(PlanText.Missing, r.EstimatedRows);
            Assert.Equal(PlanText.Missing, r.ActualRows);
            Assert.Equal(PlanText.Missing, r.Time);
            Assert.Equal(PlanText.Missing, r.Loops);
            Assert.Equal("", r.MisestimateText);
        });
        Assert.Equal("", vm.Details.ShareText);
        Assert.Equal("Plan estimé : l'instruction n'a pas été exécutée", vm.StatusLine);
    }

    [Fact]
    public void Summary_ListsOnlyTheFiguresThePlanHas()
    {
        var full = PlanViewModel.Create(Postgres());
        Assert.Equal(["Durée totale", "Coût", "Tampons lus"], full.Summary.Select(f => f.Label));
        Assert.Equal(PlanText.Milliseconds(1182), full.Summary[0].Value);
        Assert.Equal(1234.5.ToString("#,##0.##", UiText.Culture), full.Summary[1].Value);
        Assert.Equal(UiText.Format("plan.size.mb", "431"), full.Summary[2].Value);

        var estimated = PlanViewModel.Create(SqlServer());
        Assert.Equal(["Coût"], estimated.Summary.Select(f => f.Label));
    }

    [Fact]
    public void Pills_ArePluralInBothLanguages()
    {
        var vm = PlanViewModel.Create(Postgres());
        Assert.Equal("1 nœud critique", vm.CriticalCountText);
        Assert.Equal("1 avertissement", vm.WarningCountText);
        Assert.Equal("5 nœuds", vm.NodeCountText);
        var sqlServer = PlanViewModel.Create(SqlServer());
        Assert.Equal("2 avertissements", sqlServer.WarningCountText);
        Assert.Equal("1 nœud", sqlServer.NodeCountText);

        using (UiText.Override(UiLanguage.English))
        {
            var english = PlanViewModel.Create(Postgres());
            Assert.Equal("1 critical node", english.CriticalCountText);
            Assert.Equal("1 warning", english.WarningCountText);
            Assert.Equal("5 nodes", english.NodeCountText);
            Assert.Equal("2 warnings", PlanViewModel.Create(SqlServer()).WarningCountText);
        }
    }

    [Fact]
    public void StatusLine_DependsOnModeAndRollback()
    {
        Assert.Equal("EXPLAIN (ANALYZE, BUFFERS) exécuté dans une transaction annulée", PlanViewModel.Create(Postgres()).StatusLine);
        Assert.Equal("EXPLAIN (ANALYZE, BUFFERS) exécuté", PlanViewModel.Create(Postgres(rolledBack: false)).StatusLine);
        Assert.Equal("Plan estimé : l'instruction n'a pas été exécutée", PlanViewModel.Create(SqlServer()).StatusLine);
        using (UiText.Override(UiLanguage.English))
            Assert.Equal("EXPLAIN (ANALYZE, BUFFERS) run in a rolled-back transaction", PlanViewModel.Create(Postgres()).StatusLine);
    }

    [Theory]
    [InlineData(DatabaseDialect.PostgreSql, PlanMode.Estimated, "EXPLAIN (FORMAT JSON)")]
    [InlineData(DatabaseDialect.PostgreSql, PlanMode.Actual, "EXPLAIN (ANALYZE, BUFFERS)")]
    [InlineData(DatabaseDialect.MySql, PlanMode.Estimated, "EXPLAIN FORMAT=JSON")]
    [InlineData(DatabaseDialect.MySql, PlanMode.Actual, "EXPLAIN ANALYZE")]
    [InlineData(DatabaseDialect.SqlServer, PlanMode.Estimated, "SHOWPLAN_XML")]
    [InlineData(DatabaseDialect.SqlServer, PlanMode.Actual, "STATISTICS XML")]
    public void CommandText_FollowsTheEngineAndMode(DatabaseDialect dialect, PlanMode mode, string expected)
    {
        var plan = Sqlite() with { Dialect = dialect, Mode = mode };
        Assert.Equal(expected, PlanViewModel.Create(plan).CommandText);
    }

    [Fact]
    public void Zoom_StaysBetweenAQuarterAndDouble()
    {
        var vm = PlanViewModel.Create(Sqlite());
        Assert.Equal("100 %", vm.ZoomText);
        for (var i = 0; i < 30; i++)
            vm.ZoomOutCommand.Execute(null);
        Assert.Equal(0.25, vm.Zoom);
        Assert.Equal("25 %", vm.ZoomText);
        for (var i = 0; i < 30; i++)
            vm.ZoomInCommand.Execute(null);
        Assert.Equal(2, vm.Zoom);
        Assert.Equal("200 %", vm.ZoomText);
        vm.ResetZoomCommand.Execute(null);
        Assert.Equal(1, vm.Zoom);
        vm.Zoom = 50;
        Assert.Equal(2, vm.Zoom);
    }

    [Fact]
    public void Details_TitleAndProperties_AreTranslatedInOrder()
    {
        var vm = PlanViewModel.Create(Postgres());
        Assert.Equal("Seq Scan sur lignes_commande", vm.Details.Title);
        Assert.Equal(
            ["Prédicat", "Lignes écartées", "Workers", "Filtre", "Foo"],
            vm.Details.Properties.Select(p => p.Name));
        Assert.Equal("(prix > 3)", vm.Details.Properties[0].Value);
        Assert.Equal("64 % du temps total", vm.Details.ShareText);
        Assert.Single(vm.Details.Warnings);
        Assert.Contains("590", vm.Details.Warnings[0], StringComparison.Ordinal);
        Assert.Contains("basse", vm.Details.Warnings[0], StringComparison.Ordinal);

        vm.SelectedRow = vm.Rows[0];
        Assert.Equal("Limit", vm.Details.Title);

        using (UiText.Override(UiLanguage.English))
        {
            var english = PlanViewModel.Create(Postgres());
            Assert.Equal("Seq Scan on lignes_commande", english.Details.Title);
            Assert.Equal("Rows removed", english.Details.Properties[1].Name);
            Assert.Contains("low", english.Details.Warnings[0], StringComparison.Ordinal);
        }
    }

    [Fact]
    public void Rows_ArePreOrderWithDepth_AndPercentagesRound()
    {
        var vm = PlanViewModel.Create(Postgres());
        Assert.Equal(["Limit", "Sort", "Hash Join", "Seq Scan", "Seq Scan"], vm.Rows.Select(r => r.Label));
        Assert.Equal([0, 1, 2, 3, 3], vm.Rows.Select(r => r.Depth));
        Assert.Equal("64 %", vm.Rows[3].RelativeText);
        Assert.Equal("0 %", vm.Rows[4].RelativeText);
        Assert.Equal("x590", vm.Rows[3].MisestimateText);
        Assert.Equal("5 900", vm.Rows[3].ActualRows.Replace(' ', ' ').Replace(' ', ' '));
        Assert.Equal(PlanText.Milliseconds(640), vm.Rows[3].Time);
    }

    [Fact]
    public void SqlServerSuggestion_IsShownWithItsCopyText()
    {
        var vm = PlanViewModel.Create(SqlServer());
        Assert.True(vm.Details.HasSuggestions);
        Assert.Equal("CREATE INDEX [IX_a] ON [sample].[orders] ([total]);", vm.Details.CopyText);
        Assert.False(PlanViewModel.Create(Postgres()).Details.HasSuggestions);
    }

    [Theory]
    [InlineData(0.64, "64 %")]
    [InlineData(0.004, "0 %")]
    [InlineData(0.005, "1 %")]
    [InlineData(1, "100 %")]
    public void Percent_RoundsToAWholeNumber(double share, string expected) => Assert.Equal(expected, PlanText.Percent(share));

    [Fact]
    public void Milliseconds_KeepsASubMillisecondFraction()
    {
        Assert.Equal("0,4 ms", PlanText.Milliseconds(0.4));
        Assert.Equal("–", PlanText.Milliseconds(null));
        using (UiText.Override(UiLanguage.English))
            Assert.Equal("0.4 ms", PlanText.Milliseconds(0.4));
    }
}
