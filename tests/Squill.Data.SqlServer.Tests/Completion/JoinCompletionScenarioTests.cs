using Squill.Core;
using Squill.Data.RoundTrip;
using Xunit;

namespace Squill.Data.SqlServer.Tests;

public sealed class JoinCompletionScenarioTests(SqlServerFixture server) : IClassFixture<SqlServerFixture>
{
    [Fact(Skip = DockerGate.SkipReason, SkipUnless = nameof(DockerGate.Enabled), SkipType = typeof(DockerGate))]
    public async Task GeneratedJoinConditions_RunOnTheEngine()
    {
        await using var connection = await new SqlServerDatabaseProvider().OpenAsync(
            new ConnectionProfile(Guid.NewGuid(), "join-completion", DatabaseDialect.SqlServer, server.Host, server.Port, SqlServerFixture.Database, SqlServerFixture.UserName, null, null),
            SqlServerFixture.Password,
            CancellationToken.None);
        await JoinCompletionScenario.RunAsync(connection, new JoinCompletionSpec(DatabaseDialect.SqlServer, "sample.", "int", "sqlserver.sql"));
    }
}
