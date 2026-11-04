using Squill.Core;
using Squill.Data.RoundTrip;
using Xunit;

namespace Squill.Data.Sqlite.Tests;

/// <summary>Reads the 40-table er_stress group back from SQLite and checks the ER diagram pipeline on it.</summary>
public sealed class ErStressTests
{
    [Fact]
    public async Task ErStressGroup_ProducesAValidDiagram()
    {
        await using var connection = await new SqliteDatabaseProvider().OpenAsync(
            new ConnectionProfile(Guid.NewGuid(), "er", DatabaseDialect.Sqlite, null, null, null, null, ":memory:", null), null, CancellationToken.None);
        await ErStressScenario.RunAsync(connection, DatabaseDialect.Sqlite);
    }
}
