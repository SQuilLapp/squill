using Squill.Core;
using Squill.Data.Tests.Scenarios;
using Xunit;

namespace Squill.Data.Sqlite.Tests;

public sealed class SchemaCompareFlowTests
{
    [Fact]
    public async Task CompareAndRunTheScript_LeavesNothingToCompare()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"squill-cmp-{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        try
        {
            ConnectionProfile Profile(string name) =>
                new(Guid.NewGuid(), name, DatabaseDialect.Sqlite, null, null, null, null, Path.Combine(dir, name + ".db"), null);
            await SchemaCompareFlowScenario.RunAsync(new SqliteDatabaseProvider(), Profile("source"), Profile("target"), null, null);
        }
        finally
        {
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            Directory.Delete(dir, true);
        }
    }
}
