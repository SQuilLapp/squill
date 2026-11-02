using Squill.Core;
using Squill.Data.RoundTrip;
using Xunit;

namespace Squill.Data.Postgres.Tests;

public sealed class DmlRoundTripTests(PostgresFixture server) : IClassFixture<PostgresFixture>
{
    private async Task<IDatabaseConnection> OpenAsync() => await new PostgresDatabaseProvider().OpenAsync(
        new ConnectionProfile(Guid.NewGuid(), "dml", DatabaseDialect.PostgreSql, server.Host, server.Port, PostgresFixture.Database, PostgresFixture.UserName, null, null),
        PostgresFixture.Password,
        CancellationToken.None);

    [Fact(Skip = DockerGate.SkipReason, SkipUnless = nameof(DockerGate.Enabled), SkipType = typeof(DockerGate))]
    public async Task GeneratedStatements_ApplyAndReadBack()
    {
        await using var connection = await OpenAsync();
        await DmlScenario.RunAsync(connection, DatabaseDialect.PostgreSql, "public");
    }

    [Fact(Skip = DockerGate.SkipReason, SkipUnless = nameof(DockerGate.Enabled), SkipType = typeof(DockerGate))]
    public async Task TypedParameters_ReadBackEqual()
    {
        await using var connection = await OpenAsync();
        await DmlScenario.RunTypedAsync(connection, DatabaseDialect.PostgreSql, "public");
    }
}
