using Squill.Core;
using Squill.Data;
using Squill.Sql.Completion;
using Xunit;

namespace Squill.Data.RoundTrip;

/// <summary>What differs between engines in <see cref="JoinCompletionScenario"/>: where the objects live and how types are spelled.</summary>
/// <param name="Dialect">The engine.</param>
/// <param name="Prefix">Schema prefix of the tables, with its dot (<c>sample.</c>), or empty.</param>
/// <param name="IntType">The spelling of a 32-bit integer column.</param>
/// <param name="ScriptName">The file of <c>SampleSchema</c> that creates the sample tables.</param>
internal sealed record JoinCompletionSpec(DatabaseDialect Dialect, string Prefix, string IntType, string ScriptName);

/// <summary>
/// Computes the completion of the join positions with the real schema reader and the real providers, applies each proposed row to the script
/// and runs the result on the engine: the generated text must be valid and join the expected rows. Shared by every driver test project.
/// </summary>
internal static class JoinCompletionScenario
{
    /// <summary>Loads the sample schema, seeds it, creates the scratch tables and runs every step; the scratch tables are dropped at the end.</summary>
    public static async Task RunAsync(IDatabaseConnection connection, JoinCompletionSpec spec)
    {
        var sample = await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, "SampleSchema", spec.ScriptName), TestContext.Current.CancellationToken);
        // GO is a client convention, not T-SQL: split the script on the lines that hold only GO.
        foreach (var batch in sample.Split("\nGO", StringSplitOptions.None).Where(b => !string.IsNullOrWhiteSpace(b)))
        {
            await RowsAsync(connection, batch, "sample schema");
        }

        var p = spec.Prefix;
        await RowsAsync(connection, $"insert into {p}customers (name, email) values ('a', 'a@x'), ('b', 'b@x'), ('c', 'c@x')", "seed");
        await RowsAsync(connection, $"insert into {p}orders (customer_id, total) select id, 5 from {p}customers", "seed");
        await RowsAsync(connection, $"insert into {p}order_items (order_id, line_no, sku) select id, 1, 'x' from {p}orders", "seed");

        var odd = $"{p}{Quote(spec.Dialect, "Odd Name")}";
        var oddId = Quote(spec.Dialect, "Odd Id");
        var i = spec.IntType;
        string[] create =
        [
            $"create table {p}cx_person (id {i} primary key, boss_id {i}, foreign key (boss_id) references {p}cx_person (id))",
            $"create table {p}cx_city (id {i} primary key)",
            $"create table {p}cx_trip (id {i} primary key, from_city_id {i}, to_city_id {i}, foreign key (from_city_id) references {p}cx_city (id), foreign key (to_city_id) references {p}cx_city (id))",
            $"create table {p}cx_pair_a (k1 {i} not null, k2 {i} not null, primary key (k1, k2))",
            $"create table {p}cx_pair_b (a1 {i}, a2 {i}, foreign key (a1, a2) references {p}cx_pair_a (k1, k2))",
            $"create table {odd} ({oddId} {i} primary key)",
            $"create table {p}cx_pair_c (id {i} primary key, odd_ref {i}, foreign key (odd_ref) references {odd} ({oddId}))",
        ];
        string[] drop = [$"{p}cx_pair_c", odd, $"{p}cx_pair_b", $"{p}cx_pair_a", $"{p}cx_trip", $"{p}cx_city", $"{p}cx_person"];
        try
        {
            foreach (var sql in create)
            {
                await RowsAsync(connection, sql, "scratch table");
            }

            await StepsAsync(connection, spec, await connection.ReadSchemaAsync(CancellationToken.None));
        }
        finally
        {
            foreach (var table in drop)
            {
                await RowsAsync(connection, $"drop table if exists {table}", "cleanup");
            }
        }
    }

    private static async Task StepsAsync(IDatabaseConnection connection, JoinCompletionSpec spec, SchemaSnapshot schema)
    {
        var d = spec.Dialect;
        var p = spec.Prefix;

        // Control: a failing statement must surface as an exception, or the steps below would prove nothing.
        await Assert.ThrowsAnyAsync<Exception>(() => RowsAsync(connection, $"select * from {p}cx_does_not_exist", "control"));

        // (1) the generated condition runs and joins the seeded rows
        Assert.Equal(3, await RowsAsync(connection, Apply(schema, d, $"select * from {p}customers c join |", "orders"), "join"));

        // (2) every join kind that takes a condition gets the same text
        var kinds = new List<string> { "inner join", "left join", "left outer join" };
        if (d != DatabaseDialect.Sqlite || await SqliteHasFullJoinAsync(connection))
        {
            kinds.Add("right join");
            if (d != DatabaseDialect.MySql)
            {
                kinds.Add("full join");
            }
        }

        string? reference = null;
        foreach (var kind in kinds)
        {
            var (text, offset) = Mark($"select * from {p}customers c {kind} |");
            var row = JoinRow(Complete(schema, d, text, offset), "orders");
            reference ??= row.InsertText;
            Assert.Equal(reference, row.InsertText);
            Assert.Equal(3, await RowsAsync(connection, Replace(text, row), kind));
        }

        if (d == DatabaseDialect.MySql)
        {
            var (text, offset) = Mark($"select * from {p}customers c |");
            Assert.DoesNotContain(Complete(schema, d, text, offset), x => x.Kind == CompletionKind.Keyword && x.FilterText == "FULL JOIN");
        }

        // (3) a child table of a joined table
        Assert.Equal(3, await RowsAsync(connection, Apply(schema, d, $"select * from {p}orders o join |", "order_items"), "child"));

        // (4) repeat / self join: two rows (each side), both run
        await AllRowsRunAsync(connection, schema, d, $"select * from {p}cx_person e join |", "cx_person", expected: 2);

        // (5) two foreign keys to the same table
        await AllRowsRunAsync(connection, schema, d, $"select * from {p}cx_trip t join |", "cx_city", expected: 2);

        // (6) composite key
        var composite = await AllRowsRunAsync(connection, schema, d, $"select * from {p}cx_pair_a a join |", "cx_pair_b", expected: 1);
        Assert.Contains(" AND ", composite[0].InsertText, StringComparison.OrdinalIgnoreCase);

        // (7) a name with a space and a capital is quoted
        var odd = await AllRowsRunAsync(connection, schema, d, $"select * from {p}cx_pair_c c join |", "Odd Name", expected: 1);
        Assert.Contains(Quote(d, "Odd Name"), odd[0].InsertText, StringComparison.Ordinal);
        Assert.Contains(Quote(d, "Odd Id"), odd[0].InsertText, StringComparison.Ordinal);

        // (8) no condition after CROSS or NATURAL
        foreach (var join in new[] { "cross join", "natural join" })
        {
            var (text, offset) = Mark($"select * from {p}customers c {join} |");
            Assert.DoesNotContain(Complete(schema, d, text, offset), x => x.Detail?.StartsWith("ON ", StringComparison.OrdinalIgnoreCase) == true);
        }

        // (9) the conditions proposed once `on ` is typed
        var (onText, onOffset) = Mark($"select * from {p}customers c join {p}orders o on |");
        var conditions = Complete(schema, d, onText, onOffset).Where(x => x.Kind == CompletionKind.Snippet).ToList();
        Assert.NotEmpty(conditions);
        foreach (var condition in conditions)
        {
            Assert.Equal(3, await RowsAsync(connection, Replace(onText, condition), "on"));
        }
    }

    private static async Task<List<CompletionItem>> AllRowsRunAsync(IDatabaseConnection connection, SchemaSnapshot schema, DatabaseDialect d, string marked, string table, int expected)
    {
        var (text, offset) = Mark(marked);
        var rows = Complete(schema, d, text, offset).Where(i => IsJoinRow(i, table)).ToList();
        Assert.Equal(expected, rows.Count);
        Assert.Equal(rows.Count, rows.Select(r => r.InsertText).Distinct().Count());
        foreach (var row in rows)
        {
            await RowsAsync(connection, Replace(text, row), "row");
        }

        return rows;
    }

    private static bool IsJoinRow(CompletionItem item, string table) =>
        item.Kind == CompletionKind.Struct && item.FilterText == table && item.Detail?.StartsWith("ON ", StringComparison.OrdinalIgnoreCase) == true;

    private static string Apply(SchemaSnapshot schema, DatabaseDialect d, string marked, string table)
    {
        var (text, offset) = Mark(marked);
        return Replace(text, JoinRow(Complete(schema, d, text, offset), table));
    }

    private static CompletionItem JoinRow(IReadOnlyList<CompletionItem> items, string table) => items.Single(i => IsJoinRow(i, table));

    private static IReadOnlyList<CompletionItem> Complete(SchemaSnapshot schema, DatabaseDialect d, string text, int offset) =>
        new CompositeCompletionProvider(new KeywordCompletionProvider(), new SchemaCompletionProvider()).GetCompletions(new SqlRequest(text, offset, d, schema));

    private static (string Text, int Offset) Mark(string marked)
    {
        var offset = marked.IndexOf('|', StringComparison.Ordinal);
        return (marked.Remove(offset, 1), offset);
    }

    private static string Replace(string text, CompletionItem item) =>
        text[..item.ReplaceRange.Start] + item.InsertText + text[(item.ReplaceRange.Start + item.ReplaceRange.Length)..];

    private static string Quote(DatabaseDialect d, string name) => d switch
    {
        DatabaseDialect.MySql => $"`{name}`",
        DatabaseDialect.SqlServer => $"[{name}]",
        _ => $"\"{name}\"",
    };

    private static async Task<bool> SqliteHasFullJoinAsync(IDatabaseConnection connection)
    {
        await using var reader = await connection.ExecuteAsync("select sqlite_version()", [], CancellationToken.None);
        var version = Convert.ToString((await reader.ReadAllAsync(1, CancellationToken.None))[0][0], System.Globalization.CultureInfo.InvariantCulture)!;
        return Version.Parse(version) >= new Version(3, 39);
    }

    private static async Task<int> RowsAsync(IDatabaseConnection connection, string sql, string step)
    {
        try
        {
            await using var reader = await connection.ExecuteAsync(sql, [], CancellationToken.None);
            return (await reader.ReadAllAsync(100, CancellationToken.None)).Count;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            throw new Xunit.Sdk.XunitException($"{step}: the statement failed: {sql}{Environment.NewLine}{ex.Message}", ex);
        }
    }
}
