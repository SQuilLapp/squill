using Squill.Core;
using Squill.Core.Plans;
using Squill.Sql.Transactions;
using Xunit;

namespace Squill.Data.RoundTrip;

/// <summary>What an engine does inside a transaction, so one scenario serves every dialect.</summary>
/// <param name="DdlTransactional"><c>CREATE TABLE</c> is rolled back with the transaction.</param>
/// <param name="ErrorAbortsTransaction">A failed statement makes the following ones fail until a rollback (PostgreSQL).</param>
/// <param name="OtherConnectionCanReadBeforeCommit">A second connection reads the committed state while a transaction writes.</param>
/// <param name="ProbeKnowsTransactions">The probe reports whether a transaction is open.</param>
/// <param name="ProbeKnowsFailure">The probe reports an aborted transaction.</param>
/// <param name="SupportsActualPlan">The connection can run an actual plan.</param>
internal sealed record EngineExpectations(
    bool DdlTransactional,
    bool ErrorAbortsTransaction,
    bool OtherConnectionCanReadBeforeCommit,
    bool ProbeKnowsTransactions,
    bool ProbeKnowsFailure,
    bool SupportsActualPlan);

/// <summary>The transaction scenarios every engine's <c>TransactionTests</c> runs, built on <see cref="TransactionSql"/>.</summary>
internal static class TransactionScenario
{
    private const string Table = "tx_items";
    private static CancellationToken Ct => CancellationToken.None;

    /// <summary>Runs every scenario; <paramref name="open"/> must return a new connection to the same database each time.</summary>
    public static async Task RunAsync(IDatabaseProvider provider, Func<ValueTask<IDatabaseConnection>> open, EngineExpectations e)
    {
        var d = provider.Dialect;
        await using var a = await open();
        await using var b = await open();

        await CommitMakesRowsVisibleAsync(d, a, b, e);
        await RollbackDiscardsChangesAsync(d, a, b);
        await DdlInTransactionAsync(d, a, e);
        await SavepointAsync(d, a, b);
        await ErrorInTransactionAsync(d, a, b, e);
        await ProbeAsync(d, a, e);
        await DisposeDiscardsAsync(d, open, b);
        if (e.SupportsActualPlan)
            await ActualPlanKeepsTransactionAsync(d, a, b);
        await ExecAsync(a, $"DROP TABLE IF EXISTS {Table}");
    }

    private static async Task ResetAsync(DatabaseDialect d, IDatabaseConnection c)
    {
        var text = d == DatabaseDialect.SqlServer ? "nvarchar(100)" : d == DatabaseDialect.Sqlite ? "TEXT" : "varchar(100)";
        var integer = d == DatabaseDialect.Sqlite ? "INTEGER" : "int";
        await ExecAsync(c, $"DROP TABLE IF EXISTS {Table}");
        await ExecAsync(c, $"CREATE TABLE {Table} (id {integer} NOT NULL PRIMARY KEY, name {text})");
    }

    private static async Task ExecAsync(IDatabaseConnection c, string sql)
    {
        await using var reader = await c.ExecuteAsync(sql, [], Ct);
        await reader.ReadAllAsync(100, Ct);
    }

    private static async Task<List<long>> IdsAsync(IDatabaseConnection c)
    {
        await using var reader = await c.ExecuteAsync($"SELECT id FROM {Table} ORDER BY id", [], Ct);
        return [.. (await reader.ReadAllAsync(100, Ct)).Select(r => Convert.ToInt64(r[0], System.Globalization.CultureInfo.InvariantCulture))];
    }

    private static Task BeginAsync(DatabaseDialect d, IDatabaseConnection c) => ExecAsync(c, TransactionSql.Begin(d)!);

    private static async Task CommitMakesRowsVisibleAsync(DatabaseDialect d, IDatabaseConnection a, IDatabaseConnection b, EngineExpectations e)
    {
        await ResetAsync(d, a);
        await BeginAsync(d, a);
        await ExecAsync(a, $"INSERT INTO {Table} (id, name) VALUES (1, 'a')");
        Assert.Equal([1L], await IdsAsync(a));
        if (e.OtherConnectionCanReadBeforeCommit)
            Assert.Empty(await IdsAsync(b));
        await ExecAsync(a, TransactionSql.Commit(d));
        Assert.Equal([1L], await IdsAsync(b));
    }

    private static async Task RollbackDiscardsChangesAsync(DatabaseDialect d, IDatabaseConnection a, IDatabaseConnection b)
    {
        await ResetAsync(d, a);
        await ExecAsync(a, $"INSERT INTO {Table} (id, name) VALUES (1, 'a'), (2, 'b'), (3, 'c')");
        await BeginAsync(d, a);
        await ExecAsync(a, $"INSERT INTO {Table} (id, name) VALUES (4, 'd')");
        await ExecAsync(a, $"UPDATE {Table} SET name = 'z' WHERE id = 1");
        await ExecAsync(a, $"DELETE FROM {Table} WHERE id = 2");
        Assert.Equal([1L, 3L, 4L], await IdsAsync(a));
        await ExecAsync(a, TransactionSql.Rollback(d));
        Assert.Equal([1L, 2L, 3L], await IdsAsync(b));
        await using var reader = await b.ExecuteAsync($"SELECT name FROM {Table} WHERE id = 1", [], Ct);
        Assert.Equal("a", (await reader.ReadAllAsync(10, Ct)).Single()[0]);
    }

    private static async Task DdlInTransactionAsync(DatabaseDialect d, IDatabaseConnection a, EngineExpectations e)
    {
        await ExecAsync(a, "DROP TABLE IF EXISTS tx_ddl");
        await BeginAsync(d, a);
        await ExecAsync(a, "CREATE TABLE tx_ddl (id " + (d == DatabaseDialect.Sqlite ? "INTEGER" : "int") + ")");
        await ExecAsync(a, TransactionSql.Rollback(d));
        var exists = await TableExistsAsync(a, "tx_ddl");
        Assert.Equal(!e.DdlTransactional, exists);
        await ExecAsync(a, "DROP TABLE IF EXISTS tx_ddl");
    }

    private static async Task<bool> TableExistsAsync(IDatabaseConnection c, string name)
    {
        try
        {
            await ExecAsync(c, $"SELECT COUNT(*) FROM {name}");
            return true;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return false;
        }
    }

    private static async Task SavepointAsync(DatabaseDialect d, IDatabaseConnection a, IDatabaseConnection b)
    {
        const string sp = "squill_sp_1";
        await ResetAsync(d, a);
        await BeginAsync(d, a);
        await ExecAsync(a, $"INSERT INTO {Table} (id, name) VALUES (1, 'a')");
        await ExecAsync(a, TransactionSql.Savepoint(d, sp));
        await ExecAsync(a, $"INSERT INTO {Table} (id, name) VALUES (2, 'b')");
        await ExecAsync(a, TransactionSql.RollbackToSavepoint(d, sp));
        await ExecAsync(a, TransactionSql.Commit(d));
        Assert.Equal([1L], await IdsAsync(b));
    }

    private static async Task ErrorInTransactionAsync(DatabaseDialect d, IDatabaseConnection a, IDatabaseConnection b, EngineExpectations e)
    {
        const string sp = "squill_sp_1";
        await ResetAsync(d, a);
        await BeginAsync(d, a);
        await ExecAsync(a, $"INSERT INTO {Table} (id, name) VALUES (1, 'a')");
        await ExecAsync(a, TransactionSql.Savepoint(d, sp));
        await Assert.ThrowsAnyAsync<Exception>(() => ExecAsync(a, $"INSERT INTO {Table} (id, name) VALUES (1, 'dup')"));
        if (e.ProbeKnowsFailure)
            Assert.Equal(e.ErrorAbortsTransaction, (await ((ITransactionProbe)a).ProbeAsync(Ct)).Failed);
        if (e.ErrorAbortsTransaction)
        {
            await Assert.ThrowsAnyAsync<Exception>(() => ExecAsync(a, $"INSERT INTO {Table} (id, name) VALUES (2, 'b')"));
            await ExecAsync(a, TransactionSql.RollbackToSavepoint(d, sp));
            Assert.False((await ((ITransactionProbe)a).ProbeAsync(Ct)).Failed);
            await ExecAsync(a, TransactionSql.Commit(d));
            Assert.Equal([1L], await IdsAsync(b));
        }
        else
        {
            await ExecAsync(a, $"INSERT INTO {Table} (id, name) VALUES (2, 'b')");
            await ExecAsync(a, TransactionSql.Commit(d));
            Assert.Equal([1L, 2L], await IdsAsync(b));
        }
    }

    private static async Task ProbeAsync(DatabaseDialect d, IDatabaseConnection a, EngineExpectations e)
    {
        var probe = Assert.IsAssignableFrom<ITransactionProbe>(a);
        await ResetAsync(d, a);
        var idle = await probe.ProbeAsync(Ct);
        Assert.False(idle.Failed);
        await BeginAsync(d, a);
        Assert.Equal(e.ProbeKnowsTransactions ? true : null, (await probe.ProbeAsync(Ct)).InTransaction);
        await ExecAsync(a, TransactionSql.Commit(d));
        Assert.Equal(e.ProbeKnowsTransactions ? false : null, (await probe.ProbeAsync(Ct)).InTransaction);
        await BeginAsync(d, a);
        await ExecAsync(a, TransactionSql.Rollback(d));
        Assert.Equal(e.ProbeKnowsTransactions ? false : null, (await probe.ProbeAsync(Ct)).InTransaction);
    }

    private static async Task DisposeDiscardsAsync(DatabaseDialect d, Func<ValueTask<IDatabaseConnection>> open, IDatabaseConnection b)
    {
        await ResetAsync(d, b);
        var c = await open();
        await BeginAsync(d, c);
        await ExecAsync(c, $"INSERT INTO {Table} (id, name) VALUES (1, 'a')");
        await c.DisposeAsync();
        Assert.Empty(await IdsAsync(b));
    }

    private static async Task ActualPlanKeepsTransactionAsync(DatabaseDialect d, IDatabaseConnection a, IDatabaseConnection b)
    {
        await ResetAsync(d, a);
        await BeginAsync(d, a);
        await ExecAsync(a, $"INSERT INTO {Table} (id, name) VALUES (1, 'a')");
        var reader = Assert.IsAssignableFrom<IPlanReader>(a);
        await reader.ExplainAsync($"SELECT * FROM {Table}", [], PlanMode.Actual, Ct);
        Assert.Equal([1L], await IdsAsync(a));
        await ExecAsync(a, TransactionSql.Rollback(d));
        Assert.Empty(await IdsAsync(b));
    }
}
