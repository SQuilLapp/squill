using Squill.Core;
using Squill.Data.Tests.Scenarios;
using Xunit;

namespace Squill.Data.MySql.Tests;

public sealed class ImportEndToEndTests(MySqlFixture server, ITestOutputHelper output) : IClassFixture<MySqlFixture>
{
    [Fact(Skip = DockerGate.SkipReason, SkipUnless = nameof(DockerGate.Enabled), SkipType = typeof(DockerGate))]
    public async Task CsvImportReachesTheDatabase()
    {
        var elapsed = await ImportScenario.RunAsync(new MySqlDatabaseProvider(), new ConnectionProfile(Guid.NewGuid(), "my", DatabaseDialect.MySql, server.Host, server.Port, MySqlFixture.Database, MySqlFixture.UserName, null, null), MySqlFixture.Password);
        output.WriteLine($"100,000 rows imported in {elapsed.TotalSeconds:F1} s");
        Assert.True(elapsed < TimeSpan.FromMinutes(5), $"took {elapsed}");
    }
}
