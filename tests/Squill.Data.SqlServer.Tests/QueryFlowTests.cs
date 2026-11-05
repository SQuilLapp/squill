using Squill.Core;
using Squill.Data.Tests.Scenarios;
using Xunit;

namespace Squill.Data.SqlServer.Tests;

public sealed class QueryFlowTests(SqlServerFixture server) : IClassFixture<SqlServerFixture>
{
    [Fact(Skip = DockerGate.SkipReason, SkipUnless = nameof(DockerGate.Enabled), SkipType = typeof(DockerGate))]
    public async Task QueryTabFlowsWorkOnSqlServer() =>
        await QueryFlowScenario.RunAsync(
            new SqlServerDatabaseProvider(),
            new ConnectionProfile(Guid.NewGuid(), "ms", DatabaseDialect.SqlServer, "127.0.0.1", server.Port, SqlServerFixture.Database, SqlServerFixture.UserName, null, null),
            SqlServerFixture.Password,
            new QueryFlowSpec(
                "select top (100000) row_number() over (order by (select null)) as n from sys.all_objects a cross join sys.all_objects b",
                "waitfor delay '00:00:30'",
                "@",
                null,
                "select * from dbo.qf_",
                "qf_t"));
}
