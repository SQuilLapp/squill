using Squill.Core;
using Squill.Data.Tests.Scenarios;
using Xunit;

namespace Squill.Data.Postgres.Tests;

public sealed class ImportEndToEndTests(PostgresFixture server, ITestOutputHelper output) : IClassFixture<PostgresFixture>
{
    [Fact(Skip = DockerGate.SkipReason, SkipUnless = nameof(DockerGate.Enabled), SkipType = typeof(DockerGate))]
    public async Task CsvImportReachesTheDatabase()
    {
        var elapsed = await ImportScenario.RunAsync(new PostgresDatabaseProvider(), new ConnectionProfile(Guid.NewGuid(), "pg", DatabaseDialect.PostgreSql, server.Host, server.Port, PostgresFixture.Database, PostgresFixture.UserName, null, null), PostgresFixture.Password);
        output.WriteLine($"100,000 rows imported in {elapsed.TotalSeconds:F1} s");
        Assert.True(elapsed < TimeSpan.FromMinutes(5), $"took {elapsed}");
    }
}
