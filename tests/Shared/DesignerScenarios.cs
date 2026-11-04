using Squill.Core;
using Squill.Core.Design;
using Squill.Sql.Ddl;
using Xunit;

namespace Squill.Data.RoundTrip;

/// <summary>The table designer operation matrix every server engine runs against a live connection: one populated table, one edit per case, then schema and data are checked.</summary>
internal static class DesignerScenarios
{
    private static CancellationToken Ct => CancellationToken.None;

    private sealed record Case(Func<TableDesign, TableDesign> Edit, string Check);

    /// <summary>The names of the cases that apply to a dialect, for <c>MemberData</c>.</summary>
    public static TheoryData<string> Names(DatabaseDialect d)
    {
        var data = new TheoryData<string>();
        foreach (var name in Cases(d, null).Keys)
            data.Add(name);
        return data;
    }

    private static string Int(DatabaseDialect d) => d == DatabaseDialect.PostgreSql ? "integer" : "int";

    private static string Str(DatabaseDialect d, int n) => d switch
    {
        DatabaseDialect.SqlServer => $"nvarchar({n})",
        DatabaseDialect.PostgreSql => $"character varying({n})",
        _ => $"varchar({n})",
    };

    private static string LongText(DatabaseDialect d) => d == DatabaseDialect.SqlServer ? "nvarchar(max)" : "text";

    private static ColumnDesign Col(string name, string type, bool nullable, string? def = null) => new(null, name, type, nullable, def, false);

    private static TableDesign Map(TableDesign t, string column, Func<ColumnDesign, ColumnDesign> f) =>
        t with { Columns = [.. t.Columns.Select(c => c.Name == column ? f(c) : c)] };

    private static TableDesign Without(TableDesign t, string column) =>
        t with { Columns = [.. t.Columns.Where(c => c.Name != column)] };

    private static readonly ReferentialAction[] Actions =
        [ReferentialAction.NoAction, ReferentialAction.Restrict, ReferentialAction.Cascade, ReferentialAction.SetNull, ReferentialAction.SetDefault];

    private static Dictionary<string, Case> Cases(DatabaseDialect d, string? schema)
    {
        var cases = new Dictionary<string, Case>
        {
            ["add nullable column"] = new(t => t with { Columns = [.. t.Columns, Col("n1", Str(d, 20), true)] }, "a"),
            ["add column with default"] = new(t => t with { Columns = [.. t.Columns, Col("n2", Str(d, 20), true, "'z'")] }, "a"),
            ["add not null column with default"] = new(t => t with { Columns = [.. t.Columns, Col("n3", Int(d), false, "7")] }, "a"),
            ["drop plain column"] = new(t => Without(t, "q"), "a"),
            ["drop column with default"] = new(t => Without(t, "c"), "a"),
            ["drop indexed column"] = new(t => Without(Map(t, "a", c => c) with { Indexes = [.. t.Indexes.Where(i => i.Name != "ix_b")] }, "b"), "a"),
            ["drop column in a foreign key"] = new(t => Without(t, "p") with { Indexes = [.. t.Indexes.Where(i => i.Name != "ix_p")], ForeignKeys = [] }, "a"),
            ["rename column"] = new(t => Map(t, "b", c => c with { Name = "b2" }) with { Indexes = [.. t.Indexes.Select(i => i.Name == "ix_b" ? i with { Columns = ["b2"] } : i)] }, "b"),
            ["rename column with default"] = new(t => Map(t, "c", c => c with { Name = "c2" }), "c"),
            ["change type"] = new(t => Map(t, "n", c => c with { DataType = Int(d) }), "n"),
            ["widen indexed column"] = new(t => Map(t, "b", c => c with { DataType = Str(d, 100) }), "b"),
            ["widen to long text"] = new(t => Map(t, "s", c => c with { DataType = LongText(d) }), "s"),
            ["change type keeps not null and default"] = new(t => Map(t, "c", c => c with { DataType = Str(d, 30) }), "c"),
            ["make column not null"] = new(t => Map(t, "q", c => c with { IsNullable = false }), "q"),
            ["make column nullable"] = new(t => Map(t, "c", c => c with { IsNullable = true }), "c"),
            ["change default"] = new(t => Map(t, "c", c => c with { DefaultExpression = "'other'" }), "c"),
            ["drop default"] = new(t => Map(t, "c", c => c with { DefaultExpression = null }), "c"),
            ["composite primary key"] = new(t => Map(t, "q", c => c with { IsNullable = false }) with { PrimaryKey = ["id", "q"] }, "b"),
            ["drop primary key"] = new(t => t with { PrimaryKey = [] }, "a"),
            ["add unique index"] = new(t => t with { Indexes = [.. t.Indexes, new IndexDesign(null, "ux_b", true, ["b"])] }, "a"),
            ["drop unique index"] = new(t => t with { Indexes = [.. t.Indexes.Where(i => i.Name != "ux_a")] }, "a"),
            ["rename index"] = new(t => t with { Indexes = [.. t.Indexes.Select(i => i.Name == "ix_b" ? i with { Name = "ix_b2" } : i)] }, "a"),
            ["change index columns"] = new(t => t with { Indexes = [.. t.Indexes.Select(i => i.Name == "ix_b" ? i with { Columns = ["b", "q"] } : i)] }, "a"),
            ["drop foreign key"] = new(t => t with { ForeignKeys = [] }, "a"),
            ["rename table"] = new(t => t with { Name = "dm_t2" }, "a"),
            ["rename table and change type"] = new(t => Map(t, "n", c => c with { DataType = Int(d) }) with { Name = "dm_t2" }, "n"),
        };

        foreach (var action in Actions)
        {
            // InnoDB parses SET DEFAULT but refuses to create the key.
            if (d == DatabaseDialect.MySql && action == ReferentialAction.SetDefault)
                continue;
            var a = action;
            cases[$"add foreign key {a}"] = new(t => t with { ForeignKeys = [.. t.ForeignKeys, new ForeignKeyDesign(null, "fk_q", ["q"], schema, "dm_parent", ["id"], a, a)] }, "a");
        }

        if (d == DatabaseDialect.PostgreSql)
        {
            var sequence = $"nextval('{schema}.dm_seq')";
            cases["add column with sequence default"] = new(t => t with { Columns = [.. t.Columns, Col("n4", "bigint", true, sequence)] }, "a");
        }

        return cases;
    }

    private static async Task<List<object?[]>> RunAsync(IDatabaseConnection c, string sql)
    {
        await using var reader = await c.ExecuteAsync(sql, [], Ct);
        return await reader.ReadAllAsync(1000, Ct);
    }

    private static ReferentialAction Expected(DatabaseDialect d, ReferentialAction a) =>
        d == DatabaseDialect.SqlServer && a == ReferentialAction.Restrict ? ReferentialAction.NoAction : a;

    /// <summary>Creates the populated table <c>dm_t</c> and its parent <c>dm_parent</c>, dropping earlier ones.</summary>
    public static async Task SeedAsync(IDatabaseConnection c, DatabaseDialect d, string? schema)
    {
        var g = DdlGenerators.For(d);
        var parent = new TableDesign(schema, "dm_parent", [Col("id", Int(d), false)], ["id"], null, [], []);
        var t = new TableDesign(schema, "dm_t",
            [
                Col("id", Int(d), false), Col("a", Int(d), true), Col("b", Str(d, 50), true), Col("c", Str(d, 20), false, "'d'"),
                Col("p", Int(d), true), Col("q", Int(d), true), Col("n", Str(d, 20), true), Col("s", Str(d, 50), true),
            ],
            ["id"], null,
            [new IndexDesign(null, "ix_b", false, ["b"]), new IndexDesign(null, "ux_a", true, ["a"]), new IndexDesign(null, "ix_p", false, ["p"])],
            [new ForeignKeyDesign(null, "fk_p", ["p"], schema, "dm_parent", ["id"], ReferentialAction.NoAction, ReferentialAction.NoAction)]);

        foreach (var name in new[] { "dm_t", "dm_t2", "dm_parent" })
        {
            foreach (var sql in g.DropTable(schema, name))
                await RunAsync(c, d == DatabaseDialect.SqlServer ? $"IF OBJECT_ID(N'{(schema is null ? "" : schema + ".")}{name}') IS NOT NULL {sql}" : sql.Replace("DROP TABLE", "DROP TABLE IF EXISTS", StringComparison.Ordinal));
        }

        if (d == DatabaseDialect.PostgreSql)
        {
            await RunAsync(c, $"DROP SEQUENCE IF EXISTS {schema}.dm_seq");
            await RunAsync(c, $"CREATE SEQUENCE {schema}.dm_seq");
        }

        foreach (var sql in g.CreateTable(parent).Concat(g.CreateTable(t)))
            await RunAsync(c, sql);

        var q = (string name) => SqlQuoting.QualifiedName(schema, name, d);
        await RunAsync(c, $"INSERT INTO {q("dm_parent")} (id) VALUES (1), (2)");
        var rows = Enumerable.Range(1, 100).Select(i => $"({i}, {i * 3}, 'v{i}', {i % 2 + 1}, {i % 2 + 1}, '{i}', 's{i}')");
        await RunAsync(c, $"INSERT INTO {q("dm_t")} (id, a, b, p, q, n, s) VALUES {string.Join(", ", rows)}");
    }

    private static async Task<string> ColumnDataAsync(IDatabaseConnection c, DatabaseDialect d, string? schema, string table, string column)
    {
        var rows = await RunAsync(c, $"SELECT {SqlQuoting.Quote(column, d)} FROM {SqlQuoting.QualifiedName(schema, table, d)} ORDER BY {SqlQuoting.Quote("id", d)}");
        return string.Join("|", rows.Select(r => r[0]?.ToString() ?? "<null>"));
    }

    /// <summary>Applies one operation of the matrix and checks the resulting schema, the data and the generator's idempotence.</summary>
    public static async Task RunAsync(IDatabaseConnection c, DatabaseDialect d, string? schema, string name)
    {
        await SeedAsync(c, d, schema);
        var read = await c.ReadSchemaAsync(Ct);
        var original = TableDesign.FromTable(read.FindTable(schema, "dm_t")!);
        var step = Cases(d, schema)[name];
        var edited = step.Edit(original);
        var before = await ColumnDataAsync(c, d, schema, "dm_t", step.Check);
        var afterName = edited.Columns.First(x => x.OriginalName == step.Check).Name;

        Assert.DoesNotContain(DesignValidator.Validate(edited, original, d), i => i.Severity == DesignIssueSeverity.Error);
        foreach (var sql in DdlGenerators.For(d).Alter(original, edited))
            await RunAsync(c, sql);

        var table = (await c.ReadSchemaAsync(Ct)).FindTable(schema, edited.Name)!;
        Assert.Equal(edited.Columns.Select(x => x.Name), table.Columns.Select(x => x.Name));
        Assert.Equal(edited.Columns.Select(x => x.DataType.ToUpperInvariant()), table.Columns.Select(x => x.DataType.ToUpperInvariant()));
        Assert.Equal(edited.Columns.Select(x => x.IsNullable), table.Columns.Select(x => x.IsNullable));
        Assert.Equal(edited.Columns.Select(x => !string.IsNullOrWhiteSpace(x.DefaultExpression)), table.Columns.Select(x => !string.IsNullOrWhiteSpace(x.DefaultExpression)));
        Assert.Equal(edited.PrimaryKey, table.Columns.Where(x => x.IsPrimaryKey).Select(x => x.Name));

        // MySQL creates a same-named index behind a foreign key that has none.
        var implicitNames = d == DatabaseDialect.MySql ? edited.ForeignKeys.Select(f => f.Name).Except(edited.Indexes.Select(i => i.Name)) : [];
        Assert.Equal(
            edited.Indexes.Select(i => (i.Name, i.IsUnique, string.Join(",", i.Columns))).Order(),
            table.Indexes.Where(i => !implicitNames.Contains(i.Name)).Select(i => (i.Name, i.IsUnique, string.Join(",", i.Columns))).Order());
        Assert.Equal(
            edited.ForeignKeys.Select(f => (f.Name, string.Join(",", f.Columns), f.ReferencedTable, Expected(d, f.OnDelete), Expected(d, f.OnUpdate))).Order(),
            table.ForeignKeys.Select(f => (f.Name, string.Join(",", f.Columns), f.ReferencedTable, f.OnDelete, f.OnUpdate)).Order());

        Assert.Equal(before, await ColumnDataAsync(c, d, schema, edited.Name, afterName));
        Assert.Equal(100L, Convert.ToInt64((await RunAsync(c, $"SELECT count(*) FROM {SqlQuoting.QualifiedName(schema, edited.Name, d)}"))[0][0], System.Globalization.CultureInfo.InvariantCulture));
    }

    /// <summary>Checks that the validator, not the server, refuses to add or remove an identity / auto-increment column.</summary>
    public static async Task AutoIncrementChangeIsRefusedAsync(IDatabaseConnection c, DatabaseDialect d, string? schema)
    {
        await SeedAsync(c, d, schema);
        var original = TableDesign.FromTable((await c.ReadSchemaAsync(Ct)).FindTable(schema, "dm_t")!);
        var edited = Map(original, "id", x => x with { IsAutoIncrement = true });
        Assert.Contains(DesignValidator.Validate(edited, original, d), i => i.Code == CoreMessageCodes.DesignAutoIncrementChange);
    }

    /// <summary>Applies a script whose second statement fails and returns what the first one left behind.</summary>
    public static async Task<bool> FirstStatementSurvivesFailureAsync(IDatabaseConnection c, DatabaseDialect d, string? schema)
    {
        await SeedAsync(c, d, schema);
        var table = SqlQuoting.QualifiedName(schema, "dm_t", d);
        var keyword = d == DatabaseDialect.SqlServer ? "ADD" : "ADD COLUMN";
        await RunAsync(c, $"ALTER TABLE {table} {keyword} {SqlQuoting.Quote("first", d)} {Int(d)} NULL");
        await Assert.ThrowsAnyAsync<Exception>(() => RunAsync(c, $"ALTER TABLE {table} DROP COLUMN {SqlQuoting.Quote("missing", d)}"));
        return (await c.ReadSchemaAsync(Ct)).FindTable(schema, "dm_t")!.Columns.Any(x => x.Name == "first");
    }
}
