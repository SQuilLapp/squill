using Squill.Core;
using Squill.Data.Tests.Scenarios;
using Xunit;

namespace Squill.Data.MySql.Tests;

public sealed class SchemaCompareFlowTests(MySqlFixture server) : IClassFixture<MySqlFixture>
{
    [Fact(Skip = DockerGate.SkipReason, SkipUnless = nameof(DockerGate.Enabled), SkipType = typeof(DockerGate))]
    public async Task CompareAndRunTheScript_LeavesNothingToCompare()
    {
        var source = new ConnectionProfile(Guid.NewGuid(), "source", DatabaseDialect.MySql, server.Host, server.Port, MySqlFixture.Database, MySqlFixture.UserName, null, null);
        await SchemaCompareFlowScenario.RunAsync(
            new MySqlDatabaseProvider(), source, source with { Id = Guid.NewGuid(), Name = "target", Database = "squill_cmp_target" }, MySqlFixture.Password, "create database squill_cmp_target");
    }
}
