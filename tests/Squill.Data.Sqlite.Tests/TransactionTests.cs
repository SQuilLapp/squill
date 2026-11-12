using Squill.Core;
using Squill.Data.RoundTrip;
using Xunit;

namespace Squill.Data.Sqlite.Tests;

public sealed class TransactionTests : IDisposable
{
    private readonly string _path = Path.Combine(Path.GetTempPath(), $"squill-tx-{Guid.NewGuid():N}.db");

    public void Dispose()
    {
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        foreach (var file in new[] { _path, _path + "-journal", _path + "-wal", _path + "-shm" })
        {
            try
            {
                File.Delete(file);
            }
            catch (IOException)
            {
                // A temp file the pool may still hold.
            }
        }
    }

    [Fact]
    public async Task SharedScenario_PassesOnSqlite()
    {
        var provider = new SqliteDatabaseProvider();
        var profile = new ConnectionProfile(Guid.NewGuid(), "file", DatabaseDialect.Sqlite, null, null, null, null, _path, null);
        var expectations = new EngineExpectations(
            DdlTransactional: true,
            ErrorAbortsTransaction: false,
            OtherConnectionCanReadBeforeCommit: true,
            ProbeKnowsTransactions: true,
            ProbeKnowsFailure: false,
            SupportsActualPlan: false);
        await TransactionScenario.RunAsync(provider, () => provider.OpenAsync(profile, null, CancellationToken.None), expectations);
    }
}
