using Squill.Core;
using Squill.Data.RoundTrip;
using Xunit;

namespace Squill.Data.Postgres.Tests;

public sealed class SqlCorpusTests(PostgresFixture server) : IClassFixture<PostgresFixture>
{
    [Fact(Skip = DockerGate.SkipReason, SkipUnless = nameof(DockerGate.Enabled), SkipType = typeof(DockerGate))]
    public async Task EveryValidStatement_IsAcceptedByTheServer()
    {
        await using var connection = await new PostgresDatabaseProvider().OpenAsync(
            new ConnectionProfile(Guid.NewGuid(), "corpus", DatabaseDialect.PostgreSql, server.Host, server.Port, PostgresFixture.Database, PostgresFixture.UserName, null, null),
            PostgresFixture.Password,
            CancellationToken.None);
        var script = await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, "SampleSchema", "postgres.sql"), TestContext.Current.CancellationToken);
        await MultiResultScenario.RunAsync(connection, script);

        var failures = await SqlCorpusScenario.RunAsync(connection, DatabaseDialect.PostgreSql, "postgres", "begin", "rollback");
        Assert.True(failures.Count == 0, string.Join("\n", failures));
    }
}
