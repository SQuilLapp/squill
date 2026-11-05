using Squill.Core;
using Squill.Data.Tests.Scenarios;
using Xunit;

namespace Squill.Data.Sqlite.Tests;

public sealed class GridEditEndToEndTests
{
    [Fact]
    public async Task GridEditsReachTheDatabase() =>
        Assert.Null(await Record.ExceptionAsync(async () => await GridEditScenario.RunAsync(new SqliteDatabaseProvider(), new ConnectionProfile(Guid.NewGuid(), "t", DatabaseDialect.Sqlite, null, null, null, null, ":memory:", null), null)));
}
