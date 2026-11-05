using Squill.Core;
using Squill.Data.Tests.Scenarios;
using Xunit;

namespace Squill.Data.Sqlite.Tests;

public sealed class QueryFlowTests
{
    [Fact]
    public async Task QueryTabFlowsWorkOnAFile()
    {
        var file = Path.Combine(Path.GetTempPath(), $"squill-qf-{Guid.NewGuid():N}.db");
        try
        {
            await QueryFlowScenario.RunAsync(
                new SqliteDatabaseProvider(),
                new ConnectionProfile(Guid.NewGuid(), "t", DatabaseDialect.Sqlite, null, null, null, null, file, null),
                null,
                new QueryFlowSpec(
                    "with recursive s(n) as (select 1 union all select n + 1 from s where n < 100000) select n from s",
                    "with recursive s(n) as (select 1 union all select n + 1 from s where n < 100000000000) select count(*) from s",
                    ":",
                    "?",
                    "select * from qf_",
                    "qf_t"));
        }
        finally
        {
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            File.Delete(file);
        }
    }
}
