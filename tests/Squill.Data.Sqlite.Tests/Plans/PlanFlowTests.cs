using Squill.Core;
using Squill.Data.Tests.Scenarios;
using Xunit;

namespace Squill.Data.Sqlite.Tests;


public sealed class PlanFlowTests
{
    [Fact]
    public async Task ExplainAndAnalyzeFlowsWorkOnAFile()
    {
        var file = Path.Combine(Path.GetTempPath(), $"squill-pf-{Guid.NewGuid():N}.db");
        try
        {
            await PlanFlowScenario.RunAsync(
                new SqliteDatabaseProvider(),
                new ConnectionProfile(Guid.NewGuid(), "t", DatabaseDialect.Sqlite, null, null, null, null, file, null),
                null,
                new PlanFlowSpec(false, "", ":", null, null));
        }
        finally
        {
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            File.Delete(file);
        }
    }
}
