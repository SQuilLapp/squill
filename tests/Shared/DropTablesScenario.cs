using System.Globalization;
using Squill.Core;
using Squill.Core.Design;
using Squill.Data;
using Squill.Sql.Ddl;
using Squill.Sql.Transactions;
using Xunit;

namespace Squill.Data.Tests.Scenarios;

/// <summary>
/// Multi-table drop proof every engine runs against a live connection: the statements of <see cref="TableDropPlan"/> and
/// <see cref="DdlGenerators"/> drop tables that reference each other, the wrong order is refused, and a failure in a transaction leaves the
/// tables in place on every engine except MySQL, whose DDL commits implicitly.
/// </summary>
internal static class DropTablesScenario
{
    private static async Task RunAsync(IDatabaseConnection connection, string sql)
    {
        await using var reader = await connection.ExecuteAsync(sql, [], CancellationToken.None);
        await reader.ReadAllAsync(100, CancellationToken.None);
    }


    private static async Task<bool> ExistsAsync(IDatabaseConnection connection, string? schema, string name)
    {
        // Reads the catalog, so a broken connection throws instead of reading as "the table is gone".
        var snapshot = await connection.ReadSchemaAsync(CancellationToken.None);
        return snapshot.Tables.Any(t => t.Name == name && (schema is null || string.Equals(t.Schema, schema, StringComparison.OrdinalIgnoreCase)));
    }

    private static string Unique() => "p20_" + Guid.NewGuid().ToString("N")[..8];

    // Creates a table with an integer key, and a nullable ref_id column referencing the parent when one is given.
    private static async Task CreateAsync(IDatabaseConnection connection, DatabaseDialect dialect, string? schema, string name, string? parent)
    {
        var integer = dialect switch
        {
            DatabaseDialect.PostgreSql => "integer",
            DatabaseDialect.Sqlite => "INTEGER",
            DatabaseDialect.Oracle => "NUMBER(10)",
            _ => "int",
        };
        List<ColumnDesign> columns = [new(null, "id", integer, false, null, false)];
        List<ForeignKeyDesign> keys = [];
        if (parent is not null)
        {
            columns.Add(new(null, "ref_id", integer, true, null, false));
            keys.Add(new(null, "fk_" + name, ["ref_id"], schema, parent, ["id"], ReferentialAction.NoAction, ReferentialAction.NoAction));
        }

        foreach (var sql in DdlGenerators.For(dialect).CreateTable(new TableDesign(schema, name, columns, ["id"], null, [], keys)))
            await RunAsync(connection, sql);
    }

    private static async Task<TableDropPlan> PlanAsync(IDatabaseConnection connection, string? schema, params string[] selected)
    {
        var snapshot = await connection.ReadSchemaAsync(CancellationToken.None);
        var tables = selected.Select(n => snapshot.Tables.Single(t => t.Name == n)).ToList();
        return TableDropPlan.Build(tables, snapshot);
    }

    private static async Task DropAsync(IDatabaseConnection connection, DatabaseDialect dialect, string? schema, string name)
    {
        foreach (var sql in DdlGenerators.For(dialect).DropTable(schema, name))
            await RunAsync(connection, sql);
    }

    private static async Task CleanUpAsync(IDatabaseConnection connection, DatabaseDialect dialect, string? schema, params string[] childrenFirst)
    {
        foreach (var name in childrenFirst)
        {
            try
            {
                await DropAsync(connection, dialect, schema, name);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // Already dropped by the test.
            }
        }
    }

    /// <summary>Chain c -> b -> a: the planned order drops all three.</summary>
    /// <param name="connection">An open connection.</param>
    /// <param name="dialect">The engine of the connection.</param>
    /// <param name="schema">Schema for the test tables; null when the engine has none.</param>
    public static async Task OrderDropsReferencingTablesFirstAsync(IDatabaseConnection connection, DatabaseDialect dialect, string? schema)
    {
        string a = Unique() + "_a", b = Unique() + "_b", c = Unique() + "_c";
        try
        {
            await CreateAsync(connection, dialect, schema, a, null);
            await CreateAsync(connection, dialect, schema, b, a);
            await CreateAsync(connection, dialect, schema, c, b);

            // Selection order is the worst one: the parent first.
            var plan = await PlanAsync(connection, schema, a, b, c);
            Assert.Equal([c, b, a], plan.Order.Select(t => t.Name));
            foreach (var table in plan.Order)
                await DropAsync(connection, dialect, table.Schema, table.Name);

            foreach (var name in new[] { a, b, c })
                Assert.False(await ExistsAsync(connection, schema, name), name);
        }
        finally
        {
            await CleanUpAsync(connection, dialect, schema, c, b, a);
        }
    }

    /// <summary>Dropping the parent first is refused by the engine and nothing is dropped.</summary>
    /// <param name="connection">An open connection.</param>
    /// <param name="dialect">The engine of the connection.</param>
    /// <param name="schema">Schema for the test tables; null when the engine has none.</param>
    public static async Task WrongOrderIsRefusedAsync(IDatabaseConnection connection, DatabaseDialect dialect, string? schema)
    {
        string a = Unique() + "_a", b = Unique() + "_b", c = Unique() + "_c";
        try
        {
            if (dialect == DatabaseDialect.Sqlite)
            {
                // Microsoft.Data.Sqlite turns foreign keys on when it opens the connection; without them SQLite drops the parent silently.
                await using var reader = await connection.ExecuteAsync("PRAGMA foreign_keys", [], CancellationToken.None);
                var rows = await reader.ReadAllAsync(1, CancellationToken.None);
                Assert.NotEqual(0L, Convert.ToInt64(rows.Single()[0], CultureInfo.InvariantCulture));
            }

            await CreateAsync(connection, dialect, schema, a, null);
            await CreateAsync(connection, dialect, schema, b, a);
            await CreateAsync(connection, dialect, schema, c, b);

            // SQLite refuses the parent drop only when child rows would be orphaned.
            string Q(string identifier) => SqlQuoting.Quote(identifier, dialect);
            foreach (var (table, columns, values) in new[] { (a, "id", "1"), (b, "id, ref_id", "1, 1"), (c, "id, ref_id", "1, 1") })
                await RunAsync(connection, $"INSERT INTO {SqlQuoting.QualifiedName(schema, table, dialect)} ({string.Join(", ", columns.Split(", ").Select(Q))}) VALUES ({values})");

            await Assert.ThrowsAnyAsync<Exception>(() => DropAsync(connection, dialect, schema, a));

            foreach (var name in new[] { a, b, c })
                Assert.True(await ExistsAsync(connection, schema, name), name);
        }
        finally
        {
            await CleanUpAsync(connection, dialect, schema, c, b, a);
        }
    }

    /// <summary>A table outside the selection that references a selected table is reported as the only blocker.</summary>
    /// <param name="connection">An open connection.</param>
    /// <param name="dialect">The engine of the connection.</param>
    /// <param name="schema">Schema for the test tables; null when the engine has none.</param>
    public static async Task UnselectedReferencingTableBlocksAsync(IDatabaseConnection connection, DatabaseDialect dialect, string? schema)
    {
        string a = Unique() + "_a", d = Unique() + "_d";
        try
        {
            await CreateAsync(connection, dialect, schema, a, null);
            await CreateAsync(connection, dialect, schema, d, a);

            var plan = await PlanAsync(connection, schema, a);
            var blocker = Assert.Single(plan.Blockers);
            Assert.Equal(d, blocker.Referencing.Name);
            Assert.Equal(a, blocker.Referenced.Name);
        }
        finally
        {
            await CleanUpAsync(connection, dialect, schema, d, a);
        }
    }

    /// <summary>Two drops then a failing one inside a transaction: rolled back everywhere except on MySQL, where the drops stay.</summary>
    /// <param name="connection">An open connection.</param>
    /// <param name="dialect">The engine of the connection.</param>
    /// <param name="schema">Schema for the test tables; null when the engine has none.</param>
    public static async Task FailureInTransactionRollsBackOrIsPartialAsync(IDatabaseConnection connection, DatabaseDialect dialect, string? schema)
    {
        string x = Unique() + "_x", y = Unique() + "_y", missing = Unique() + "_missing";
        try
        {
            await CreateAsync(connection, dialect, schema, x, null);
            await CreateAsync(connection, dialect, schema, y, null);

            await RunAsync(connection, TransactionSql.Begin(dialect)!);
            await DropAsync(connection, dialect, schema, x);
            await DropAsync(connection, dialect, schema, y);
            await Assert.ThrowsAnyAsync<Exception>(() => DropAsync(connection, dialect, schema, missing));
            await RunAsync(connection, TransactionSql.Rollback(dialect));

            // MySQL committed each DROP implicitly, so the "partial" message of the drop flow is accurate there.
            var kept = DdlGenerators.UsesTransactionalDdl(dialect);
            Assert.Equal(kept, await ExistsAsync(connection, schema, x));
            Assert.Equal(kept, await ExistsAsync(connection, schema, y));
        }
        finally
        {
            await CleanUpAsync(connection, dialect, schema, x, y);
        }
    }
}
