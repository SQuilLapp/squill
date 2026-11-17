using System.Diagnostics;
using Squill.Core;
using Squill.Data;
using Squill.Presentation.Localization;
using Squill.Presentation.Palette;
using Squill.Presentation.Sessions;
using Xunit;

namespace Squill.Presentation.Tests.Palette;

public sealed class PaletteObjectProviderTests
{
    private sealed class FakeConnection(Func<SchemaSnapshot> next) : IDatabaseConnection
    {
        public string ServerVersion => "1";

        public ValueTask<IResultReader> ExecuteAsync(string sql, IReadOnlyList<QueryParameter> parameters, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public ValueTask<IReadOnlyList<string>> ListDatabasesAsync(CancellationToken cancellationToken) => ValueTask.FromResult<IReadOnlyList<string>>([]);

        public ValueTask<SchemaSnapshot> ReadSchemaAsync(CancellationToken cancellationToken) => ValueTask.FromResult(next());

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class FakeHost : IPaletteObjectHost
    {
        public List<ConnectionSession> Open { get; } = [];
        public ConnectionSession? Active { get; set; }
        public List<(ConnectionSession Session, TableInfo Table)> Opened { get; } = [];
        public List<(ConnectionSession Session, string Text)> Scripts { get; } = [];

        public IReadOnlyList<ConnectionSession> Sessions => Open;
        public ConnectionSession? ActiveSession => Active;

        public ValueTask OpenTableAsync(ConnectionSession session, TableInfo table)
        {
            Opened.Add((session, table));
            return ValueTask.CompletedTask;
        }

        public void OpenScript(ConnectionSession session, string text) => Scripts.Add((session, text));
    }

    private static readonly ColumnDefinition Id = new("id", "int", false, true, null);

    private static TableInfo Table(string name, string? schema = "public", TableKind kind = TableKind.Table, string? definition = null) =>
        new(schema, name, kind, [Id], [], [], null, definition);

    private static ConnectionSession Session(string name, DatabaseDialect dialect, SchemaSnapshot schema, Func<SchemaSnapshot>? next = null) =>
        new(new ConnectionProfile(Guid.NewGuid(), name, dialect, "h", 1, "ventes", "u", null, null),
            new FakeConnection(next ?? (() => schema)), schema, null);

    private static FakeHost HostWith(params ConnectionSession[] sessions)
    {
        var host = new FakeHost { Active = sessions.FirstOrDefault() };
        host.Open.AddRange(sessions);
        return host;
    }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Search_NothingConnected_ReturnsEmpty()
    {
        var provider = new PaletteObjectProvider(new FakeHost());

        Assert.Empty(await provider.Search("a", Ct));
    }

    [Fact]
    public async Task Search_RanksBestFirstAndCapsAt200()
    {
        var tables = Enumerable.Range(0, 500).Select(i => Table("order_" + i)).Append(Table("orders")).ToArray();
        var provider = new PaletteObjectProvider(HostWith(Session("a", DatabaseDialect.PostgreSql, new SchemaSnapshot("public", tables))));

        var items = await provider.Search("orders", Ct);

        Assert.Equal("orders", items[0].Title);
        Assert.Equal(PaletteObjectProvider.MaxResults, (await provider.Search("order", Ct)).Count);
    }

    [Fact]
    public async Task Search_SameNameInTwoConnections_ActiveFirstWithConnectionNames()
    {
        var one = Session("one", DatabaseDialect.PostgreSql, new SchemaSnapshot("public", [Table("users")]));
        var two = Session("two", DatabaseDialect.PostgreSql, new SchemaSnapshot("public", [Table("users")]));
        var host = HostWith(one, two);
        host.Active = two;

        var items = await new PaletteObjectProvider(host).Search("users", Ct);

        Assert.Equal(["two", "one"], items.Select(i => i.Right));
    }

    [Fact]
    public async Task Search_DetailShowsKindAndSchema_OrDatabaseWhenNoSchema()
    {
        using var _ = UiText.Override(UiLanguage.English);
        var pg = Session("pg", DatabaseDialect.PostgreSql, new SchemaSnapshot("public", [Table("sales_v", "analytics", TableKind.View), Table("sales_t")]));
        var lite = Session("lite", DatabaseDialect.Sqlite, new SchemaSnapshot(null, [Table("sales_l", null)]));
        var items = await new PaletteObjectProvider(HostWith(pg, lite)).Search("sales", Ct);

        Assert.Equal("view · ventes.analytics", items.Single(i => i.Title == "sales_v").Detail);
        Assert.Equal("table · ventes.public", items.Single(i => i.Title == "sales_t").Detail);
        Assert.Equal("table · ventes", items.Single(i => i.Title == "sales_l").Detail);
    }

    [Fact]
    public async Task Run_Table_View_OpensThroughHost_Procedure_OpensDefinition()
    {
        var schema = new SchemaSnapshot("public", [Table("alpha"), Table("beta", kind: TableKind.View, definition: "SELECT 1")])
        {
            Routines = [new RoutineInfo("public", "gamma", RoutineKind.Procedure, "gamma()", null, "CREATE PROCEDURE gamma() ...")],
        };
        var session = Session("s", DatabaseDialect.SqlServer, schema);
        var host = HostWith(session);
        var provider = new PaletteObjectProvider(host);

        await (await provider.Search("alpha", Ct))[0].Run(PaletteRunMode.Default, Ct);
        await (await provider.Search("beta", Ct))[0].Run(PaletteRunMode.Default, Ct);
        await (await provider.Search("gamma", Ct))[0].Run(PaletteRunMode.Default, Ct);

        Assert.Equal(["alpha", "beta"], host.Opened.Select(o => o.Table.Name));
        Assert.Equal("CREATE PROCEDURE gamma() ...", Assert.Single(host.Scripts).Text);
    }

    [Theory]
    [InlineData(DatabaseDialect.Sqlite, "SELECT * FROM \"public\".\"t\"")]
    [InlineData(DatabaseDialect.PostgreSql, "SELECT * FROM \"public\".\"t\"")]
    [InlineData(DatabaseDialect.MySql, "SELECT * FROM `public`.`t`")]
    [InlineData(DatabaseDialect.SqlServer, "SELECT * FROM [public].[t]")]
    public async Task Run_NewTab_BuildsQuotedSelect(DatabaseDialect dialect, string expected)
    {
        var host = HostWith(Session("s", dialect, new SchemaSnapshot("public", [Table("t")])));

        await (await new PaletteObjectProvider(host).Search("t", Ct))[0].Run(PaletteRunMode.NewTab, Ct);

        Assert.Equal(expected, Assert.Single(host.Scripts).Text);
        Assert.Empty(host.Opened);
    }

    [Fact]
    public async Task Search_Cancelled_Throws()
    {
        var provider = new PaletteObjectProvider(HostWith(Session("s", DatabaseDialect.PostgreSql, new SchemaSnapshot("public", [Table("t")]))));
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await provider.Search("t", cts.Token));
    }

    [Fact]
    public async Task Search_AfterRefreshSchema_SeesTheNewSnapshot()
    {
        var current = new SchemaSnapshot("public", [Table("old_table")]);
        var session = Session("s", DatabaseDialect.PostgreSql, current, () => new SchemaSnapshot("public", [Table("new_table")]));
        var provider = new PaletteObjectProvider(HostWith(session));
        Assert.Single(await provider.Search("old_table", Ct));

        await session.RefreshSchemaAsync(Ct);

        Assert.Empty(await provider.Search("old_table", Ct));
        Assert.Single(await provider.Search("new_table", Ct));
    }

    [Fact]
    public async Task Search_FiftyThousandObjects_StaysFast()
    {
        var tables = Enumerable.Range(0, 50_000).Select(i => Table("table_" + i, "s" + i % 10)).ToArray();
        var provider = new PaletteObjectProvider(HostWith(Session("s", DatabaseDialect.PostgreSql, new SchemaSnapshot("public", tables))));

        var built = Stopwatch.StartNew();
        await provider.Search("tbl4", Ct);
        built.Stop();
        var warm = Stopwatch.StartNew();
        var items = await provider.Search("tbl49", Ct);
        warm.Stop();

        Assert.NotEmpty(items);
        Assert.True(built.Elapsed < TimeSpan.FromSeconds(5), $"first search took {built.Elapsed}");
        Assert.True(warm.Elapsed < TimeSpan.FromSeconds(2), $"warm search took {warm.Elapsed}");
    }

    [Fact]
    public async Task Search_FindsTablesTheExplorerFilterHides()
    {
        // The palette reads the schema snapshot, not the explorer tree, so the "Choose tables" filter does not apply.
        var provider = new PaletteObjectProvider(HostWith(Session("s", DatabaseDialect.PostgreSql, new SchemaSnapshot("public", [Table("hidden_one")]))));

        Assert.Single(await provider.Search("hidden", Ct));
    }
}
