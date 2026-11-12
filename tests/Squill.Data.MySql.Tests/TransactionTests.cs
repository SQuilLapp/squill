using MySqlConnector;
using Squill.Core;
using Squill.Core.Transactions;
using Squill.Data.RoundTrip;
using Squill.Sql.Transactions;
using Xunit;

namespace Squill.Data.MySql.Tests;

public sealed class TransactionTests(MySqlFixture server) : IClassFixture<MySqlFixture>
{
    private const string Table = "tx_my_extra";
    private static readonly DatabaseDialect Dialect = DatabaseDialect.MySql;

    private async ValueTask<IDatabaseConnection> OpenAsync() => await new MySqlDatabaseProvider().OpenAsync(
        new ConnectionProfile(Guid.NewGuid(), "tx", Dialect, server.Host, server.Port, MySqlFixture.Database, MySqlFixture.UserName, null, null),
        MySqlFixture.Password,
        CancellationToken.None);

    private static Task ExecAsync(IDatabaseConnection c, string sql) => ExecWithAsync(c, sql, TestContext.Current.CancellationToken);

    private static async Task ExecWithAsync(IDatabaseConnection c, string sql, CancellationToken ct)
    {
        await using var reader = await c.ExecuteAsync(sql, [], ct);
        await reader.ReadAllAsync(100, ct);
    }

    private static async Task<long> CountAsync(IDatabaseConnection c, string where = "1 = 1")
    {
        await using var reader = await c.ExecuteAsync($"SELECT COUNT(*) FROM {Table} WHERE {where}", [], CancellationToken.None);
        return Convert.ToInt64((await reader.ReadAllAsync(1, CancellationToken.None)).Single()[0], System.Globalization.CultureInfo.InvariantCulture);
    }

    private static async Task ResetAsync(IDatabaseConnection c)
    {
        await ExecAsync(c, "UNLOCK TABLES");
        await ExecAsync(c, "DROP TABLE IF EXISTS tx_my_other");
        await ExecAsync(c, "DROP TABLE IF EXISTS tx_my_new");
        await ExecAsync(c, $"DROP TABLE IF EXISTS {Table}");
        await ExecAsync(c, $"CREATE TABLE {Table} (id int NOT NULL PRIMARY KEY, name varchar(100)) ENGINE=InnoDB");
        await ExecAsync(c, "CREATE TABLE tx_my_other (id int NOT NULL PRIMARY KEY) ENGINE=InnoDB");
    }

    [Fact(Skip = DockerGate.SkipReason, SkipUnless = nameof(DockerGate.Enabled), SkipType = typeof(DockerGate))]
    public async Task SharedScenario_PassesOnMySql()
    {
        var expectations = new EngineExpectations(
            DdlTransactional: false,
            ErrorAbortsTransaction: false,
            OtherConnectionCanReadBeforeCommit: true,
            ProbeKnowsTransactions: false,
            ProbeKnowsFailure: false,
            SupportsActualPlan: true);
        await TransactionScenario.RunAsync(new MySqlDatabaseProvider(), OpenAsync, expectations);
    }

    // One row per head of StatementClassifierTests.ImplicitCommit_MySql, so the classifier and the server are checked against the same list.
    [Theory(Skip = DockerGate.SkipReason, SkipUnless = nameof(DockerGate.Enabled), SkipType = typeof(DockerGate))]
    [InlineData("CREATE TABLE tx_my_new (id int)")]
    [InlineData("ALTER TABLE tx_my_other ADD COLUMN extra int")]
    [InlineData("TRUNCATE TABLE tx_my_other")]
    [InlineData("DROP TABLE tx_my_other")]
    [InlineData("RENAME TABLE tx_my_other TO tx_my_new")]
    [InlineData("LOCK TABLES tx_my_other WRITE")]
    [InlineData("ANALYZE TABLE tx_my_other")]
    [InlineData("OPTIMIZE TABLE tx_my_other")]
    [InlineData("FLUSH TABLES")]
    [InlineData("START TRANSACTION")]
    [InlineData("CREATE TEMPORARY TABLE tx_my_tmp (id int)")]
    [InlineData("SELECT 1")]
    public async Task StatementCommitsTheOpenTransaction_ExactlyWhenClassifiedImplicitCommit(string sql)
    {
        await using var a = await OpenAsync();
        await using var b = await OpenAsync();
        await ResetAsync(a);
        await ExecAsync(a, "START TRANSACTION");
        await ExecAsync(a, $"INSERT INTO {Table} (id, name) VALUES (1, 'a')");
        await ExecAsync(a, sql);
        await ExecAsync(a, "ROLLBACK");
        await ExecAsync(a, "UNLOCK TABLES");
        var committed = await CountAsync(b) == 1;
        var expected = sql.StartsWith("START", StringComparison.Ordinal) || StatementClassifier.Classify(sql, Dialect).ImplicitCommit;
        Assert.Equal(expected, committed);
        await ExecAsync(a, "DROP TEMPORARY TABLE IF EXISTS tx_my_tmp");
    }

    // SET autocommit = 1 commits only when autocommit was 0 (not after START TRANSACTION in autocommit mode): the classifier is conservative and asks in both cases.
    [Fact(Skip = DockerGate.SkipReason, SkipUnless = nameof(DockerGate.Enabled), SkipType = typeof(DockerGate))]
    public async Task SetAutocommitOn_CommitsWhenAutocommitWasOff()
    {
        await using var a = await OpenAsync();
        await using var b = await OpenAsync();
        await ResetAsync(a);
        Assert.True(StatementClassifier.Classify("SET autocommit = 1", Dialect).ImplicitCommit);
        await ExecAsync(a, "SET autocommit = 0");
        await ExecAsync(a, $"INSERT INTO {Table} (id, name) VALUES (1, 'a')");
        await ExecAsync(a, "SET autocommit = 1");
        await ExecAsync(a, "ROLLBACK");
        Assert.Equal(1, await CountAsync(b));
    }

    [Fact(Skip = DockerGate.SkipReason, SkipUnless = nameof(DockerGate.Enabled), SkipType = typeof(DockerGate))]
    public async Task BeginInsideOpenTransaction_CommitsIt()
    {
        await using var a = await OpenAsync();
        await using var b = await OpenAsync();
        await ResetAsync(a);
        await ExecAsync(a, "START TRANSACTION");
        await ExecAsync(a, $"INSERT INTO {Table} (id, name) VALUES (1, 'a')");
        await ExecAsync(a, "BEGIN");
        await ExecAsync(a, "ROLLBACK");
        Assert.Equal(1, await CountAsync(b));
    }

    [Fact(Skip = DockerGate.SkipReason, SkipUnless = nameof(DockerGate.Enabled), SkipType = typeof(DockerGate))]
    public async Task Savepoint_IsGoneAfterImplicitCommit()
    {
        await using var a = await OpenAsync();
        await ResetAsync(a);
        await ExecAsync(a, "START TRANSACTION");
        await ExecAsync(a, TransactionSql.Savepoint(Dialect, "sp"));
        await ExecAsync(a, "CREATE TABLE tx_my_new (id int)");
        var ex = await Assert.ThrowsAsync<MySqlException>(() => ExecAsync(a, TransactionSql.RollbackToSavepoint(Dialect, "sp")));
        Assert.Equal((MySqlErrorCode)1305, ex.ErrorCode);
    }

    [Fact(Skip = DockerGate.SkipReason, SkipUnless = nameof(DockerGate.Enabled), SkipType = typeof(DockerGate))]
    public async Task Deadlock_RollsBackTheWholeVictimTransaction()
    {
        await using var a = await OpenAsync();
        await using var b = await OpenAsync();
        await ResetAsync(a);
        await ExecAsync(a, $"INSERT INTO {Table} (id, name) VALUES (1, 'x'), (2, 'x')");
        using var ceiling = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await ExecAsync(a, "START TRANSACTION");
        await ExecAsync(b, "START TRANSACTION");
        await ExecAsync(a, $"INSERT INTO {Table} (id, name) VALUES (100, 'a')");
        await ExecAsync(b, $"INSERT INTO {Table} (id, name) VALUES (200, 'b')");
        await ExecAsync(a, $"UPDATE {Table} SET name = 'a' WHERE id = 1");
        await ExecAsync(b, $"UPDATE {Table} SET name = 'b' WHERE id = 2");
        var first = ExecWithAsync(a, $"UPDATE {Table} SET name = 'a' WHERE id = 2", ceiling.Token);
        await Task.Delay(500, ceiling.Token);
        var second = ExecWithAsync(b, $"UPDATE {Table} SET name = 'b' WHERE id = 1", ceiling.Token);
        var results = await Task.WhenAll(Capture(first), Capture(second));
        var victim = Assert.IsType<MySqlException>(Assert.Single(results, r => r is not null));
        Assert.Equal(MySqlErrorCode.LockDeadlock, victim.ErrorCode);
        var (victimConnection, survivor, victimRow, survivorRow) = results[0] is not null ? (a, b, 100, 200) : (b, a, 200, 100);
        // The victim lost its whole transaction, earlier statements included: its own insert is gone without any ROLLBACK from the client.
        Assert.Equal(0, await CountAsync(victimConnection, $"id = {victimRow}"));
        await ExecAsync(survivor, "COMMIT");
        Assert.Equal(1, await CountAsync(survivor, $"id = {survivorRow}"));

        static async Task<Exception?> Capture(Task t)
        {
            try
            {
                await t;
                return null;
            }
            catch (MySqlException ex)
            {
                return ex;
            }
        }
    }

    [Fact(Skip = DockerGate.SkipReason, SkipUnless = nameof(DockerGate.Enabled), SkipType = typeof(DockerGate))]
    public async Task Probe_IsUnknown()
    {
        await using var a = await OpenAsync();
        await ExecAsync(a, "START TRANSACTION");
        Assert.Equal(TransactionProbeResult.Unknown, await ((ITransactionProbe)a).ProbeAsync(CancellationToken.None));
        await ExecAsync(a, "ROLLBACK");
    }
}
