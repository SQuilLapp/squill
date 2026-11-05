using Squill.Core;
using Squill.Data.RoundTrip;
using Xunit;

namespace Squill.Data.Sqlite.Tests;

public sealed class MultiResultTests
{
    [Fact]
    public async Task SingleStatement_HasOneResultSet_AndNextResultIsFalse()
    {
        await using var connection = await new SqliteDatabaseProvider().OpenAsync(
            new ConnectionProfile(Guid.NewGuid(), "mem", DatabaseDialect.Sqlite, null, null, null, null, ":memory:", null), null, CancellationToken.None);
        var (sets, messages, _) = await MultiResultScenario.ReadAllSetsAsync(connection, "select 1 as a union all select 2");
        var set = Assert.Single(sets);
        Assert.Equal(["a"], set.Columns);
        Assert.Equal(2, set.Rows.Count);
        Assert.Empty(messages);
    }
}
