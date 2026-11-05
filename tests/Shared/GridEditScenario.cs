using System.Globalization;
using Squill.Core;
using Squill.Data;
using Squill.Platform.Profiles;
using Squill.Platform.Settings;
using Squill.Platform.Ssh;
using Squill.Platform.Storage;
using Squill.Presentation;
using Squill.Presentation.Editor;
using Squill.Presentation.Query;
using Squill.Presentation.Results;
using Squill.Presentation.Sessions;
using Squill.Sql.Ddl;
using Xunit;

namespace Squill.Data.Tests.Scenarios;

/// <summary>Edits result grids through the real <c>QueryTabViewModel</c> and <c>ResultEditorViewModel</c> on a live connection and checks what the database holds afterwards.</summary>
public static class GridEditScenario
{
    private static CancellationToken Ct => CancellationToken.None;

    /// <summary>Runs every grid-editing case against one engine.</summary>
    /// <param name="provider">The provider of the engine under test.</param>
    /// <param name="profile">Profile of an empty, writable database.</param>
    /// <param name="password">The password of the profile, or <c>null</c>.</param>
    public static async Task RunAsync(IDatabaseProvider provider, ConnectionProfile profile, string? password)
    {
        var d = profile.Dialect;
        var registry = new ProviderRegistry();
        registry.Register(provider);
        var manager = new SessionManager(registry, new ScenarioProfileStore(password), new ScenarioTunnelFactory());
        var session = await manager.OpenAsync(profile, Ct);
        try
        {
            var schema = d switch { DatabaseDialect.Sqlite => "main", DatabaseDialect.PostgreSql => "public", DatabaseDialect.SqlServer => "dbo", _ => profile.Database };
            var rig = new Rig(session, d, schema);
            foreach (var sql in Ddl(d, rig))
                await rig.ExecAsync(sql);
            await session.RefreshSchemaAsync(Ct);

            await UpdateByKeyAndAlias(rig);
            await UpdateKeyColumnAndComposite(rig);
            await InsertWithGeneratedKey(rig);
            await InsertOnDefaultsOnly(rig);
            await DeleteSeveral(rig);
            await ReadOnlySources(rig);
            await FailingCommitRollsBack(rig);
            await RowDeletedByAnotherSession(rig);
            await InsertOnTableWithTrigger(rig);
            await TypedKeys(rig);
        }
        finally
        {
            await manager.CloseAsync(session);
        }
    }

    private static async Task UpdateByKeyAndAlias(Rig rig)
    {
        await rig.ExecAsync($"insert into {rig.T("ge_items")} (name, qty) values ('a', 1), ('b', 2), ('c', 3)");
        foreach (var sql in new[] { $"select * from {rig.T("ge_items")}", $"select i.* from {rig.T("ge_items")} i" })
        {
            var ed = (await rig.RunAsync(sql)).Editor ?? throw new Xunit.Sdk.XunitException($"not editable: {sql}");
            var row = Find(ed, "name", "a");
            Set(ed, row, "name", "a2");
            ed.SetNull(row, Col(ed, "qty"));
            await CommitAsync(ed);
            Assert.Equal(["a2|", "b|2", "c|3"], await rig.QueryAsync($"select name, qty from {rig.T("ge_items")} order by id"));

            Set(ed, Find(ed, "name", "a2"), "name", "a"); // the same grid keeps working after a commit
            Set(ed, Find(ed, "name", "a"), "qty", "1");
            await CommitAsync(ed);
            Assert.Equal(["a|1", "b|2", "c|3"], await rig.QueryAsync($"select name, qty from {rig.T("ge_items")} order by id"));
        }
    }

    private static async Task UpdateKeyColumnAndComposite(Rig rig)
    {
        await rig.ExecAsync($"insert into {rig.T("ge_comp")} (a, b, label) values (1, 1, 'x'), (1, 2, 'y')");
        var ed = (await rig.RunAsync($"select * from {rig.T("ge_comp")}")).Editor!;
        Assert.NotNull(ed);
        var row = Find(ed, "b", "2");
        Set(ed, row, "b", "3");
        Set(ed, row, "label", "z");
        await CommitAsync(ed);
        Assert.Equal(["1|1|x", "1|3|z"], await rig.QueryAsync($"select a, b, label from {rig.T("ge_comp")} order by a, b"));
    }

    private static async Task InsertWithGeneratedKey(Rig rig)
    {
        var ed = (await rig.RunAsync($"select * from {rig.T("ge_items")}")).Editor!;
        var row = ed.AddRow();
        Set(ed, row, "name", "new");
        Set(ed, row, "qty", "5");
        await CommitAsync(ed);
        var key = ed.Grid.GetCell(row, Col(ed, "id"));
        Assert.NotNull(key);
        Assert.Equal([$"new|5"], await rig.QueryAsync($"select name, qty from {rig.T("ge_items")} where id = {Convert.ToString(key, CultureInfo.InvariantCulture)}"));
    }

    private static async Task InsertOnDefaultsOnly(Rig rig)
    {
        var ed = (await rig.RunAsync($"select * from {rig.T("ge_defaults")}")).Editor!;
        Assert.NotNull(ed);
        var row = ed.AddRow();
        await CommitAsync(ed);
        Assert.NotNull(ed.Grid.GetCell(row, Col(ed, "id")));
        Assert.Equal(["x|7"], await rig.QueryAsync($"select note, qty from {rig.T("ge_defaults")}"));
    }

    private static async Task DeleteSeveral(Rig rig)
    {
        var ed = (await rig.RunAsync($"select * from {rig.T("ge_items")}")).Editor!;
        ed.DeleteRows([Find(ed, "name", "a"), Find(ed, "name", "b")]);
        await CommitAsync(ed);
        Assert.Equal(["c|3", "new|5"], await rig.QueryAsync($"select name, qty from {rig.T("ge_items")} order by id"));
    }

    private static async Task ReadOnlySources(Rig rig)
    {
        await rig.ExecAsync($"insert into {rig.T("ge_nopk")} (x, y) values (1, 'x')");
        foreach (var sql in new[]
        {
            $"select * from {rig.T("ge_nopk")}",
            $"select * from {rig.T("ge_view")}",
            $"select i.id, c.a from {rig.T("ge_items")} i join {rig.T("ge_comp")} c on c.a = i.id",
        })
        {
            var result = await rig.RunAsync(sql);
            Assert.Null(result.Editor);
            Assert.False(string.IsNullOrEmpty(result.ReadOnlyReason), sql);
        }
    }

    private static async Task FailingCommitRollsBack(Rig rig)
    {
        await rig.ExecAsync($"insert into {rig.T("ge_uniq")} (id, u) values (1, 'a'), (2, 'b')");
        var ed = (await rig.RunAsync($"select * from {rig.T("ge_uniq")}")).Editor!;
        var first = Find(ed, "id", "1");
        var second = Find(ed, "id", "2");
        Set(ed, first, "u", "z");
        Set(ed, second, "u", "z");
        await CommitAsync(ed);

        Assert.Single(rig.Dialogs.Errors);
        Assert.True(ed.HasPendingChanges);
        var issue = Assert.Single(ed.Issues);
        Assert.Contains(issue.Row, new[] { first, second });
        Assert.False(string.IsNullOrWhiteSpace(issue.Message));
        Assert.Equal(["1|a", "2|b"], await rig.QueryAsync($"select id, u from {rig.T("ge_uniq")} order by id"));
        rig.Dialogs.Errors.Clear();
    }

    private static async Task RowDeletedByAnotherSession(Rig rig)
    {
        var ed = (await rig.RunAsync($"select * from {rig.T("ge_items")}")).Editor!;
        var row = Find(ed, "name", "c");
        Set(ed, row, "qty", "30");
        await rig.ExecAsync($"delete from {rig.T("ge_items")} where name = 'c'");
        await CommitAsync(ed);

        Assert.Single(rig.Dialogs.Errors);
        Assert.True(ed.HasPendingChanges);
        Assert.Equal(row, Assert.Single(ed.Issues).Row);
        Assert.Equal(["new|5"], await rig.QueryAsync($"select name, qty from {rig.T("ge_items")}"));
        rig.Dialogs.Errors.Clear();
    }

    private static async Task InsertOnTableWithTrigger(Rig rig)
    {
        var ed = (await rig.RunAsync($"select * from {rig.T("ge_trig")}")).Editor!;
        var row = ed.AddRow();
        Set(ed, row, "name", "t");
        await CommitAsync(ed);
        var key = ed.Grid.GetCell(row, Col(ed, "id"));
        Assert.NotNull(key);
        Assert.Equal([$"t"], await rig.QueryAsync($"select name from {rig.T("ge_trig")} where id = {Convert.ToString(key, CultureInfo.InvariantCulture)}"));
        Assert.Equal(["t"], await rig.QueryAsync($"select msg from {rig.T("ge_audit")}"));
    }

    private static async Task TypedKeys(Rig rig)
    {
        var guid = "6f9619ff-8b86-d011-b42d-00c04fc964ff";
        await rig.ExecAsync($"insert into {rig.T("ge_guid")} (id, v) values ('{guid}', 'a')");
        await rig.ExecAsync($"insert into {rig.T("ge_char")} (id, v) values ('ab', 'a')");
        await rig.ExecAsync($"insert into {rig.T("ge_dec")} (id, v) values (1.50, 'a')");

        var ed = (await rig.RunAsync($"select * from {rig.T("ge_guid")}")).Editor!;
        Set(ed, 0, "v", "b");
        var added = ed.AddRow();
        Set(ed, added, "id", "a0eebc99-9c0b-4ef8-bb6d-6bb9bd380a11");
        Set(ed, added, "v", "c");
        await CommitAsync(ed);
        var guids = (await rig.QueryAsync($"select id, v from {rig.T("ge_guid")}")).Select(s => s.ToLowerInvariant()).Order(StringComparer.Ordinal).ToArray();
        Assert.Equal(["6f9619ff-8b86-d011-b42d-00c04fc964ff|b", "a0eebc99-9c0b-4ef8-bb6d-6bb9bd380a11|c"], guids);

        ed = (await rig.RunAsync($"select * from {rig.T("ge_char")}")).Editor!;
        Set(ed, 0, "v", "b");
        await CommitAsync(ed);
        Assert.Equal(["ab|b"], (await rig.QueryAsync($"select id, v from {rig.T("ge_char")}")).Select(s => string.Join("|", s.Split('|').Select(p => p.TrimEnd()))).ToList());

        ed = (await rig.RunAsync($"select * from {rig.T("ge_dec")}")).Editor!;
        Set(ed, 0, "v", "b");
        var more = ed.AddRow();
        Set(ed, more, "id", "2.25");
        Set(ed, more, "v", "c");
        await CommitAsync(ed);
        Assert.Equal(["1.5|b", "2.25|c"], (await rig.QueryAsync($"select id, v from {rig.T("ge_dec")} order by id")).Select(s => Normalize(s)).ToList());
    }

    // "1.50|b" and "1.5|b" are the same number: drop trailing zeros of a decimal key.
    private static string Normalize(string row)
    {
        var parts = row.Split('|');
        return decimal.TryParse(parts[0], CultureInfo.InvariantCulture, out var n) ? $"{n.ToString("0.##", CultureInfo.InvariantCulture)}|{parts[1]}" : row;
    }

    private static Task CommitAsync(Squill.Presentation.Editing.ResultEditorViewModel ed) => ed.CommitCommand.ExecuteAsync(null);

    private static int Col(Squill.Presentation.Editing.ResultEditorViewModel ed, string name)
    {
        for (var i = 0; i < ed.Grid.Columns.Count; i++)
        {
            if (string.Equals(ed.Grid.Columns[i].Name, name, StringComparison.OrdinalIgnoreCase))
                return i;
        }

        throw new Xunit.Sdk.XunitException($"no column {name}");
    }

    private static long Find(Squill.Presentation.Editing.ResultEditorViewModel ed, string column, string text)
    {
        var c = Col(ed, column);
        for (long r = 0; r < ed.Grid.RowCount; r++)
        {
            var cell = ed.Grid.GetCell(r, c);
            if (string.Equals(Convert.ToString(cell, CultureInfo.InvariantCulture)?.TrimEnd(), text, StringComparison.Ordinal))
                return r;
        }

        throw new Xunit.Sdk.XunitException($"no row with {column} = {text}");
    }

    private static void Set(Squill.Presentation.Editing.ResultEditorViewModel ed, long row, string column, string text) =>
        Assert.Null(ed.TrySetCell(row, Col(ed, column), text));

    private static string[] Ddl(DatabaseDialect d, Rig rig)
    {
        var i = d == DatabaseDialect.Sqlite ? "INTEGER" : "int";
        var t = d switch { DatabaseDialect.Sqlite => "TEXT", DatabaseDialect.SqlServer => "nvarchar(100)", _ => "varchar(100)" };
        var auto = d switch
        {
            DatabaseDialect.Sqlite => "id INTEGER PRIMARY KEY AUTOINCREMENT",
            DatabaseDialect.PostgreSql => "id serial PRIMARY KEY",
            DatabaseDialect.MySql => "id int NOT NULL AUTO_INCREMENT PRIMARY KEY",
            _ => "id int IDENTITY(1,1) PRIMARY KEY",
        };
        var guid = d switch { DatabaseDialect.PostgreSql => "uuid", DatabaseDialect.SqlServer => "uniqueidentifier", DatabaseDialect.MySql => "char(36)", _ => "TEXT" };
        var chr = d == DatabaseDialect.Sqlite ? "TEXT" : "char(5)";
        var dec = d == DatabaseDialect.Sqlite ? "NUMERIC" : "decimal(10,2)";
        string[] tables = ["ge_view", "ge_trig", "ge_audit", "ge_items", "ge_comp", "ge_defaults", "ge_nopk", "ge_uniq", "ge_guid", "ge_char", "ge_dec"];
        List<string> sql = [];
        foreach (var name in tables)
            sql.Add($"DROP {(name == "ge_view" ? "VIEW" : "TABLE")} IF EXISTS {rig.T(name)}");
        sql.AddRange(
        [
            $"CREATE TABLE {rig.T("ge_items")} ({auto}, name {t} NOT NULL, qty {i} NULL)",
            $"CREATE TABLE {rig.T("ge_comp")} (a {i} NOT NULL, b {i} NOT NULL, label {t} NULL, PRIMARY KEY (a, b))",
            $"CREATE TABLE {rig.T("ge_defaults")} ({auto}, note {t} DEFAULT 'x', qty {i} DEFAULT 7)",
            $"CREATE TABLE {rig.T("ge_nopk")} (x {i}, y {t})",
            $"CREATE TABLE {rig.T("ge_uniq")} (id {i} NOT NULL PRIMARY KEY, u {t} NOT NULL UNIQUE)",
            $"CREATE TABLE {rig.T("ge_guid")} (id {guid} NOT NULL PRIMARY KEY, v {t})",
            $"CREATE TABLE {rig.T("ge_char")} (id {chr} NOT NULL PRIMARY KEY, v {t})",
            $"CREATE TABLE {rig.T("ge_dec")} (id {dec} NOT NULL PRIMARY KEY, v {t})",
            $"CREATE TABLE {rig.T("ge_audit")} ({auto}, msg {t})",
            $"CREATE TABLE {rig.T("ge_trig")} ({auto}, name {t})",
            $"CREATE VIEW {rig.T("ge_view")} AS SELECT id, name FROM {rig.T("ge_items")}",
        ]);
        switch (d)
        {
            case DatabaseDialect.Sqlite:
                sql.Add($"CREATE TRIGGER ge_trig_ai AFTER INSERT ON {rig.T("ge_trig")} BEGIN INSERT INTO ge_audit (msg) VALUES (new.name); END");
                break;
            case DatabaseDialect.PostgreSql:
                sql.Add($"CREATE OR REPLACE FUNCTION {rig.T("ge_trig_fn")}() RETURNS trigger AS $$ BEGIN INSERT INTO {rig.T("ge_audit")} (msg) VALUES (NEW.name); RETURN NEW; END $$ LANGUAGE plpgsql");
                sql.Add($"CREATE TRIGGER ge_trig_ai AFTER INSERT ON {rig.T("ge_trig")} FOR EACH ROW EXECUTE FUNCTION {rig.T("ge_trig_fn")}()");
                break;
            case DatabaseDialect.MySql:
                sql.Add($"CREATE TRIGGER ge_trig_ai AFTER INSERT ON {rig.T("ge_trig")} FOR EACH ROW INSERT INTO {rig.T("ge_audit")} (msg) VALUES (NEW.name)");
                break;
            default:
                sql.Add($"CREATE TRIGGER {rig.T("ge_trig_ai")} ON {rig.T("ge_trig")} AFTER INSERT AS BEGIN SET NOCOUNT ON; INSERT INTO {rig.T("ge_audit")} (msg) SELECT name FROM inserted; END");
                break;
        }

        return [.. sql];
    }

    private sealed class Rig
    {
        private readonly QueryTabViewModel _tab;
        private readonly StubSqlEditor _editor = new();
        private readonly ConnectionSession _session;
        private readonly DatabaseDialect _dialect;
        private readonly string? _schema;

        public Rig(ConnectionSession session, DatabaseDialect dialect, string? schema)
        {
            _session = session;
            _dialect = dialect;
            _schema = schema;
            _tab = new QueryTabViewModel(session, _editor, new History(), new ScenarioSettings(), new ImmediateDispatcher(), Dialogs);
        }

        public Dialogs Dialogs { get; } = new();

        public string T(string name) => SqlQuoting.QualifiedName(_schema, name, _dialect);

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

    private sealed class Dialogs : IDialogService
    {
        public List<(string Title, string Message)> Errors { get; } = [];

        public ValueTask<bool> ConfirmAsync(string title, string message) => ValueTask.FromResult(true);

        public ValueTask<string?> PromptAsync(string title, string message, string? initial) => ValueTask.FromResult(initial);

        public ValueTask<string?> PickSaveFileAsync(string suggestedName, string extension) => ValueTask.FromResult<string?>(null);

        public ValueTask ShowErrorAsync(string title, string message)
        {
            Errors.Add((title, message));
            return ValueTask.CompletedTask;
        }

        public ValueTask<IReadOnlyList<ParameterValue>?> PromptParametersAsync(IReadOnlyList<string> names, IReadOnlyDictionary<string, ParameterValue> defaults) =>
            ValueTask.FromResult<IReadOnlyList<ParameterValue>?>(null);
    }

    private sealed class History : IQueryHistoryStore
    {
        public ValueTask AppendAsync(HistoryEntry e, CancellationToken ct) => ValueTask.CompletedTask;

        public ValueTask<IReadOnlyList<HistoryEntry>> ListAsync(int max, CancellationToken ct) => ValueTask.FromResult<IReadOnlyList<HistoryEntry>>([]);

        public ValueTask ClearAsync(CancellationToken ct) => ValueTask.CompletedTask;
    }
}
