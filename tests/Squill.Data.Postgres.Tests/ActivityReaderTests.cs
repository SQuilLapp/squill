using Npgsql;
using Squill.Core;
using Squill.Core.Activity;
using Xunit;

namespace Squill.Data.Postgres.Tests;

public sealed class ActivityReaderTests(PostgresFixture server) : IClassFixture<PostgresFixture>
{
    [Theory]
    [InlineData("active", null, SessionState.Active)]
    [InlineData("active", "Lock", SessionState.Waiting)]
    [InlineData("active", "IO", SessionState.Active)]
    [InlineData("idle", null, SessionState.Idle)]
    [InlineData("idle in transaction", null, SessionState.IdleInTransaction)]
    [InlineData("idle in transaction (aborted)", null, SessionState.IdleInTransaction)]
    [InlineData("fastpath function call", null, SessionState.Other)]
    [InlineData(null, null, SessionState.Other)]
    public void MapState_EngineState_NeutralState(string? state, string? waitType, SessionState expected) =>
        Assert.Equal(expected, PostgresActivityReader.MapState(state, waitType));

    [Theory]
    [InlineData("1234", 1234)]
    [InlineData("1", 1)]
    public void ParsePid_Number_Parsed(string text, int expected) => Assert.Equal(expected, PostgresActivityReader.ParsePid(text));

    [Theory]
    [InlineData("")]
    [InlineData("abc")]
    [InlineData("0")]
    [InlineData("-5")]
    [InlineData("12; drop table t")]
    [InlineData("1.5")]
    public void ParsePid_NotAPositiveNumber_Throws(string text) => Assert.Throws<ArgumentException>(() => PostgresActivityReader.ParsePid(text));

    [Fact]
    public async Task CancelAsync_NonNumericId_ThrowsBeforeAnyRequest()
    {
        await using var dataSource = NpgsqlDataSource.Create("Host=127.0.0.1;Port=1;Timeout=1");
        await Assert.ThrowsAsync<ArgumentException>(async () => await PostgresActivityReader.CancelAsync(dataSource, "x", CancellationToken.None));
        await Assert.ThrowsAsync<ArgumentException>(async () => await PostgresActivityReader.TerminateAsync(dataSource, "x", CancellationToken.None));
    }

    private ValueTask<IDatabaseConnection> OpenAsync() =>
        new PostgresDatabaseProvider().OpenAsync(
            new ConnectionProfile(Guid.NewGuid(), "activity", DatabaseDialect.PostgreSql, server.Host, server.Port, PostgresFixture.Database, PostgresFixture.UserName, null, null),
            PostgresFixture.Password,
            CancellationToken.None);

    private static async Task<string> PidAsync(IDatabaseConnection connection)
    {
        await using var reader = await connection.ExecuteAsync("select pg_backend_pid()", [], CancellationToken.None);
        return Convert.ToString((await reader.ReadAllAsync(1, CancellationToken.None))[0][0], System.Globalization.CultureInfo.InvariantCulture)!;
    }

    private static async Task RunAsync(IDatabaseConnection connection, string sql)
    {
        await using var reader = await connection.ExecuteAsync(sql, [], CancellationToken.None);
        await reader.ReadAllAsync(1, CancellationToken.None);
    }

    private static async Task<ActivitySnapshot> WaitForAsync(IActivityReader reader, Func<ActivitySnapshot, bool> done)
    {
        for (var i = 0; i < 50; i++)
        {
            var snapshot = await reader.ReadAsync(CancellationToken.None);
            if (done(snapshot))
                return snapshot;
            await Task.Delay(100);
        }
        throw new TimeoutException("The expected activity never appeared.");
    }

    [Fact(Skip = DockerGate.SkipReason, SkipUnless = nameof(DockerGate.Enabled), SkipType = typeof(DockerGate))]
    public async Task ReadAsync_BlockedUpdate_ShowsStatesEdgeLockAndLimits()
    {
        await using var monitor = await OpenAsync();
        await using var holder = await OpenAsync();
        await using var waiter = await OpenAsync();
        var reader = (IActivityReader)monitor;
        await RunAsync(monitor, "drop table if exists activity_t; create table activity_t(id int primary key, v int); insert into activity_t values (1, 0)");
        await RunAsync(holder, "begin; update activity_t set v = 1 where id = 1");
        var holderPid = await PidAsync(holder);
        var waiterPid = await PidAsync(waiter);
        var blocked = RunAsync(waiter, "update activity_t set v = 2 where id = 1");
        try
        {
            var snapshot = await WaitForAsync(reader, s => s.Blocking.Any(e => e.BlockedId == waiterPid));

            var held = snapshot.Sessions.Single(s => s.Id == holderPid);
            Assert.Equal(SessionState.IdleInTransaction, held.State);
            Assert.Equal("idle in transaction", held.StateText);
            Assert.NotNull(held.TransactionStart);
            Assert.True(held.LocksHeld > 0);
            Assert.False(held.IsSystem);
            var waiting = snapshot.Sessions.Single(s => s.Id == waiterPid);
            Assert.Equal(SessionState.Waiting, waiting.State);
            Assert.Contains("update activity_t", waiting.Query);
            var edge = Assert.Single(snapshot.Blocking, e => e.BlockedId == waiterPid);
            Assert.Equal(holderPid, edge.BlockerId);
            Assert.False(string.IsNullOrEmpty(edge.LockMode));
            Assert.Contains("activity_t", edge.Relation);
            Assert.True(snapshot.MaxConnections > 0);
            Assert.True(snapshot.TransactionCounter > 0);
            Assert.True(snapshot.ActiveConnections >= 2);
        }
        finally
        {
            await RunAsync(holder, "rollback");
            await blocked;
        }
    }

    [Fact(Skip = DockerGate.SkipReason, SkipUnless = nameof(DockerGate.Enabled), SkipType = typeof(DockerGate))]
    public async Task CancelQueryAsync_BlockedStatement_FailsWithCancelError()
    {
        await using var monitor = await OpenAsync();
        await using var holder = await OpenAsync();
        await using var waiter = await OpenAsync();
        var reader = (IActivityReader)monitor;
        await RunAsync(monitor, "drop table if exists activity_c; create table activity_c(id int primary key); insert into activity_c values (1)");
        await RunAsync(holder, "begin; update activity_c set id = 1 where id = 1");
        var waiterPid = await PidAsync(waiter);
        var blocked = RunAsync(waiter, "update activity_c set id = 1 where id = 1");
        try
        {
            await WaitForAsync(reader, s => s.Blocking.Any(e => e.BlockedId == waiterPid));
            await reader.CancelQueryAsync(waiterPid, CancellationToken.None);
            var error = await Assert.ThrowsAsync<PostgresException>(() => blocked);
            Assert.Equal(PostgresErrorCodes.QueryCanceled, error.SqlState);
        }
        finally
        {
            await RunAsync(holder, "rollback");
        }
    }

    [Fact(Skip = DockerGate.SkipReason, SkipUnless = nameof(DockerGate.Enabled), SkipType = typeof(DockerGate))]
    public async Task TerminateSessionAsync_IdleSession_Disappears()
    {
        await using var monitor = await OpenAsync();
        await using var victim = await OpenAsync();
        var reader = (IActivityReader)monitor;
        var victimPid = await PidAsync(victim);
        Assert.Contains((await reader.ReadAsync(CancellationToken.None)).Sessions, s => s.Id == victimPid);

        await reader.TerminateSessionAsync(victimPid, CancellationToken.None);

        var snapshot = await WaitForAsync(reader, s => s.Sessions.All(x => x.Id != victimPid));
        Assert.DoesNotContain(snapshot.Sessions, s => s.Id == victimPid);
        await Assert.ThrowsAsync<ActivityActionFailedException>(async () => await reader.TerminateSessionAsync("2147483000", CancellationToken.None));
    }

    [Fact(Skip = DockerGate.SkipReason, SkipUnless = nameof(DockerGate.Enabled), SkipType = typeof(DockerGate))]
    public async Task ReadAsync_OwnBackend_IsAbsentAndNonNumericIdRejected()
    {
        await using var connection = await OpenAsync();
        var reader = (IActivityReader)connection;
        var snapshot = await reader.ReadAsync(CancellationToken.None);
        var ownPid = await PidAsync(connection);
        // The reader works on a pooled session of its own that must not list itself: no session runs the monitoring query, the user's session is listed.
        Assert.DoesNotContain(snapshot.Sessions, s => s.Query?.Contains("pg_blocking_pids", StringComparison.Ordinal) == true);
        Assert.Contains(snapshot.Sessions, s => s.Id == ownPid);
        await Assert.ThrowsAsync<ArgumentException>(async () => await reader.CancelQueryAsync("abc", CancellationToken.None));
    }
}
