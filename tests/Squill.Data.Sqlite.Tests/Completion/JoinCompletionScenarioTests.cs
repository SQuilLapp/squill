using Squill.Core;
using Squill.Data.RoundTrip;
using Xunit;

namespace Squill.Data.Sqlite.Tests;

public sealed class JoinCompletionScenarioTests
{
    [Fact]
    public async Task GeneratedJoinConditions_RunOnTheEngine()
    {
        await using var connection = await new SqliteDatabaseProvider().OpenAsync(
            new ConnectionProfile(Guid.NewGuid(), "join-completion", DatabaseDialect.Sqlite, null, null, null, null, ":memory:", null), null, TestContext.Current.CancellationToken);
        Assert.Null(await Record.ExceptionAsync(() => JoinCompletionScenario.RunAsync(connection, new JoinCompletionSpec(DatabaseDialect.Sqlite, "", "integer", "sqlite.sql"))));
    }
}
