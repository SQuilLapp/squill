using Squill.Core;
using Squill.Data.RoundTrip;
using Xunit;

namespace Squill.Data.MySql.Tests;

public sealed class MultiResultTests(MySqlFixture server) : IClassFixture<MySqlFixture>
{
    private ValueTask<IDatabaseConnection> OpenAsync() =>
        new MySqlDatabaseProvider().OpenAsync(
            new ConnectionProfile(Guid.NewGuid(), "multi", DatabaseDialect.MySql, server.Host, server.Port, MySqlFixture.Database, MySqlFixture.UserName, null, null),
            MySqlFixture.Password,
            CancellationToken.None);

    [Fact(Skip = DockerGate.SkipReason, SkipUnless = nameof(DockerGate.Enabled), SkipType = typeof(DockerGate))]
    public async Task CallProcedure_GivesTwoResultSets()
    {
        await using var connection = await OpenAsync();
        await MultiResultScenario.RunAsync(connection, "drop procedure if exists squill_two_sets");
        await MultiResultScenario.RunAsync(connection, "create procedure squill_two_sets() begin select 1 as a; select 'x' as b, 2 as c; end");
        try
        {
            var (sets, _, _) = await MultiResultScenario.ReadAllSetsAsync(connection, "call squill_two_sets()");
            Assert.Equal(2, sets.Count);
            Assert.Equal(["a"], sets[0].Columns);
            Assert.Equal(["b", "c"], sets[1].Columns);
            Assert.Equal("x", Assert.Single(sets[1].Rows)[0]);
        }
        finally
        {
            await MultiResultScenario.RunAsync(connection, "drop procedure if exists squill_two_sets");
        }
    }
}
