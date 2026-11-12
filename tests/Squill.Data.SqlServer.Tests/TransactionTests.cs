using Squill.Core;
using Squill.Core.Plans;
using Squill.Core.Transactions;
using Squill.Data.RoundTrip;
using Squill.Sql.Transactions;
using Xunit;

namespace Squill.Data.SqlServer.Tests;

public sealed class TransactionTests(SqlServerFixture server) : IClassFixture<SqlServerFixture>
{
    private const string Table = "tx_ms_extra";
    private static readonly DatabaseDialect Dialect = DatabaseDialect.SqlServer;

    private async ValueTask<IDatabaseConnection> OpenAsync() => await new SqlServerDatabaseProvider().OpenAsync(
        new ConnectionProfile(Guid.NewGuid(), "tx", Dialect, server.Host, server.Port, SqlServerFixture.Database, SqlServerFixture.UserName, null, null),
        SqlServerFixture.Password,
        CancellationToken.None);

    private static async Task ExecAsync(IDatabaseConnection c, string sql)
    {
        await using var reader = await c.ExecuteAsync(sql, [], CancellationToken.None);
        await reader.ReadAllAsync(100, CancellationToken.None);
    }

    private static async Task<long> ScalarAsync(IDatabaseConnection c, string sql)
    {
        await using var reader = await c.ExecuteAsync(sql, [], CancellationToken.None);
        return Convert.ToInt64((await reader.ReadAllAsync(1, CancellationToken.None)).Single()[0], System.Globalization.CultureInfo.InvariantCulture);
    }

    private static Task<long> TranCountAsync(IDatabaseConnection c) => ScalarAsync(c, "SELECT @@TRANCOUNT");

    private static Task<long> RowsAsync(IDatabaseConnection c) => ScalarAsync(c, $"SELECT COUNT(*) FROM {Table}");

    private static Task<TransactionProbeResult> ProbeAsync(IDatabaseConnection c) => ((ITransactionProbe)c).ProbeAsync(CancellationToken.None).AsTask();

    private async Task<IDatabaseConnection> PrepareAsync()
    {
        var a = await OpenAsync();
        await ExecAsync(a, $"DROP TABLE IF EXISTS {Table}");
        await ExecAsync(a, $"CREATE TABLE {Table} (id int NOT NULL PRIMARY KEY, name nvarchar(100))");
        return a;
    }

    [Fact(Skip = DockerGate.SkipReason, SkipUnless = nameof(DockerGate.Enabled), SkipType = typeof(DockerGate))]
    public async Task SharedScenario_PassesOnSqlServer()
    {
        var expectations = new EngineExpectations(
            DdlTransactional: true,
            ErrorAbortsTransaction: false,
            OtherConnectionCanReadBeforeCommit: false,
            ProbeKnowsTransactions: true,
            ProbeKnowsFailure: true,
            SupportsActualPlan: true);
        await TransactionScenario.RunAsync(new SqlServerDatabaseProvider(), OpenAsync, expectations);
    }

    [Fact(Skip = DockerGate.SkipReason, SkipUnless = nameof(DockerGate.Enabled), SkipType = typeof(DockerGate))]
    public async Task NestedBegin_OneCommitLeavesOneLevel()
    {
        await using var a = await PrepareAsync();
        await ExecAsync(a, TransactionSql.Begin(Dialect)!);
        await ExecAsync(a, TransactionSql.Begin(Dialect)!);
        Assert.Equal(2, await TranCountAsync(a));
        await ExecAsync(a, TransactionSql.Commit(Dialect));
        Assert.Equal(1, await TranCountAsync(a));
        await ExecAsync(a, TransactionSql.Rollback(Dialect));
    }

    [Fact(Skip = DockerGate.SkipReason, SkipUnless = nameof(DockerGate.Enabled), SkipType = typeof(DockerGate))]
    public async Task UnnamedRollback_ResetsEveryNestedLevel()
    {
        await using var a = await PrepareAsync();
        await ExecAsync(a, TransactionSql.Begin(Dialect)!);
        await ExecAsync(a, TransactionSql.Begin(Dialect)!);
        await ExecAsync(a, $"INSERT INTO {Table} (id, name) VALUES (1, 'a')");
        await ExecAsync(a, TransactionSql.Rollback(Dialect));
        Assert.Equal(0, await TranCountAsync(a));
        Assert.Equal(0, await RowsAsync(a));
    }

    [Fact(Skip = DockerGate.SkipReason, SkipUnless = nameof(DockerGate.Enabled), SkipType = typeof(DockerGate))]
    public async Task NamedSavepointRollback_KeepsTheTransactionAndEarlierRows()
    {
        await using var a = await PrepareAsync();
        await ExecAsync(a, TransactionSql.Begin(Dialect)!);
        await ExecAsync(a, $"INSERT INTO {Table} (id, name) VALUES (1, 'a')");
        await ExecAsync(a, TransactionSql.Savepoint(Dialect, "a"));
        await ExecAsync(a, $"INSERT INTO {Table} (id, name) VALUES (2, 'b')");
        await ExecAsync(a, TransactionSql.RollbackToSavepoint(Dialect, "a"));
        Assert.Equal(1, await TranCountAsync(a));
        Assert.Equal(1, await RowsAsync(a));
        await ExecAsync(a, TransactionSql.Commit(Dialect));
        Assert.Equal(1, await RowsAsync(a));
    }

    // A doomed transaction (XACT_STATE = -1) cannot outlive its batch: SQL Server rolls it back at the end of the batch (error 3998), so the probe never sees -1 between two statements.
    [Fact(Skip = DockerGate.SkipReason, SkipUnless = nameof(DockerGate.Enabled), SkipType = typeof(DockerGate))]
    public async Task XactAbortOn_ErrorInsideTryCatch_DoomsTheTransactionUntilTheBatchEnds()
    {
        await using var a = await PrepareAsync();
        await ExecAsync(a, "SET XACT_ABORT ON");
        await ExecAsync(a, TransactionSql.Begin(Dialect)!);
        await ExecAsync(a, $"INSERT INTO {Table} (id, name) VALUES (1, 'a')");
        var ex = await Assert.ThrowsAsync<Microsoft.Data.SqlClient.SqlException>(() =>
            ExecAsync(a, $"BEGIN TRY INSERT INTO {Table} (id, name) VALUES (1, 'dup') END TRY BEGIN CATCH END CATCH"));
        Assert.Equal(3998, ex.Number);
        var probe = await ProbeAsync(a);
        Assert.False(probe.InTransaction);
        Assert.False(probe.Failed);
        Assert.Equal(0, await RowsAsync(a));
    }

    [Fact(Skip = DockerGate.SkipReason, SkipUnless = nameof(DockerGate.Enabled), SkipType = typeof(DockerGate))]
    public async Task XactAbortOn_PlainError_EndsTheTransactionAndTheProbeSeesIt()
    {
        await using var a = await PrepareAsync();
        await ExecAsync(a, "SET XACT_ABORT ON");
        await ExecAsync(a, TransactionSql.Begin(Dialect)!);
        await ExecAsync(a, $"INSERT INTO {Table} (id, name) VALUES (1, 'a')");
        await Assert.ThrowsAnyAsync<Exception>(() => ExecAsync(a, $"INSERT INTO {Table} (id, name) VALUES (1, 'dup')"));
        var probe = await ProbeAsync(a);
        Assert.False(probe.InTransaction);
        Assert.Equal(0, await RowsAsync(a));
    }

    [Fact(Skip = DockerGate.SkipReason, SkipUnless = nameof(DockerGate.Enabled), SkipType = typeof(DockerGate))]
    public async Task CreateDatabase_InsideTransaction_Errors()
    {
        await using var a = await PrepareAsync();
        await ExecAsync(a, TransactionSql.Begin(Dialect)!);
        await ExecAsync(a, $"INSERT INTO {Table} (id, name) VALUES (1, 'a')");
        await Assert.ThrowsAnyAsync<Exception>(() => ExecAsync(a, "CREATE DATABASE squill_tx_denied"));
        await ExecAsync(a, TransactionSql.Rollback(Dialect));
    }

    [Fact(Skip = DockerGate.SkipReason, SkipUnless = nameof(DockerGate.Enabled), SkipType = typeof(DockerGate))]
    public async Task ActualPlan_InsideUserTransaction_LeavesItOpenWithItsRows()
    {
        await using var a = await PrepareAsync();
        await ExecAsync(a, TransactionSql.Begin(Dialect)!);
        await ExecAsync(a, $"INSERT INTO {Table} (id, name) VALUES (1, 'a')");
        var plan = await Assert.IsAssignableFrom<IPlanReader>(a).ExplainAsync($"SELECT * FROM {Table}", [], PlanMode.Actual, CancellationToken.None);
        Assert.NotNull(plan);
        Assert.Equal(1, await TranCountAsync(a));
        Assert.Equal(1, await RowsAsync(a));
        await ExecAsync(a, TransactionSql.Rollback(Dialect));
        Assert.Equal(0, await RowsAsync(a));
    }
}
