using System.Globalization;
using Squill.Core;
using Squill.Core.Design;
using Squill.Core.Editing;
using Squill.Sql.Ddl;
using Squill.Sql.Editing;
using Xunit;

namespace Squill.Data.RoundTrip;

/// <summary>The DML scenario every dialect's <c>DmlRoundTripTests</c> runs against a live connection.</summary>
internal static class DmlScenario
{
    private static CancellationToken Ct => CancellationToken.None;

    /// <summary>Runs the key, default-value, update, delete and composite-key steps.</summary>
    public static async Task RunAsync(IDatabaseConnection c, DatabaseDialect d, string? schema)
    {
        var text = d switch { DatabaseDialect.Sqlite => "TEXT", DatabaseDialect.SqlServer => "nvarchar(100)", _ => "varchar(100)" };
        var integer = d == DatabaseDialect.Sqlite ? "INTEGER" : "int";
        var items = new TableDesign(schema, "dml_items",
            [Col("id", integer, false, auto: true), Col("name", text, false), Col("qty", integer, true), Col("price", "decimal(10,2)", true)],
            ["id"], null, [], []);
        var defaults = new TableDesign(schema, "dml_defaults_only",
            [Col("id", integer, false, auto: true), Col("note", text, true, "'x'"), Col("qty", integer, true, "7")],
            ["id"], null, [], []);
        var composite = new TableDesign(schema, "dml_composite",
            [Col("a", integer, false), Col("b", integer, false), Col("label", text, true)],
            ["a", "b"], null, [], []);
        await CreateAsync(c, d, items, defaults, composite);

        // Generated key, read back the way EditCommitter does.
        var k1 = await InsertAsync(c, d, items, ("name", "a"), ("qty", 1), ("price", 1.50m));
        Assert.NotNull(k1);
        await AssertRowsAsync(c, d, items, "id", [[k1, "a", 1, 1.50m]]);

        // Explicit key; SQL Server refuses explicit identity values without IDENTITY_INSERT, the composite table covers that case there.
        var explicitKey = d != DatabaseDialect.SqlServer;
        if (explicitKey)
        {
            await InsertAsync(c, d, items, ("id", 100), ("name", "b"), ("price", 2.25m));
            await AssertRowsAsync(c, d, items, "id", [[k1, "a", 1, 1.50m], [100, "b", null, 2.25m]]);
        }

        await ExecAsync(c, d, items, new UpdateRow([new("id", k1)], [new("name", "renamed"), new("qty", 5)]), 1);
        await ExecAsync(c, d, items, new UpdateRow([new("id", k1)], [new("price", null)]), 1);
        object?[][] others = explicitKey ? [[100, "b", null, 2.25m]] : [];
        await AssertRowsAsync(c, d, items, "id", [[k1, "renamed", 5, null], .. others]);
        await ExecAsync(c, d, items, new DeleteRow([new("id", k1)]), 1);
        await ExecAsync(c, d, items, new DeleteRow([new("id", k1)]), 0);
        await AssertRowsAsync(c, d, items, "id", others);

        // No values: every column falls back to its default.
        var k2 = await InsertAsync(c, d, defaults);
        Assert.NotNull(k2);
        await AssertRowsAsync(c, d, defaults, "id", [[k2, "x", 7]]);

        // Composite key: explicit values, key column update, NULL, delete.
        await InsertAsync(c, d, composite, ("a", 1), ("b", 1), ("label", "x"));
        await InsertAsync(c, d, composite, ("a", 1), ("b", 2), ("label", "y"));
        await ExecAsync(c, d, composite, new UpdateRow([new("a", 1), new("b", 2)], [new("b", 3)]), 1);
        await ExecAsync(c, d, composite, new UpdateRow([new("a", 1), new("b", 1)], [new("label", null)]), 1);
        await AssertRowsAsync(c, d, composite, "a, b", [[1, 1, null], [1, 3, "y"]]);
        await ExecAsync(c, d, composite, new DeleteRow([new("a", 1), new("b", 3)]), 1);
        await AssertRowsAsync(c, d, composite, "a, b", [[1, 1, null]]);

        if (d == DatabaseDialect.Sqlite)
        {
            // SQLite allows NULL in a non-integer primary key, so the IS NULL key predicate can match a row.
            var nullKey = new TableDesign(schema, "dml_null_key", [Col("a", "TEXT", true), Col("v", "INTEGER", true)], ["a"], null, [], []);
            await CreateAsync(c, d, nullKey);
            await InsertAsync(c, d, nullKey, ("a", null), ("v", 1));
            await ExecAsync(c, d, nullKey, new UpdateRow([new("a", null)], [new("v", 2)]), 1);
            await AssertRowsAsync(c, d, nullKey, "v", [[null, 2]]);
            await ExecAsync(c, d, nullKey, new DeleteRow([new("a", null)]), 1);
            await AssertRowsAsync(c, d, nullKey, "v", []);
        }
    }

    /// <summary>Binds one value of every CLR type the grid can edit and reads it back.</summary>
    public static async Task RunTypedAsync(IDatabaseConnection c, DatabaseDialect d, string? schema)
    {
        var integer = d == DatabaseDialect.Sqlite ? "INTEGER" : "int";
        var text = d switch { DatabaseDialect.Sqlite => "TEXT", DatabaseDialect.SqlServer => "nvarchar(100)", _ => "varchar(100)" };
        var typed = new TableDesign(schema, "dml_typed",
            [
                Col("id", integer, false),
                Col("i", integer, true),
                Col("l", "bigint", true),
                Col("m", "decimal(10,2)", true),
                Col("s", text, true),
                Col("b", d switch { DatabaseDialect.Sqlite => "INTEGER", DatabaseDialect.SqlServer => "bit", _ => "boolean" }, true),
                Col("t", d switch { DatabaseDialect.Sqlite => "TEXT", DatabaseDialect.PostgreSql => "timestamp", DatabaseDialect.MySql => "datetime", _ => "datetime2" }, true),
                Col("bin", d switch { DatabaseDialect.PostgreSql => "bytea", DatabaseDialect.SqlServer => "varbinary(max)", _ => "blob" }, true),
                Col("g", d switch { DatabaseDialect.PostgreSql => "uuid", DatabaseDialect.SqlServer => "uniqueidentifier", DatabaseDialect.MySql => "char(36)", _ => "TEXT" }, true),
            ],
            ["id"], null, [], []);
        await CreateAsync(c, d, typed);

        var guid = Guid.Parse("6f9619ff-8b86-d011-b42d-00c04fc964ff");
        object?[] row = [1, 42, 9_000_000_000L, 12345.67m, "héllo ' \"quoted\"", true, new DateTime(2024, 5, 6, 7, 8, 9), new byte[] { 0, 1, 254, 255 }, guid];
        string[] names = ["id", "i", "l", "m", "s", "b", "t", "bin", "g"];
        await InsertAsync(c, d, typed, [.. names.Zip(row, (n, v) => (n, v))]);
        await AssertRowsAsync(c, d, typed, "id", [row]);

        await ExecAsync(c, d, typed, new UpdateRow([new("id", 1)], [new("s", "changed"), new("b", false), new("g", null)]), 1);
        row[4] = "changed";
        row[5] = false;
        row[8] = null;
        await AssertRowsAsync(c, d, typed, "id", [row]);
    }

    private static ColumnDesign Col(string name, string type, bool nullable, string? def = null, bool auto = false) =>
        new(null, name, type, nullable, def, auto);

    private static EditTarget Target(TableDesign t)
    {
        var columns = t.Columns
            .Select(c => new ColumnDefinition(c.Name, c.DataType, c.IsNullable, t.PrimaryKey.Contains(c.Name), c.DefaultExpression, c.IsAutoIncrement))
            .ToList();
        return new EditTarget(t.Schema, t.Name, [.. columns.Select((def, i) => new EditColumn(i, def))], t.PrimaryKey, new TableInfo(t.Schema, t.Name, TableKind.Table, columns, [], []));
    }

    private static async Task CreateAsync(IDatabaseConnection c, DatabaseDialect d, params TableDesign[] tables)
    {
        foreach (var t in tables)
        {
            await RunAsync(c, $"DROP TABLE IF EXISTS {SqlQuoting.QualifiedName(t.Schema, t.Name, d)}", []);
            foreach (var sql in DdlGenerators.For(d).CreateTable(t))
                await RunAsync(c, sql, []);
        }
    }

    private static async Task<(List<object?[]> Rows, long Affected)> RunAsync(IDatabaseConnection c, string sql, IReadOnlyList<DmlParameter> ps)
    {
        await using var reader = await c.ExecuteAsync(sql, [.. ps.Select(p => new QueryParameter(p.Name, p.Value))], Ct);
        var rows = await reader.ReadAllAsync(100, Ct);
        return (rows, reader.RecordsAffected);
    }

    // Returns the generated key, or null when the insert does not generate one.
    private static async Task<object?> InsertAsync(IDatabaseConnection c, DatabaseDialect d, TableDesign t, params (string Column, object? Value)[] values)
    {
        var plan = DmlGenerator.PlanInsert(new InsertRow([.. values.Select(v => new ColumnValue(v.Column, v.Value))]), Target(t), d);
        var (rows, _) = await RunAsync(c, plan.Insert.Sql, plan.Insert.Parameters);
        if (plan.KeyQuery is not null)
            rows = (await RunAsync(c, plan.KeyQuery.Sql, plan.KeyQuery.Parameters)).Rows;
        return rows.Count > 0 ? rows[0][0] : null;
    }

    private static async Task ExecAsync(IDatabaseConnection c, DatabaseDialect d, TableDesign t, RowEdit edit, long affected)
    {
        var statement = DmlGenerator.Generate(edit, Target(t), d);
        Assert.Equal(affected, (await RunAsync(c, statement.Sql, statement.Parameters)).Affected);
    }

    private static async Task AssertRowsAsync(IDatabaseConnection c, DatabaseDialect d, TableDesign t, string orderBy, object?[][] expected)
    {
        var cols = string.Join(", ", t.Columns.Select(x => SqlQuoting.Quote(x.Name, d)));
        var order = string.Join(", ", orderBy.Split(", ").Select(x => SqlQuoting.Quote(x, d)));
        var (rows, _) = await RunAsync(c, $"SELECT {cols} FROM {SqlQuoting.QualifiedName(t.Schema, t.Name, d)} ORDER BY {order}", []);
        Assert.Equal(expected.Length, rows.Count);
        for (var r = 0; r < expected.Length; r++)
        {
            for (var i = 0; i < expected[r].Length; i++)
                Assert.True(Equivalent(expected[r][i], rows[r][i]), $"{t.Name} row {r} column {t.Columns[i].Name}: expected {expected[r][i]} ({expected[r][i]?.GetType().Name}) but read {(rows[r][i] is double dv ? dv.ToString("R", CultureInfo.InvariantCulture) : rows[r][i])} ({rows[r][i]?.GetType().Name})");
        }
    }

    // Providers hand back different CLR types for the same value (int/long, bool/0-1, DateTime/ISO text), so compare by meaning.
    private static bool Equivalent(object? expected, object? actual)
    {
        if (expected is null || actual is null || actual is DBNull)
            return expected is null && actual is null or DBNull;
        return expected switch
        {
            bool b => (actual is bool ab ? ab : Convert.ToInt64(actual, CultureInfo.InvariantCulture) != 0) == b,
            // SQLite gives a decimal column REAL affinity, so it reads back as a double: compare with a tolerance there.
            int or long or decimal => actual is double or float
                ? Math.Abs(Convert.ToDouble(actual, CultureInfo.InvariantCulture) - Convert.ToDouble(expected, CultureInfo.InvariantCulture)) < 1e-9
                : Convert.ToDecimal(actual, CultureInfo.InvariantCulture) == Convert.ToDecimal(expected, CultureInfo.InvariantCulture),
            DateTime t => (actual as DateTime? ?? DateTime.Parse(actual.ToString()!, CultureInfo.InvariantCulture)) == t,
            Guid g => Guid.Parse(actual.ToString()!) == g,
            byte[] bytes => actual is byte[] bs && bs.SequenceEqual(bytes),
            _ => Equals(expected, actual),
        };
    }
}
