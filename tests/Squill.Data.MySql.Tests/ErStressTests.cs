using Squill.Core;
using Squill.Data.RoundTrip;
using Xunit;

namespace Squill.Data.MySql.Tests;

/// <summary>Reads the 40-table er_stress group back from a real MySql server and checks the ER diagram pipeline on it.</summary>
public sealed class ErStressTests(MySqlFixture server) : IClassFixture<MySqlFixture>
{
    [Fact(Skip = DockerGate.SkipReason, SkipUnless = nameof(DockerGate.Enabled), SkipType = typeof(DockerGate))]
    public async Task ErStressGroup_ProducesAValidDiagram()
    {
        await using var connection = await new MySqlDatabaseProvider().OpenAsync(
            new ConnectionProfile(Guid.NewGuid(), "er", DatabaseDialect.MySql, server.Host, server.Port, MySqlFixture.Database, MySqlFixture.UserName, null, null),
            MySqlFixture.Password,
            CancellationToken.None);
        await ErStressScenario.RunAsync(connection, DatabaseDialect.MySql);
    }
}
