using Squill.Core;
using Squill.Data.RoundTrip;
using Xunit;

namespace Squill.Data.MySql.Tests;

public sealed class CancelAndTimeoutTests(MySqlFixture server) : IClassFixture<MySqlFixture>
{
    // sleep() returns 1 instead of failing when the server interrupts it, so a CPU-bound statement is used.
    private const string Slow = "select count(*) from information_schema.columns a, information_schema.columns b, information_schema.columns c, information_schema.columns d";
    private const string Huge = "with recursive c(n) as (select 1 union all select n + 1 from c where n < 50000000) select n from c";

    private ValueTask<IDatabaseConnection> OpenAsync(int commandTimeoutSeconds = 0) =>
        new MySqlDatabaseProvider().OpenAsync(
            new ConnectionProfile(Guid.NewGuid(), "cancel", DatabaseDialect.MySql, server.Host, server.Port, MySqlFixture.Database, MySqlFixture.UserName, null, null, CommandTimeoutSeconds: commandTimeoutSeconds),
            MySqlFixture.Password,
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
        await using (var setup = await connection.ExecuteAsync("set session cte_max_recursion_depth = 100000000", [], CancellationToken.None))
            await setup.ReadBatchAsync(1, CancellationToken.None);
        await CancelScenario.CancelWhileReadingAsync(connection, Huge);
    }
}
