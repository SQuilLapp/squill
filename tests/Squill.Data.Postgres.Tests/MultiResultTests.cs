using Squill.Core;
using Squill.Data.RoundTrip;
using Xunit;

namespace Squill.Data.Postgres.Tests;

public sealed class MultiResultTests(PostgresFixture server) : IClassFixture<PostgresFixture>
{
    private ValueTask<IDatabaseConnection> OpenAsync() =>
        new PostgresDatabaseProvider().OpenAsync(
            new ConnectionProfile(Guid.NewGuid(), "multi", DatabaseDialect.PostgreSql, server.Host, server.Port, PostgresFixture.Database, PostgresFixture.UserName, null, null),
            PostgresFixture.Password,
            CancellationToken.None);

    [Fact(Skip = DockerGate.SkipReason, SkipUnless = nameof(DockerGate.Enabled), SkipType = typeof(DockerGate))]
    public async Task TwoStatements_GiveTwoResultSets()
    {
        await using var connection = await OpenAsync();
        var (sets, _, _) = await MultiResultScenario.ReadAllSetsAsync(connection, "select 1 as a; select 'x' as b, 2 as c");
        Assert.Equal(2, sets.Count);
        Assert.Equal(["a"], sets[0].Columns);
        Assert.Equal(["b", "c"], sets[1].Columns);
        Assert.Equal(1, Assert.Single(sets[0].Rows)[0]);
        Assert.Equal("x", Assert.Single(sets[1].Rows)[0]);
    }

    [Fact(Skip = DockerGate.SkipReason, SkipUnless = nameof(DockerGate.Enabled), SkipType = typeof(DockerGate))]
    public async Task RaiseNotice_IsReturnedAsMessage()
    {
        await using var connection = await OpenAsync();
        var (_, messages, _) = await MultiResultScenario.ReadAllSetsAsync(connection, "do $$ begin raise notice 'hi'; end $$");
        Assert.Equal(["hi"], messages);
    }
}
