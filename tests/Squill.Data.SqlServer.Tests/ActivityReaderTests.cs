using Microsoft.Data.SqlClient;
using Squill.Core;
using Squill.Core.Activity;
using Xunit;

namespace Squill.Data.SqlServer.Tests;

public sealed class ActivityReaderTests(SqlServerFixture server) : IClassFixture<SqlServerFixture>
{
    [Theory]
    [InlineData(null, null, 0, false, SessionState.Idle)]
    [InlineData(null, null, 0, true, SessionState.IdleInTransaction)]
    [InlineData("running", null, 0, false, SessionState.Active)]
    [InlineData("runnable", null, 0, true, SessionState.Active)]
    [InlineData("suspended", "PAGEIOLATCH_SH", 0, false, SessionState.Active)]
    [InlineData("suspended", "LCK_M_X", 0, false, SessionState.Waiting)]
    [InlineData("suspended", "WAITFOR", 52, false, SessionState.Waiting)]
    [InlineData("background", null, 0, false, SessionState.Other)]
    public void MapState_RequestColumns_NeutralState(string? requestStatus, string? waitType, int blockedBy, bool inTransaction, SessionState expected) =>
        Assert.Equal(expected, SqlServerActivityReader.MapState(requestStatus, waitType, blockedBy, inTransaction));

    [Fact]
    public void ParseId_Number_Parsed() => Assert.Equal(57, SqlServerActivityReader.ParseId("57"));

    [Theory]
    [InlineData("")]
    [InlineData("abc")]
    [InlineData("0")]
    [InlineData("-5")]
    [InlineData("57; drop table t")]
    public void ParseId_NotAPositiveNumber_Throws(string text) => Assert.Throws<ArgumentException>(() => SqlServerActivityReader.ParseId(text));

    [Fact]
    public async Task TerminateAsync_NonNumericId_ThrowsBeforeAnyRequest() =>
        await Assert.ThrowsAsync<ArgumentException>(async () => await SqlServerActivityReader.TerminateAsync("Data Source=127.0.0.1,1;Connect Timeout=1", "x", CancellationToken.None));

    private ValueTask<IDatabaseConnection> OpenAsync(string user = SqlServerFixture.UserName, string password = SqlServerFixture.Password) =>
        new SqlServerDatabaseProvider().OpenAsync(
            new ConnectionProfile(Guid.NewGuid(), "activity", DatabaseDialect.SqlServer, server.Host, server.Port, SqlServerFixture.Database, user, null, null),
            password,
            CancellationToken.None);

    private static async Task<string> IdAsync(IDatabaseConnection connection)
    {
        await using var reader = await connection.ExecuteAsync("select @@SPID", [], CancellationToken.None);
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
        Assert.False(reader.SupportsCancelQuery);
        await Assert.ThrowsAsync<NotSupportedException>(async () => await reader.CancelQueryAsync("57", CancellationToken.None));
        await RunAsync(monitor, "drop table if exists activity_t");
        await RunAsync(monitor, "create table activity_t(id int primary key, v int)");
        await RunAsync(monitor, "insert into activity_t values (1, 0)");
        await RunAsync(holder, "begin transaction; update activity_t set v = 1 where id = 1");
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
            Assert.Contains("activity_t", waiting.Query);
            Assert.Contains("LCK_M", waiting.WaitEvent);
            var edge = Assert.Single(snapshot.Blocking, e => e.BlockedId == waiterId);
            Assert.Equal(holderId, edge.BlockerId);
            Assert.False(string.IsNullOrEmpty(edge.LockMode));
            Assert.True(snapshot.MaxConnections > 0);
            Assert.True(snapshot.ActiveConnections >= 3);
            Assert.Contains(snapshot.Sessions, s => s.IsSystem);
        }
        finally
        {
            await RunAsync(holder, "rollback");
            await blocked;
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
        await Assert.ThrowsAsync<ActivityActionFailedException>(async () => await reader.TerminateSessionAsync("32000", CancellationToken.None));
    }

    [Fact(Skip = DockerGate.SkipReason, SkipUnless = nameof(DockerGate.Enabled), SkipType = typeof(DockerGate))]
    public async Task ReadAsync_LoginWithoutViewServerState_ThrowsTypedException()
    {
        await using (var admin = await OpenAsync())
        {
            await RunAsync(admin, "if suser_id('activity_lim') is null create login activity_lim with password = 'Lim-Pass-1234', check_policy = off");
            await RunAsync(admin, "if user_id('activity_lim') is null create user activity_lim for login activity_lim");
        }

        await using var limited = await OpenAsync("activity_lim", "Lim-Pass-1234");
        var error = await Assert.ThrowsAsync<ActivityUnavailableException>(async () => await ((IActivityReader)limited).ReadAsync(CancellationToken.None));

        Assert.Equal("activity.permission", error.MessageCode);
    }
}
