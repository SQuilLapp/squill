using Squill.Core;
using Squill.Data.Tests.Scenarios;
using Xunit;

namespace Squill.Data.SqlServer.Tests;

public sealed class TransactionFlowTests(SqlServerFixture server) : IClassFixture<SqlServerFixture>
{
    [Fact(Skip = DockerGate.SkipReason, SkipUnless = nameof(DockerGate.Enabled), SkipType = typeof(DockerGate))]
    public async Task TransactionFlowsWorkOnSqlServer() =>
        await TransactionFlowScenario.RunAsync(
            new SqlServerDatabaseProvider(),
            new ConnectionProfile(Guid.NewGuid(), "ms", DatabaseDialect.SqlServer, "127.0.0.1", server.Port, SqlServerFixture.Database, SqlServerFixture.UserName, null, null),
            SqlServerFixture.Password,
            new TransactionFlowSpec("begin transaction", "create database tf_nope", false, false, true, true, SqlServerFixture.Database));
}
