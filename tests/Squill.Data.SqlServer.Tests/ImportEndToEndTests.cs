using Squill.Core;
using Squill.Data.Tests.Scenarios;
using Xunit;

namespace Squill.Data.SqlServer.Tests;

public sealed class ImportEndToEndTests(SqlServerFixture server, ITestOutputHelper output) : IClassFixture<SqlServerFixture>
{
    [Fact(Skip = DockerGate.SkipReason, SkipUnless = nameof(DockerGate.Enabled), SkipType = typeof(DockerGate))]
    public async Task CsvImportReachesTheDatabase()
    {
        var elapsed = await ImportScenario.RunAsync(new SqlServerDatabaseProvider(), new ConnectionProfile(Guid.NewGuid(), "ms", DatabaseDialect.SqlServer, server.Host, server.Port, SqlServerFixture.Database, SqlServerFixture.UserName, null, null), SqlServerFixture.Password);
        output.WriteLine($"100,000 rows imported in {elapsed.TotalSeconds:F1} s");
        Assert.True(elapsed < TimeSpan.FromMinutes(5), $"took {elapsed}");
    }
}
