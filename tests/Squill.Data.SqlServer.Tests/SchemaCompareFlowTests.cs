using Squill.Core;
using Squill.Data.Tests.Scenarios;
using Xunit;

namespace Squill.Data.SqlServer.Tests;

public sealed class SchemaCompareFlowTests(SqlServerFixture server) : IClassFixture<SqlServerFixture>
{
    [Fact(Skip = DockerGate.SkipReason, SkipUnless = nameof(DockerGate.Enabled), SkipType = typeof(DockerGate))]
    public async Task CompareAndRunTheScript_LeavesNothingToCompare()
    {
        var source = new ConnectionProfile(Guid.NewGuid(), "source", DatabaseDialect.SqlServer, server.Host, server.Port, SqlServerFixture.Database, SqlServerFixture.UserName, null, null);
        await SchemaCompareFlowScenario.RunAsync(
            new SqlServerDatabaseProvider(), source, source with { Id = Guid.NewGuid(), Name = "target", Database = "squill_cmp_target" }, SqlServerFixture.Password, "create database squill_cmp_target");
    }
}
