using Squill.Core;
using Xunit;

namespace Squill.Data.Sqlite.Tests;

public sealed class SqliteSchemaObjectsTests
{
    private static readonly ConnectionProfile Memory =
        new(Guid.NewGuid(), "mem", DatabaseDialect.Sqlite, null, null, null, null, ":memory:", null);

    private static async Task<IDatabaseConnection> OpenAsync() => await new SqliteDatabaseProvider().OpenAsync(Memory, null, CancellationToken.None);

    private static async Task RunAsync(IDatabaseConnection connection, string sql)
    {
        await using var reader = await connection.ExecuteAsync(sql, [], CancellationToken.None);
        await reader.ReadAllAsync(10, CancellationToken.None);
    }

    [Fact]
    public async Task ViewDefinition_IsTheSelectAfterAs()
    {
        await using var connection = await OpenAsync();
        await RunAsync(connection, "CREATE TABLE t (id INTEGER PRIMARY KEY, a TEXT)");
        await RunAsync(connection, "CREATE VIEW \"as\" (x, y) AS SELECT id, a FROM t WHERE a = ' as '");
        var view = (await connection.ReadSchemaAsync(CancellationToken.None)).FindTable(null, "as")!;
        Assert.Equal("SELECT id, a FROM t WHERE a = ' as '", view.Definition);
    }

    [Theory]
    [InlineData("CREATE TRIGGER g AFTER INSERT ON t BEGIN SELECT 1; END", "AFTER", "INSERT")]
    [InlineData("CREATE TRIGGER g before update of a on t BEGIN SELECT 1; END", "BEFORE", "UPDATE")]
    [InlineData("CREATE TRIGGER IF NOT EXISTS g DELETE ON t BEGIN SELECT 1; END", "BEFORE", "DELETE")]
    [InlineData("CREATE TRIGGER \"after\" INSTEAD OF DELETE ON v BEGIN SELECT 1; END", "INSTEAD OF", "DELETE")]
    public async Task TriggerHeader_ReadsTimingAndEvent(string sql, string timing, string events)
    {
        await using var connection = await OpenAsync();
        await RunAsync(connection, "CREATE TABLE t (id INTEGER PRIMARY KEY, a TEXT)");
        await RunAsync(connection, "CREATE VIEW v AS SELECT id, a FROM t");
        await RunAsync(connection, sql);
        var trigger = Assert.Single((await connection.ReadSchemaAsync(CancellationToken.None)).Triggers);
        Assert.Equal((timing, events), (trigger.Timing, trigger.Events));
    }

    [Fact]
    public async Task Triggers_AreReadWithFullDefinition()
    {
        await using var connection = await OpenAsync();
        await RunAsync(connection, "CREATE TABLE t (id INTEGER PRIMARY KEY, a TEXT)");
        await RunAsync(connection, "CREATE TRIGGER t_ins AFTER INSERT ON t BEGIN UPDATE t SET a = 'x' WHERE id = new.id; END");
        var trigger = Assert.Single((await connection.ReadSchemaAsync(CancellationToken.None)).Triggers);
        Assert.Equal(("main", "t", "t_ins", "AFTER", "INSERT"), (trigger.Schema, trigger.Table, trigger.Name, trigger.Timing, trigger.Events));
        Assert.StartsWith("CREATE TRIGGER t_ins", trigger.Definition);
        Assert.EndsWith("END", trigger.Definition);
    }

    [Fact]
    public async Task InternalAndShadowTables_AreNotListed()
    {
        await using var connection = await OpenAsync();
        await RunAsync(connection, "CREATE TABLE t (id INTEGER PRIMARY KEY AUTOINCREMENT)"); // creates sqlite_sequence
        await RunAsync(connection, "CREATE VIRTUAL TABLE docs USING fts5(body)"); // creates docs_data, docs_idx, ...
        var names = (await connection.ReadSchemaAsync(CancellationToken.None)).Tables.Select(t => t.Name).ToList();
        Assert.Equal(["docs", "t"], names);
    }

    [Fact]
    public async Task SampleSchema_LoadsAndIsReadBack()
    {
        await using var connection = await OpenAsync();
        var script = await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, "SampleSchema", "sqlite.sql"), TestContext.Current.CancellationToken);
        await RunAsync(connection, script);
        await RunAsync(connection, script); // idempotent

        var snapshot = await connection.ReadSchemaAsync(CancellationToken.None);
        Assert.Equal(
            ["all_types", "customers", "fk_actions", "fk_targets", "no_pk", "order_items", "order_totals", "orders", "wr_lookup"],
            snapshot.Tables.Select(t => t.Name).Order());
        var view = snapshot.FindTable(null, "order_totals")!;
        Assert.Equal(TableKind.View, view.Kind);
        Assert.StartsWith("select c.id as customer_id", view.Definition);

        Assert.Equal(["order_id", "line_no"], snapshot.FindTable(null, "order_items")!.Columns.Where(c => c.IsPrimaryKey).Select(c => c.Name));
        Assert.DoesNotContain(snapshot.FindTable(null, "no_pk")!.Columns, c => c.IsPrimaryKey);
        Assert.Equal(["code"], snapshot.FindTable(null, "wr_lookup")!.Columns.Where(c => c.IsPrimaryKey).Select(c => c.Name));
        Assert.Contains(snapshot.FindTable(null, "orders")!.Indexes, i => i.Name == "orders_customer_placed" && i.Columns.SequenceEqual(["customer_id", "placed_on"]));

        var actions = snapshot.FindTable(null, "fk_actions")!.ForeignKeys.OrderBy(f => f.Columns[0]).Select(f => f.OnDelete).ToList();
        Assert.Equal([ReferentialAction.Cascade, ReferentialAction.SetNull, ReferentialAction.Restrict, ReferentialAction.NoAction, ReferentialAction.SetDefault], actions);

        var types = snapshot.FindTable(null, "all_types")!.Columns;
        Assert.Equal(9, types.Count);
        Assert.True(types.Single(c => c.Name == "c_id").IsAutoIncrement);
        Assert.Equal("NUMERIC(10,2)", types.Single(c => c.Name == "c_numeric").DataType);

        var trigger = Assert.Single(snapshot.Triggers);
        Assert.Equal(("orders", "orders_touch", "AFTER", "UPDATE"), (trigger.Table, trigger.Name, trigger.Timing, trigger.Events));
    }
}
