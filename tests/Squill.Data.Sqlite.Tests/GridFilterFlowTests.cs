using Squill.Core;
using Squill.Data.Tests.Scenarios;
using Xunit;

namespace Squill.Data.Sqlite.Tests;

public sealed class GridFilterFlowTests
{
    [Fact]
    public async Task GridFilterFlowsWorkOnAFile()
    {
        var file = Path.Combine(Path.GetTempPath(), $"squill-gf-{Guid.NewGuid():N}.db");
        try
        {
            await GridFilterFlowScenario.RunAsync(
                new SqliteDatabaseProvider(),
                new ConnectionProfile(Guid.NewGuid(), "t", DatabaseDialect.Sqlite, null, null, null, null, file, null),
                null,
                new GridFilterSpec("numeric", "text", "", "datetime", "integer", "1", "0", "text", "with recursive s(n) as (select 1 union all select n + 1 from s where n < 50000) select n from s"));
        }
        finally
        {
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            File.Delete(file);
        }
    }
}
