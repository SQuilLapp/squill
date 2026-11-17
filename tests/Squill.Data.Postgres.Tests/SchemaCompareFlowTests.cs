using Squill.Core;
using Squill.Data.Tests.Scenarios;
using Xunit;

namespace Squill.Data.Postgres.Tests;

public sealed class SchemaCompareFlowTests(PostgresFixture server) : IClassFixture<PostgresFixture>
{
    [Fact(Skip = DockerGate.SkipReason, SkipUnless = nameof(DockerGate.Enabled), SkipType = typeof(DockerGate))]
    public async Task CompareAndRunTheScript_LeavesNothingToCompare()
    {
        var source = new ConnectionProfile(Guid.NewGuid(), "source", DatabaseDialect.PostgreSql, server.Host, server.Port, PostgresFixture.Database, PostgresFixture.UserName, null, null);
        await SchemaCompareFlowScenario.RunAsync(
            new PostgresDatabaseProvider(), source, source with { Id = Guid.NewGuid(), Name = "target", Database = "squill_cmp_target" }, PostgresFixture.Password, "create database squill_cmp_target");
    }
}
