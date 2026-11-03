using Squill.Core;
using Squill.Data.RoundTrip;
using Xunit;

namespace Squill.Data.SqlServer.Tests;

public sealed class CancelAndTimeoutTests(SqlServerFixture server) : IClassFixture<SqlServerFixture>
{
    private const string Slow = "waitfor delay '00:00:30'";
    private const string Huge = "select a.object_id from sys.objects a cross join sys.objects b cross join sys.objects c";

    private ValueTask<IDatabaseConnection> OpenAsync(int commandTimeoutSeconds = 0) =>
        new SqlServerDatabaseProvider().OpenAsync(
            new ConnectionProfile(Guid.NewGuid(), "cancel", DatabaseDialect.SqlServer, server.Host, server.Port, SqlServerFixture.Database, SqlServerFixture.UserName, null, null, CommandTimeoutSeconds: commandTimeoutSeconds),
            SqlServerFixture.Password,
            CancellationToken.None);

    [Fact(Skip = DockerGate.SkipReason, SkipUnless = nameof(DockerGate.Enabled), SkipType = typeof(DockerGate))]
    public async Task CancelSlowStatement_ThrowsAndConnectionStaysUsable()
    {
        await using var connection = await OpenAsync();
        await CancelScenario.CancelSlowStatementAsync(connection, Slow);
    }

    [Fact(Skip = DockerGate.SkipReason, SkipUnless = nameof(DockerGate.Enabled), SkipType = typeof(DockerGate))]
    public async Task CommandTimeout_SlowStatement_FailsAndConnectionStaysUsable()
    {
        await using var connection = await OpenAsync(1);
        await CancelScenario.TimeoutSlowStatementAsync(connection, Slow);
    }

    [Fact(Skip = DockerGate.SkipReason, SkipUnless = nameof(DockerGate.Enabled), SkipType = typeof(DockerGate))]
    public async Task CancelWhileReading_DisposesQuickly()
    {
        await using var connection = await OpenAsync();
        await CancelScenario.CancelWhileReadingAsync(connection, Huge);
    }
}
