using MySqlConnector;
using Squill.Core;
using Squill.Core.Activity;
using Xunit;

namespace Squill.Data.MySql.Tests;

public sealed class ActivityReaderTests(MySqlFixture server) : IClassFixture<MySqlFixture>
{
    [Theory]
    [InlineData("Sleep", false, false, SessionState.Idle)]
    [InlineData("Sleep", true, false, SessionState.IdleInTransaction)]
    [InlineData("Query", false, false, SessionState.Active)]
    [InlineData("Query", true, false, SessionState.Active)]
    [InlineData("Query", true, true, SessionState.Waiting)]
    [InlineData("Execute", false, false, SessionState.Active)]
    [InlineData("Daemon", false, false, SessionState.Other)]
    [InlineData("Binlog Dump", false, false, SessionState.Other)]
    [InlineData(null, false, false, SessionState.Other)]
    public void MapState_CommandAndTransaction_NeutralState(string? command, bool inTransaction, bool lockWait, SessionState expected) =>
        Assert.Equal(expected, MySqlActivityReader.MapState(command, inTransaction, lockWait));

    [Theory]
    [InlineData("Query", "executing", "Query: executing")]
    [InlineData("Sleep", null, "Sleep")]
    [InlineData("Sleep", "", "Sleep")]
    [InlineData(null, null, "")]
    public void StateText_CommandAndState_Joined(string? command, string? state, string expected) =>
        Assert.Equal(expected, MySqlActivityReader.StateText(command, state));

    [Fact]
    public void ParseId_Number_Parsed() => Assert.Equal(4294967295L, MySqlActivityReader.ParseId("4294967295"));

    [Theory]
    [InlineData("")]
    [InlineData("abc")]
    [InlineData("0")]
    [InlineData("-5")]
    [InlineData("12; drop table t")]
    [InlineData("1 or 1=1")]
    public void ParseId_NotAPositiveNumber_Throws(string text) => Assert.Throws<ArgumentException>(() => MySqlActivityReader.ParseId(text));

    [Fact]
    public async Task KillAsync_NonNumericId_ThrowsBeforeAnyRequest()
    {
        await Assert.ThrowsAsync<ArgumentException>(async () => await MySqlActivityReader.CancelAsync("Server=127.0.0.1;Port=1;ConnectionTimeout=1", "x", CancellationToken.None));
        await Assert.ThrowsAsync<ArgumentException>(async () => await MySqlActivityReader.TerminateAsync("Server=127.0.0.1;Port=1;ConnectionTimeout=1", "x", CancellationToken.None));
    }

    private ValueTask<IDatabaseConnection> OpenAsync(string user = MySqlFixture.UserName, string password = MySqlFixture.Password) =>
        new MySqlDatabaseProvider().OpenAsync(
            new ConnectionProfile(Guid.NewGuid(), "activity", DatabaseDialect.MySql, server.Host, server.Port, MySqlFixture.Database, user, null, null),
            password,
            CancellationToken.None);

    private static async Task<string> IdAsync(IDatabaseConnection connection)
    {
        await using var reader = await connection.ExecuteAsync("select connection_id()", [], CancellationToken.None);
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
    public async Task ReadAsync_BlockedUpdate_ShowsStatesEdgeAndLimits()
    {
        await using var monitor = await OpenAsync();
        await using var holder = await OpenAsync();
        await using var waiter = await OpenAsync();
        var reader = (IActivityReader)monitor;
        Assert.True(reader.SupportsCancelQuery);
        await RunAsync(monitor, "drop table if exists activity_t");
        await RunAsync(monitor, "create table activity_t(id int primary key, v int)");
        await RunAsync(monitor, "insert into activity_t values (1, 0)");
        await RunAsync(holder, "start transaction");
        await RunAsync(holder, "update activity_t set v = 1 where id = 1");
        var holderId = await IdAsync(holder);
        var waiterId = await IdAsync(waiter);
        var blocked = RunAsync(waiter, "update activity_t set v = 2 where id = 1");
        try
        {
            var snapshot = await WaitForAsync(reader, s => s.Blocking.Any(e => e.BlockedId == waiterId));

            Assert.False(snapshot.BlockingUnavailable);
            var held = snapshot.Sessions.Single(s => s.Id == holderId);
            Assert.Equal(SessionState.IdleInTransaction, held.State);
            Assert.NotNull(held.TransactionStart);
            Assert.True(held.LocksHeld > 0);
            Assert.False(held.IsSystem);
            var waiting = snapshot.Sessions.Single(s => s.Id == waiterId);
            Assert.Equal(SessionState.Waiting, waiting.State);
            Assert.Contains("update activity_t", waiting.Query);
            var edge = Assert.Single(snapshot.Blocking, e => e.BlockedId == waiterId);
            Assert.Equal(holderId, edge.BlockerId);
            Assert.Contains("activity_t", edge.Relation);
            Assert.True(snapshot.MaxConnections > 0);
            Assert.True(snapshot.TransactionCounter >= 0);
            Assert.True(snapshot.ActiveConnections >= 3);
            Assert.DoesNotContain(snapshot.Sessions, s => s.Query?.Contains("data_lock_waits", StringComparison.Ordinal) == true);
        }
        finally
        {
            await RunAsync(holder, "rollback");
            await blocked;
        }
    }

    [Fact(Skip = DockerGate.SkipReason, SkipUnless = nameof(DockerGate.Enabled), SkipType = typeof(DockerGate))]
    public async Task CancelQueryAsync_BlockedStatement_FailsWithQueryInterrupted()
    {
        await using var monitor = await OpenAsync();
        await using var holder = await OpenAsync();
        await using var waiter = await OpenAsync();
        var reader = (IActivityReader)monitor;
        await RunAsync(monitor, "drop table if exists activity_c");
        await RunAsync(monitor, "create table activity_c(id int primary key)");
        await RunAsync(monitor, "insert into activity_c values (1)");
        await RunAsync(holder, "start transaction");
        await RunAsync(holder, "update activity_c set id = 1 where id = 1");
        var waiterId = await IdAsync(waiter);
        var blocked = RunAsync(waiter, "update activity_c set id = 1 where id = 1");
        try
        {
            await WaitForAsync(reader, s => s.Blocking.Any(e => e.BlockedId == waiterId));
            await reader.CancelQueryAsync(waiterId, CancellationToken.None);
            var error = await Assert.ThrowsAsync<MySqlException>(() => blocked);
            Assert.Equal(MySqlErrorCode.QueryInterrupted, error.ErrorCode);
        }
        finally
        {
            await RunAsync(holder, "rollback");
        }
    }

    [Fact(Skip = DockerGate.SkipReason, SkipUnless = nameof(DockerGate.Enabled), SkipType = typeof(DockerGate))]
    public async Task TerminateSessionAsync_IdleSession_DisappearsAndUnknownIdIsRefused()
    {
        await using var monitor = await OpenAsync();
        await using var victim = await OpenAsync();
        var reader = (IActivityReader)monitor;
        var victimId = await IdAsync(victim);
        Assert.Contains((await reader.ReadAsync(CancellationToken.None)).Sessions, s => s.Id == victimId);

        await reader.TerminateSessionAsync(victimId, CancellationToken.None);

        var snapshot = await WaitForAsync(reader, s => s.Sessions.All(x => x.Id != victimId));
        Assert.DoesNotContain(snapshot.Sessions, s => s.Id == victimId);
        await Assert.ThrowsAsync<ActivityActionFailedException>(async () => await reader.TerminateSessionAsync("2147483000", CancellationToken.None));
    }

    [Fact(Skip = DockerGate.SkipReason, SkipUnless = nameof(DockerGate.Enabled), SkipType = typeof(DockerGate))]
    public async Task ReadAsync_LoginWithoutLockTableRights_ReportsBlockingUnavailable()
    {
        await using (var admin = await OpenAsync())
        {
            await RunAsync(admin, "drop user if exists 'activity_lim'@'%'");
            await RunAsync(admin, "create user 'activity_lim'@'%' identified by 'lim-pass-1'");
            await RunAsync(admin, "grant select on squill.* to 'activity_lim'@'%'");
        }

        await using var limited = await OpenAsync("activity_lim", "lim-pass-1");
        var snapshot = await ((IActivityReader)limited).ReadAsync(CancellationToken.None);

        Assert.True(snapshot.BlockingUnavailable);
        Assert.Empty(snapshot.Blocking);
    }
}
