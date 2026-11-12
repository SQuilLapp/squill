using Squill.Core;
using Squill.Core.Plans;
using Squill.Data.RoundTrip;
using Squill.Sql.Transactions;
using Xunit;

namespace Squill.Data.Postgres.Tests;

public sealed class TransactionTests(PostgresFixture server) : IClassFixture<PostgresFixture>
{
    private const string Table = "tx_pg_extra";
    private static readonly DatabaseDialect Dialect = DatabaseDialect.PostgreSql;

    private async ValueTask<IDatabaseConnection> OpenAsync() => await new PostgresDatabaseProvider().OpenAsync(
        new ConnectionProfile(Guid.NewGuid(), "tx", Dialect, server.Host, server.Port, PostgresFixture.Database, PostgresFixture.UserName, null, null),
        PostgresFixture.Password,
        CancellationToken.None);

    private static async Task ExecAsync(IDatabaseConnection c, string sql)
    {
        await using var reader = await c.ExecuteAsync(sql, [], CancellationToken.None);
        await reader.ReadAllAsync(100, CancellationToken.None);
    }

    private static async Task<List<long>> IdsAsync(IDatabaseConnection c)
    {
        await using var reader = await c.ExecuteAsync($"SELECT id FROM {Table} ORDER BY id", [], CancellationToken.None);
        return [.. (await reader.ReadAllAsync(100, CancellationToken.None)).Select(r => Convert.ToInt64(r[0], System.Globalization.CultureInfo.InvariantCulture))];
    }

    private static Task<Squill.Core.Transactions.TransactionProbeResult> ProbeAsync(IDatabaseConnection c) => ((ITransactionProbe)c).ProbeAsync(CancellationToken.None).AsTask();

    private async Task<(IDatabaseConnection A, IDatabaseConnection B)> PrepareAsync()
    {
        var a = await OpenAsync();
        var b = await OpenAsync();
        await ExecAsync(a, $"DROP TABLE IF EXISTS {Table}");
        await ExecAsync(a, $"CREATE TABLE {Table} (id int NOT NULL PRIMARY KEY, name varchar(100))");
        return (a, b);
    }

    [Fact(Skip = DockerGate.SkipReason, SkipUnless = nameof(DockerGate.Enabled), SkipType = typeof(DockerGate))]
    public async Task SharedScenario_PassesOnPostgres()
    {
        var provider = new PostgresDatabaseProvider();
        var expectations = new EngineExpectations(
            DdlTransactional: true,
            ErrorAbortsTransaction: true,
            OtherConnectionCanReadBeforeCommit: true,
            ProbeKnowsTransactions: false,
            ProbeKnowsFailure: true,
            SupportsActualPlan: true);
        await TransactionScenario.RunAsync(provider, OpenAsync, expectations);
    }

    [Theory(Skip = DockerGate.SkipReason, SkipUnless = nameof(DockerGate.Enabled), SkipType = typeof(DockerGate))]
    [InlineData("CREATE INDEX CONCURRENTLY tx_pg_extra_name ON tx_pg_extra (name)")]
    [InlineData("VACUUM tx_pg_extra")]
    public async Task StatementThatCannotRunInTransaction_FailsAndLeavesItFailed(string sql)
    {
        var (a, b) = await PrepareAsync();
        await using var _a = a;
        await using var _b = b;
        await ExecAsync(a, TransactionSql.Begin(Dialect)!);
        await ExecAsync(a, $"INSERT INTO {Table} (id, name) VALUES (1, 'a')");
        await Assert.ThrowsAnyAsync<Exception>(() => ExecAsync(a, sql));
        Assert.True((await ProbeAsync(a)).Failed);
        await ExecAsync(a, TransactionSql.Rollback(Dialect));
        Assert.False((await ProbeAsync(a)).Failed);
        Assert.Empty(await IdsAsync(b));
    }

    [Fact(Skip = DockerGate.SkipReason, SkipUnless = nameof(DockerGate.Enabled), SkipType = typeof(DockerGate))]
    public async Task RollbackToSavepoint_AfterFailure_KeepsEarlierRows()
    {
        var (a, b) = await PrepareAsync();
        await using var _a = a;
        await using var _b = b;
        await ExecAsync(a, TransactionSql.Begin(Dialect)!);
        await ExecAsync(a, $"INSERT INTO {Table} (id, name) VALUES (1, 'a')");
        await ExecAsync(a, TransactionSql.Savepoint(Dialect, "sp"));
        await Assert.ThrowsAnyAsync<Exception>(() => ExecAsync(a, "VACUUM"));
        Assert.True((await ProbeAsync(a)).Failed);
        await ExecAsync(a, TransactionSql.RollbackToSavepoint(Dialect, "sp"));
        Assert.False((await ProbeAsync(a)).Failed);
        await ExecAsync(a, TransactionSql.Commit(Dialect));
        Assert.Equal([1L], await IdsAsync(b));
    }

    [Fact(Skip = DockerGate.SkipReason, SkipUnless = nameof(DockerGate.Enabled), SkipType = typeof(DockerGate))]
    public async Task ActualPlan_InFailedTransaction_ThrowsAndLeavesNothingPending()
    {
        var (a, b) = await PrepareAsync();
        await using var _a = a;
        await using var _b = b;
        await ExecAsync(a, TransactionSql.Begin(Dialect)!);
        await ExecAsync(a, $"INSERT INTO {Table} (id, name) VALUES (1, 'a')");
        await Assert.ThrowsAnyAsync<Exception>(() => ExecAsync(a, $"INSERT INTO {Table} (id, name) VALUES (1, 'dup')"));
        var reader = Assert.IsAssignableFrom<IPlanReader>(a);
        await Assert.ThrowsAnyAsync<Exception>(async () => await reader.ExplainAsync($"SELECT * FROM {Table}", [], PlanMode.Actual, CancellationToken.None));
        Assert.True((await ProbeAsync(a)).Failed);
        await ExecAsync(a, TransactionSql.Rollback(Dialect));
        // The connection is clean again: no BEGIN READ ONLY left behind.
        Assert.Empty(await IdsAsync(a));
        Assert.False((await ProbeAsync(a)).Failed);
    }

    [Fact(Skip = DockerGate.SkipReason, SkipUnless = nameof(DockerGate.Enabled), SkipType = typeof(DockerGate))]
    public async Task ActualPlan_InsideUserTransaction_DoesNotEndIt()
    {
        var (a, b) = await PrepareAsync();
        await using var _a = a;
        await using var _b = b;
        await ExecAsync(a, TransactionSql.Begin(Dialect)!);
        await ExecAsync(a, $"INSERT INTO {Table} (id, name) VALUES (1, 'a')");
        var plan = await Assert.IsAssignableFrom<IPlanReader>(a).ExplainAsync($"SELECT * FROM {Table}", [], PlanMode.Actual, CancellationToken.None);
        Assert.False(plan.RolledBack);
        await ExecAsync(a, TransactionSql.Rollback(Dialect));
        Assert.Empty(await IdsAsync(b));
    }
}
