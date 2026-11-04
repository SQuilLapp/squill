using Squill.Core;
using Squill.Data.RoundTrip;
using Xunit;

namespace Squill.Data.SqlServer.Tests;

public sealed class MultiResultTests(SqlServerFixture server) : IClassFixture<SqlServerFixture>
{
    private ValueTask<IDatabaseConnection> OpenAsync() =>
        new SqlServerDatabaseProvider().OpenAsync(
            new ConnectionProfile(Guid.NewGuid(), "multi", DatabaseDialect.SqlServer, server.Host, server.Port, SqlServerFixture.Database, SqlServerFixture.UserName, null, null),
            SqlServerFixture.Password,
            CancellationToken.None);

    [Fact(Skip = DockerGate.SkipReason, SkipUnless = nameof(DockerGate.Enabled), SkipType = typeof(DockerGate))]
    public async Task ExecProcedure_GivesTwoResultSetsAndPrintMessage()
    {
        await using var connection = await OpenAsync();
        await MultiResultScenario.RunAsync(connection, "create or alter procedure dbo.squill_two_sets as begin select 1 as a; select 'x' as b, 2 as c; print 'hello'; end");
        try
        {
            var (sets, messages, _) = await MultiResultScenario.ReadAllSetsAsync(connection, "exec dbo.squill_two_sets");
            Assert.Equal(2, sets.Count);
            Assert.Equal(["a"], sets[0].Columns);
            Assert.Equal(["b", "c"], sets[1].Columns);
            Assert.Equal("x", Assert.Single(sets[1].Rows)[0]);
            Assert.Equal(["hello"], messages);
        }
        finally
        {
            await MultiResultScenario.RunAsync(connection, "drop procedure if exists dbo.squill_two_sets");
        }
    }
}
