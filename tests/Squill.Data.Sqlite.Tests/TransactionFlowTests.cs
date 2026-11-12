using Squill.Core;
using Squill.Data.Tests.Scenarios;
using Xunit;

namespace Squill.Data.Sqlite.Tests;

public sealed class TransactionFlowTests
{
    [Fact]
    public async Task TransactionFlowsWorkOnAFile()
    {
        var name = $"squill-tf-{Guid.NewGuid():N}.db";
        var file = Path.Combine(Path.GetTempPath(), name);
        try
        {
            await TransactionFlowScenario.RunAsync(
                new SqliteDatabaseProvider(),
                new ConnectionProfile(Guid.NewGuid(), "t", DatabaseDialect.Sqlite, null, null, null, null, file, null),
                null,
                new TransactionFlowSpec("begin", "vacuum", false, false, false, false, name));
        }
        finally
        {
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            foreach (var f in new[] { file, file + "-journal", file + "-wal", file + "-shm" })
                File.Delete(f);
        }
    }
}
