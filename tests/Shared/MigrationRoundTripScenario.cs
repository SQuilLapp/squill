using Squill.Core;
using Squill.Core.Design;
using Squill.Core.Schema.Compare;
using Squill.Data;
using Squill.Sql.Ddl;
using Squill.Sql.Migration;
using Squill.Sql.Statements;
using Xunit;

namespace Squill.Data.Tests.Scenarios;

/// <summary>
/// Migration proof every engine runs against a live connection: the database is put in a "source" state and read, then changed to a "target"
/// state and read, the script that turns the target into the source is applied, and a new comparison must find no difference.
/// </summary>
internal static class MigrationRoundTripScenario
{
    private static async Task RunAsync(IDatabaseConnection connection, string sql)
    {
        await using var reader = await connection.ExecuteAsync(sql, [], CancellationToken.None);
        await reader.ReadAllAsync(100, CancellationToken.None);
    }

    private static async Task RunAllAsync(IDatabaseConnection connection, IEnumerable<string> statements)
    {
        foreach (var sql in statements)
            await RunAsync(connection, sql);
    }

    /// <summary>Runs the scenario.</summary>
    /// <param name="connection">An open connection to an empty (or sample-schema) database.</param>
    /// <param name="dialect">The engine of the connection.</param>
    /// <param name="schema">Schema for the test tables; null when the engine has none.</param>
    public static async Task RunAsync(IDatabaseConnection connection, DatabaseDialect dialect, string? schema)
    {
        var ddl = DdlGenerators.For(dialect);
        var integer = dialect == DatabaseDialect.PostgreSql ? "integer" : dialect == DatabaseDialect.Sqlite ? "INTEGER" : "int";
        string Text(int n) => dialect switch
        {
            DatabaseDialect.SqlServer => $"nvarchar({n})",
            DatabaseDialect.Sqlite => $"VARCHAR({n})",
            _ => $"varchar({n})",
        };

        ColumnDesign Col(string name, string type, bool nullable) => new(null, name, type, nullable, null, false);
        TableDesign Parent(int nameLength, bool withNote) => new(schema, "mig_a",
            [Col("id", integer, false), Col("name", Text(nameLength), true), .. withNote ? [Col("note", Text(10), true)] : Array.Empty<ColumnDesign>()],
            ["id"], null, withNote ? [new IndexDesign(null, "ix_mig_a_name", false, ["name"])] : [], []);
        var child = new TableDesign(schema, "mig_b", [Col("id", integer, false), Col("a_id", integer, true)], ["id"], null, [],
            [new ForeignKeyDesign(null, "fk_mig_b_a", ["a_id"], schema, "mig_a", ["id"], ReferentialAction.Cascade, ReferentialAction.NoAction)]);
        var old = new TableDesign(schema, "mig_old", [Col("id", integer, false)], ["id"], null, [], []);

        // Source state, read; then the target state, read.
        await RunAllAsync(connection, ddl.CreateTable(Parent(20, true)).Concat(ddl.CreateTable(child)));
        var source = await connection.ReadSchemaAsync(CancellationToken.None);
        await RunAllAsync(connection, ddl.DropTable(schema, "mig_b").Concat(ddl.DropTable(schema, "mig_a")));
        await RunAllAsync(connection, ddl.CreateTable(Parent(10, false)).Concat(ddl.CreateTable(old)));
        var target = await connection.ReadSchemaAsync(CancellationToken.None);

        var comparison = SchemaComparer.Compare(source, target);
        Assert.NotEmpty(comparison.Differences);
        var script = MigrationScriptBuilder.Build(dialect, source, target, comparison, comparison.Differences.Select(ObjectKey.Of).ToHashSet(),
            new MigrationOptions("source", "target", new DateTime(2026, 1, 2, 3, 4, 0)));
        Assert.Contains(script.Notes, n => n.Kind == MigrationNoteKind.DropsData);

        foreach (var span in SqlStatementSplitter.Split(script.Text, dialect))
        {
            var statement = script.Text.Substring(span.Start, span.Length);
            if (!string.IsNullOrWhiteSpace(statement))
                await RunAsync(connection, statement);
        }

        var after = await connection.ReadSchemaAsync(CancellationToken.None);
        var left = SchemaComparer.Compare(source, after).Differences;
        Assert.True(left.Count == 0, "Differences left after the migration: " + string.Join(", ", left.Select(x => $"{x.Category} {x.Name} {x.Kind}")));
    }
}
