using Squill.Core;
using Squill.Data.RoundTrip;
using Xunit;

namespace Squill.Data.Postgres.Tests;

public sealed class CancelAndTimeoutTests(PostgresFixture server) : IClassFixture<PostgresFixture>
{
    private const string Slow = "select pg_sleep(30)";
    private const string Huge = "select generate_series(1, 50000000)";

    private ValueTask<IDatabaseConnection> OpenAsync(int commandTimeoutSeconds = 0) =>
        new PostgresDatabaseProvider().OpenAsync(
            new ConnectionProfile(Guid.NewGuid(), "cancel", DatabaseDialect.PostgreSql, server.Host, server.Port, PostgresFixture.Database, PostgresFixture.UserName, null, null, CommandTimeoutSeconds: commandTimeoutSeconds),
            PostgresFixture.Password,
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
