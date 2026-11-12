using Squill.Core;
using Squill.Data.Tests.Scenarios;
using Xunit;

namespace Squill.Data.Sqlite.Tests;

public sealed class ImpactEstimateTests
{
    [Fact]
    public async Task PlannerEstimateIsWithinAFactorOfTwoAndNothingIsWritten()
    {
        Assert.Null(await Record.ExceptionAsync(() => ImpactEstimateScenario.RunAsync(new SqliteDatabaseProvider(), new ConnectionProfile(Guid.NewGuid(), "t", DatabaseDialect.Sqlite, null, null, null, null, ":memory:", null), null)));
    }
}
