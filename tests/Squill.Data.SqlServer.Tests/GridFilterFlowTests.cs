using Squill.Core;
using Squill.Data.Tests.Scenarios;
using Xunit;

namespace Squill.Data.SqlServer.Tests;

public sealed class GridFilterFlowTests(SqlServerFixture server) : IClassFixture<SqlServerFixture>
{
    [Fact(Skip = DockerGate.SkipReason, SkipUnless = nameof(DockerGate.Enabled), SkipType = typeof(DockerGate))]
    public async Task GridFilterFlowsWorkOnSqlServer() =>
        await GridFilterFlowScenario.RunAsync(
            new SqlServerDatabaseProvider(),
            new ConnectionProfile(Guid.NewGuid(), "ms", DatabaseDialect.SqlServer, "127.0.0.1", server.Port, SqlServerFixture.Database, SqlServerFixture.UserName, null, null),
            SqlServerFixture.Password,
            new GridFilterSpec("decimal(10,2)", "nvarchar(100)", "N", "datetime2", "bit", "1", "0", "uniqueidentifier", "select top (50000) row_number() over (order by (select null)) as n from sys.all_objects a cross join sys.all_objects b"));
}
