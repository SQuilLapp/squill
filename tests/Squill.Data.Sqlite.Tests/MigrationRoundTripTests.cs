using Squill.Core;
using Squill.Data.Tests.Scenarios;
using Xunit;

namespace Squill.Data.Sqlite.Tests;

public sealed class MigrationRoundTripTests
{
    [Fact]
    public async Task AppliedScript_LeavesNoDifference()
    {
        await using var connection = await new SqliteDatabaseProvider().OpenAsync(
            new ConnectionProfile(Guid.NewGuid(), "t", DatabaseDialect.Sqlite, null, null, null, null, ":memory:", null), null, CancellationToken.None);
        await MigrationRoundTripScenario.RunAsync(connection, DatabaseDialect.Sqlite, null);
    }
}
