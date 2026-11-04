using Squill.Core;
using Squill.Data.RoundTrip;
using Xunit;

namespace Squill.Data.SqlServer.Tests;

/// <summary>Reads the 40-table er_stress group back from a real SqlServer server and checks the ER diagram pipeline on it.</summary>
public sealed class ErStressTests(SqlServerFixture server) : IClassFixture<SqlServerFixture>
{
    [Fact(Skip = DockerGate.SkipReason, SkipUnless = nameof(DockerGate.Enabled), SkipType = typeof(DockerGate))]
    public async Task ErStressGroup_ProducesAValidDiagram()
    {
        await using var connection = await new SqlServerDatabaseProvider().OpenAsync(
            new ConnectionProfile(Guid.NewGuid(), "er", DatabaseDialect.SqlServer, server.Host, server.Port, SqlServerFixture.Database, SqlServerFixture.UserName, null, null),
            SqlServerFixture.Password,
            CancellationToken.None);
        await ErStressScenario.RunAsync(connection, DatabaseDialect.SqlServer);
    }
}
