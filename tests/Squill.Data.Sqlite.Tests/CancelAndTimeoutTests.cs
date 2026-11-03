using Squill.Core;
using Squill.Data.RoundTrip;
using Xunit;

namespace Squill.Data.Sqlite.Tests;

public sealed class CancelAndTimeoutTests
{
    private const string Slow = "with recursive c(n) as (select 1 union all select n + 1 from c where n < 2000000000) select count(*) from c";
    private const string Huge = "with recursive c(n) as (select 1 union all select n + 1 from c where n < 2000000000) select n from c";

    private static ValueTask<IDatabaseConnection> OpenAsync(int commandTimeoutSeconds = 0) =>
        new SqliteDatabaseProvider().OpenAsync(
            new ConnectionProfile(Guid.NewGuid(), "mem", DatabaseDialect.Sqlite, null, null, null, null, ":memory:", null, CommandTimeoutSeconds: commandTimeoutSeconds), null, CancellationToken.None);

    [Fact]
    public async Task CancelSlowStatement_ThrowsAndConnectionStaysUsable()
    {
        await using var connection = await OpenAsync();
        await CancelScenario.CancelSlowStatementAsync(connection, Slow);
    }

    [Fact]
    public async Task CommandTimeout_SlowStatement_FailsAndConnectionStaysUsable()
    {
        await using var connection = await OpenAsync(1);
        await CancelScenario.TimeoutSlowStatementAsync(connection, Slow);
    }

    [Fact]
    public async Task CancelWhileReading_DisposesQuickly()
    {
        await using var connection = await OpenAsync();
        await CancelScenario.CancelWhileReadingAsync(connection, Huge);
    }
}
