using System.Diagnostics;
using System.Globalization;
using Squill.Core;
using Squill.Data;
using Squill.Platform.Storage;
using Squill.Presentation;
using Squill.Presentation.Editor;
using Squill.Presentation.Localization;
using Squill.Presentation.Query;
using Squill.Presentation.Results;
using Squill.Presentation.Sessions;
using Squill.Sql.Ddl;
using Xunit;

namespace Squill.Data.Tests.Scenarios;

/// <summary>Engine-specific column types and statements of <see cref="GridFilterFlowScenario"/>.</summary>
/// <param name="DecimalType">A fixed-point number type (exact decimals).</param>
/// <param name="TextType">A Unicode text type that keeps accents.</param>
/// <param name="StringPrefix">Prefix of a Unicode string literal (<c>N</c> for SQL Server, empty elsewhere).</param>
/// <param name="MomentType">A date-time type.</param>
/// <param name="FlagType">The boolean type of the engine, or an integer type when it has none.</param>
/// <param name="True">The literal for true.</param>
/// <param name="False">The literal for false.</param>
/// <param name="GuidType">The guid type of the engine, or a text type when it has none.</param>
/// <param name="BigSql">A statement returning the integers 1 to 50,000 in one column.</param>
public sealed record GridFilterSpec(string DecimalType, string TextType, string StringPrefix, string MomentType, string FlagType, string True, string False, string GuidType, string BigSql);

/// <summary>Drives the grid filter, the value list, the sort, the search and the read-only rule through the real view models on a live connection.</summary>
public static class GridFilterFlowScenario
{
    private static CancellationToken Ct => CancellationToken.None;

    private static readonly TimeSpan Bound = TimeSpan.FromSeconds(60);

    /// <summary>Runs every case against one engine.</summary>
    /// <param name="provider">The provider of the engine under test.</param>
    /// <param name="profile">Profile of an empty, writable database.</param>
    /// <param name="password">The password of the profile, or <c>null</c>.</param>
    /// <param name="spec">Types and statements that differ per engine.</param>
    public static async Task RunAsync(IDatabaseProvider provider, ConnectionProfile profile, string? password, GridFilterSpec spec)
    {
        var d = profile.Dialect;
        var registry = new ProviderRegistry();
        registry.Register(provider);
        var manager = new SessionManager(registry, new ScenarioProfileStore(password), new ScenarioTunnelFactory());
        var session = await manager.OpenAsync(profile, Ct);
        try
        {
            var schema = d switch { DatabaseDialect.Sqlite => "main", DatabaseDialect.PostgreSql => "public", DatabaseDialect.SqlServer => "dbo", _ => profile.Database };
            var rig = new Rig(session, SqlQuoting.QualifiedName(schema, "gf_t", d));
            await Seed(rig, spec);
            await session.RefreshSchemaAsync(Ct); // the editor of a single-table result needs the key

            await TypedFilters(rig);
            await ValueListAndIn(rig);
            await Sorts(rig);
            await Search(rig);
            await ExportIgnoresTheFilter(rig);
            await ReadOnlyWhileFiltered(rig);
            await PendingEditsBlockTheFilter(rig);
            await FiftyThousandRows(rig, spec);
        }
        finally
        {
            await manager.CloseAsync(session);
        }
    }

    private static async Task Seed(Rig rig, GridFilterSpec s)
    {
        var u = s.StringPrefix;
        await rig.ExecAsync($"drop table if exists {rig.T}");
        await rig.ExecAsync($"create table {rig.T} (id integer not null primary key, amount {s.DecimalType} not null, name {s.TextType} null, moved_at {s.MomentType} not null, flag {s.FlagType} null, uid {s.GuidType} not null)");
        await rig.ExecAsync(
            $"insert into {rig.T} (id, amount, name, moved_at, flag, uid) values " +
            $"(1, 10.50, {u}'Émile', '2024-01-15 08:00:00', {s.True}, '00000000-0000-0000-0000-000000000001'), " +
            $"(2, 100.00, {u}'Zoé', '2024-03-01 00:00:00', {s.False}, '00000000-0000-0000-0000-000000000002'), " +
            $"(3, 5.25, {u}'Eve', '2024-03-01 12:30:00', {s.True}, '00000000-0000-0000-0000-000000000003'), " +
            $"(4, 250.00, null, '2023-12-31 23:59:59', {s.False}, '00000000-0000-0000-0000-000000000004'), " +
            $"(5, 9.00, {u}'Émile', '2024-06-30 10:00:00', null, '00000000-0000-0000-0000-000000000005')");
    }

    private static async Task TypedFilters(Rig rig)
    {
        var r = await rig.RunAsync($"select * from {rig.T}");
        Assert.Equal(5, r.Grid!.RowCount);

        await Expect(r, "amount", GridFilterOperator.GreaterThan, "50", [2, 4]); // 100 and 250: a number order, not a text order
        if (GridFilterMatcher.Operators(r.Grid.Columns[Col(r, "moved_at")]).Contains(GridFilterOperator.GreaterOrEqual)) // SQLite has no date type: its column is text
            await Expect(r, "moved_at", GridFilterOperator.GreaterOrEqual, "2024-03-01 00:00:00", [2, 3, 5]);
        await Expect(r, "name", GridFilterOperator.Contains, "émi", [1, 5]); // accents and case
        await Expect(r, "name", GridFilterOperator.IsNull, null, [4]);
        await Expect(r, "flag", GridFilterOperator.IsNull, null, [5]);
        var flag = r.Grid.Columns[Col(r, "flag")];
        await Expect(r, "flag", GridFilterOperator.Equals, flag.ClrType == typeof(bool) ? "true" : "1", [1, 3]);

        // Two filters combine with AND, and the footer counts matching rows of loaded rows.
        r.View!.ClearFilters();
        r.View.AddFilter(Create(r, "amount", GridFilterOperator.GreaterThan, "5"));
        r.View.AddFilter(Create(r, "name", GridFilterOperator.StartsWith, "e"));
        Assert.Equal([3], await Ids(r)); // Eve only: both Émile rows start with a different letter
        Assert.Equal(UiText.FormatPlural("results.footer.filtered.done", 1, "5"), r.View.FooterText);
        Assert.Equal(2, r.View.Filters.Count);
        r.View.RemoveFilter(r.View.Filters[1]);
        Assert.Equal([1, 2, 3, 4, 5], await Ids(r));
        r.View.ClearFilters();
        Assert.False(r.View.IsActive);
        Assert.Same(r.Editor!.Grid, r.DisplayGrid); // the unfiltered grid is the editor's
    }

    private static async Task ValueListAndIn(Rig rig)
    {
        var r = await rig.RunAsync($"select * from {rig.T}");
        var name = Col(r, "name");
        using var menu = r.View!.OpenColumnMenu(name)!;
        await menu.ValuesLoaded.WaitAsync(Bound, Ct);
        Assert.Equal([("Émile", 2), ("NULL", 1), ("Eve", 1), ("Zoé", 1)], menu.Values.Select(v => (v.Text, v.Count)));
        Assert.Null(menu.Values.Single(v => v.Text == "NULL").Value);

        foreach (var v in menu.Values.Where(v => v.Text is "NULL" or "Eve"))
            v.IsChecked = false;
        menu.ApplyValuesCommand.Execute(null);
        Assert.Equal(GridFilterOperator.In, Assert.Single(r.View.Filters).Filter.Operator);
        Assert.Equal([1, 2, 5], await Ids(r));

        r.View.ClearFilters();
        var withNull = r.Grid!.Columns[name];
        r.View.AddFilter(GridFilterMatcher.CreateIn(withNull, [null, "Eve"]));
        Assert.Equal([3, 4], await Ids(r)); // the NULL row is a value of the list
        r.View.ClearFilters();
    }

    private static async Task Sorts(Rig rig)
    {
        var r = await rig.RunAsync($"select * from {rig.T}");
        r.View!.SetSort(new GridSort(Col(r, "amount"), false));
        Assert.Equal([3, 5, 1, 2, 4], await Ids(r)); // 5.25, 9, 10.5, 100, 250: numbers, not text
        r.View.ToggleSort(Col(r, "amount"));
        Assert.Equal([4, 2, 1, 5, 3], await Ids(r));
        Assert.Equal(new GridSort(Col(r, "amount"), true), r.View.Sort);

        r.View.SetSort(new GridSort(Col(r, "name"), false));
        Assert.Equal([null, "Émile", "Émile", "Eve", "Zoé"], await Column(r, "name")); // NULL first, accents sorted with their letter
        r.View.SetSort(new GridSort(Col(r, "name"), true));
        Assert.Equal(["Zoé", "Eve", "Émile", "Émile", null], await Column(r, "name"));

        r.View.SetSort(new GridSort(Col(r, "moved_at"), false));
        Assert.Equal([4, 1, 2, 3, 5], await Ids(r));
        r.View.ToggleSort(Col(r, "moved_at"));
        Assert.Equal([5, 3, 2, 1, 4], await Ids(r));
        r.View.ToggleSort(Col(r, "moved_at")); // a third click removes the sort
        Assert.Null(r.View.Sort);
        Assert.False(r.View.IsActive);
    }

    private static async Task Search(Rig rig)
    {
        var r = await rig.RunAsync($"select * from {rig.T}");
        r.View!.SearchText = "eve"; // the text column
        Assert.Equal([3], await Ids(r));
        r.View.SearchText = "2024-03-01"; // the date column
        Assert.Equal([2, 3], await Ids(r));
        r.View.SearchText = "nothing like this";
        Assert.Empty(await Ids(r));
        r.View.ClearFilters();
        Assert.Equal("", r.View.SearchText);
    }

    private static async Task ExportIgnoresTheFilter(Rig rig)
    {
        var r = await rig.RunAsync($"select * from {rig.T}");
        r.View!.AddFilter(Create(r, "amount", GridFilterOperator.GreaterThan, "50"));
        Assert.Equal([2, 4], await Ids(r));
        var directory = Directory.CreateTempSubdirectory("squill-gf-");
        try
        {
            var path = Path.Combine(directory.FullName, "out.csv");
            rig.Dialogs.SavePath = path;
            await r.ExportCommand.ExecuteAsync(new CsvResultExporter());
            Assert.Empty(rig.Dialogs.Errors);
            Assert.Equal(6, File.ReadLines(path).Count()); // header and the 5 loaded rows, not the 2 shown
        }
        finally
        {
            directory.Delete(true);
        }

        r.View.ClearFilters();
    }

    private static async Task ReadOnlyWhileFiltered(Rig rig)
    {
        var r = await rig.RunAsync($"select * from {rig.T}");
        var editor = r.Editor ?? throw new Xunit.Sdk.XunitException("a single-table result must be editable");
        Assert.Null(r.ReadOnlyReason);

        r.View!.AddFilter(Create(r, "amount", GridFilterOperator.GreaterThan, "50"));
        Assert.Equal(UiText.Get("results.readOnly.filtered"), r.ReadOnlyReason);
        Assert.Equal(UiText.Get("results.readOnly.filtered"), editor.TrySetCell(0, Col(r, "name"), "refused"));
        Assert.Equal(-1, editor.AddRow());
        Assert.False(editor.HasPendingChanges);

        r.View.ClearFilters();
        Assert.Null(r.ReadOnlyReason);
        Assert.Null(editor.TrySetCell(RowOf(editor, 2), Col(r, "name"), "Zed"));
        await editor.CommitCommand.ExecuteAsync(null);
        Assert.Equal(["Zed"], await rig.QueryAsync($"select name from {rig.T} where id = 2"));
        Assert.Null(editor.TrySetCell(RowOf(editor, 2), Col(r, "name"), "Zoé")); // put the seed back
        await editor.CommitCommand.ExecuteAsync(null);
        Assert.Equal(["Zoé"], await rig.QueryAsync($"select name from {rig.T} where id = 2"));
    }

    private static async Task PendingEditsBlockTheFilter(Rig rig)
    {
        var r = await rig.RunAsync($"select * from {rig.T}");
        var editor = r.Editor!;
        Assert.Null(editor.TrySetCell(RowOf(editor, 3), Col(r, "name"), "Eva"));
        Assert.True(r.HasPendingChanges);
        Assert.False(r.View!.CanChange);
        Assert.NotNull(r.View.BlockedReason);
        r.View.AddFilter(Create(r, "amount", GridFilterOperator.GreaterThan, "50"));
        r.View.SetSort(new GridSort(0, false));
        r.View.SearchText = "x";
        Assert.False(r.View.IsActive);
        Assert.Empty(r.View.Filters);

        editor.RevertRows([RowOf(editor, 3)]);
        Assert.False(r.HasPendingChanges);
        r.View.AddFilter(Create(r, "amount", GridFilterOperator.GreaterThan, "50"));
        Assert.True(r.View.IsActive);
        r.View.ClearFilters();
        Assert.Equal(["Eve"], await rig.QueryAsync($"select name from {rig.T} where id = 3"));
    }

    private static async Task FiftyThousandRows(Rig rig, GridFilterSpec spec)
    {
        var r = await rig.RunAsync(spec.BigSql);
        Assert.Equal(50_000, r.Grid!.RowCount);
        Assert.True(r.Grid.IsComplete);

        var clock = Stopwatch.StartNew();
        r.View!.AddFilter(Create(r, r.Grid.Columns[0].Name, GridFilterOperator.GreaterThan, "49990"));
        Assert.True(clock.Elapsed < TimeSpan.FromSeconds(2), $"AddFilter blocked the caller for {clock.Elapsed}"); // the rebuild runs on a worker
        await Settle(r);
        Assert.Equal(10, r.DisplayGrid!.RowCount);

        r.View.SetSort(new GridSort(0, true));
        await Settle(r);
        Assert.Equal(10, r.DisplayGrid.RowCount);
        Assert.Equal(50_000L, Convert.ToInt64(r.DisplayGrid.GetCell(0, 0), CultureInfo.InvariantCulture));

        r.View.ClearFilters();
        r.View.SetSort(new GridSort(0, true)); // a sort of every row
        await Settle(r);
        Assert.Equal(50_000, r.DisplayGrid.RowCount);
        Assert.Equal(50_000L, Convert.ToInt64(r.DisplayGrid.GetCell(0, 0), CultureInfo.InvariantCulture));
        Assert.True(clock.Elapsed < Bound, $"filter and sort of 50,000 rows took {clock.Elapsed}");
        r.View.ClearFilters();
    }

    private static GridFilter Create(ResultViewModel r, string column, GridFilterOperator op, string? text)
    {
        var info = r.Grid!.Columns[Col(r, column)];
        Assert.True(GridFilterMatcher.TryCreate(info, op, text, out var filter, out var error), error);
        return filter;
    }

    // Applies one typed filter alone and checks the ids that remain, in source order.
    private static async Task Expect(ResultViewModel r, string column, GridFilterOperator op, string? text, long[] ids)
    {
        r.View!.ClearFilters();
        r.View.AddFilter(Create(r, column, op, text));
        Assert.Equal(ids, await Ids(r));
        Assert.Equal(UiText.FormatPlural("results.footer.filtered.done", ids.Length, "5"), r.View.FooterText);
        r.View.ClearFilters();
    }

    private static async Task Settle(ResultViewModel r)
    {
        if (r.DisplayGrid is FilteredGridDataSource view)
            await view.WhenIdle().WaitAsync(Bound, Ct);
    }

    private static async Task<List<long>> Ids(ResultViewModel r)
    {
        await Settle(r);
        var grid = r.DisplayGrid!;
        var id = Col(r, "id");
        return [.. Enumerable.Range(0, (int)grid.RowCount).Select(i => Convert.ToInt64(grid.GetCell(i, id), CultureInfo.InvariantCulture))];
    }

    private static async Task<List<string?>> Column(ResultViewModel r, string name)
    {
        await Settle(r);
        var grid = r.DisplayGrid!;
        var c = Col(r, name);
        return [.. Enumerable.Range(0, (int)grid.RowCount).Select(i => grid.GetCell(i, c) is { } v ? Convert.ToString(v, CultureInfo.InvariantCulture) : null)];
    }

    private static int Col(ResultViewModel r, string name) =>
        r.Grid!.Columns.First(c => string.Equals(c.Name, name, StringComparison.OrdinalIgnoreCase)).Ordinal;

    private static long RowOf(Squill.Presentation.Editing.ResultEditorViewModel editor, long id)
    {
        for (long row = 0; row < editor.Grid.RowCount; row++)
        {
            if (Convert.ToInt64(editor.Grid.GetCell(row, 0), CultureInfo.InvariantCulture) == id)
                return row;
        }

        throw new Xunit.Sdk.XunitException($"no row {id}");
    }

    private sealed class Rig
    {
        private readonly QueryTabViewModel _tab;
        private readonly StubSqlEditor _editor = new();
        private readonly ConnectionSession _session;

        public Rig(ConnectionSession session, string table)
        {
            _session = session;
            T = table;
            _tab = new QueryTabViewModel(session, _editor, new History(), new ScenarioSettings(), new ImmediateDispatcher(), Dialogs);
        }

        public string T { get; }

        public Dialogs Dialogs { get; } = new();

        public async Task<ResultViewModel> RunAsync(string sql)
        {
            _editor.Text = sql;
            await _tab.RunCommand.ExecuteAsync(null);
            return _tab.Results[0];
        }

        public async Task ExecAsync(string sql)
        {
            await using var reader = await _session.Connection.ExecuteAsync(sql, [], Ct);
            await reader.ReadAllAsync(10, Ct);
        }

        // Each row as its cells joined by '|' (NULL is empty).
        public async Task<string[]> QueryAsync(string sql)
        {
            await using var reader = await _session.Connection.ExecuteAsync(sql, [], Ct);
            var rows = await reader.ReadAllAsync(100, Ct);
            return [.. rows.Select(r => string.Join('|', r.Select(c => c is null or DBNull ? "" : Convert.ToString(c, CultureInfo.InvariantCulture))))];
        }
    }

    private sealed class History : IQueryHistoryStore
    {
        public ValueTask AppendAsync(HistoryEntry e, CancellationToken ct) => ValueTask.CompletedTask;

        public ValueTask<IReadOnlyList<HistoryEntry>> ListAsync(int max, CancellationToken ct) => ValueTask.FromResult<IReadOnlyList<HistoryEntry>>([]);

        public ValueTask ClearAsync(CancellationToken ct) => ValueTask.CompletedTask;
    }

    private sealed class Dialogs : IDialogService
    {
        public List<(string Title, string Message)> Errors { get; } = [];

        public string? SavePath { get; set; }

        public ValueTask<bool> ConfirmSshHostKeyAsync(Squill.Platform.Ssh.SshHostKeyRequest request) => ValueTask.FromResult(false);

        public ValueTask<Squill.Presentation.Safety.ProductionConfirmationResult> ConfirmProductionAsync(Squill.Presentation.Safety.ProductionConfirmationRequest request) =>
            ValueTask.FromResult(new Squill.Presentation.Safety.ProductionConfirmationResult(true, false));

        public ValueTask<Squill.Presentation.Safety.TransactionChoice> AskTransactionAsync(Squill.Presentation.Safety.TransactionCloseRequest request) =>
            ValueTask.FromResult(Squill.Presentation.Safety.TransactionChoice.Rollback);

        public ValueTask<bool> ConfirmAsync(string title, string message) => ValueTask.FromResult(true);

        public ValueTask<string?> PromptAsync(string title, string message, string? initial) => ValueTask.FromResult(initial);

        public ValueTask<string?> PickSaveFileAsync(string suggestedName, string extension) => ValueTask.FromResult(SavePath);

        public ValueTask ShowErrorAsync(string title, string message)
        {
            Errors.Add((title, message));
            return ValueTask.CompletedTask;
        }
    }
}
