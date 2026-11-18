using Squill.Core;
using Squill.Data.Tests.Scenarios;
using Xunit;

namespace Squill.Data.Sqlite.Tests;

public sealed class ImportEndToEndTests(ITestOutputHelper output)
{
    [Fact]
    public async Task CsvImportReachesTheDatabase()
    {
        var elapsed = await ImportScenario.RunAsync(new SqliteDatabaseProvider(), new ConnectionProfile(Guid.NewGuid(), "t", DatabaseDialect.Sqlite, null, null, null, null, ":memory:", null), null);
        output.WriteLine($"100,000 rows imported in {elapsed.TotalSeconds:F1} s");
        Assert.True(elapsed < TimeSpan.FromSeconds(60), $"took {elapsed}");
    }
}
