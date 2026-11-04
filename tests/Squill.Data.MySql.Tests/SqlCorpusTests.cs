using Squill.Core;
using Squill.Data.RoundTrip;
using Xunit;

namespace Squill.Data.MySql.Tests;

public sealed class SqlCorpusTests(MySqlFixture server) : IClassFixture<MySqlFixture>
{
    [Fact(Skip = DockerGate.SkipReason, SkipUnless = nameof(DockerGate.Enabled), SkipType = typeof(DockerGate))]
    public async Task EveryValidStatement_IsAcceptedByTheServer()
    {
        await using var connection = await new MySqlDatabaseProvider().OpenAsync(
            new ConnectionProfile(Guid.NewGuid(), "corpus", DatabaseDialect.MySql, server.Host, server.Port, MySqlFixture.Database, MySqlFixture.UserName, null, null),
            MySqlFixture.Password,
            CancellationToken.None);

        // MySQL DDL commits implicitly, so the corpus runs on a scratch database that is dropped afterwards.
        await MultiResultScenario.RunAsync(connection, "drop database if exists corpus_scratch");
        await MultiResultScenario.RunAsync(connection, "create database corpus_scratch");
        try
        {
            await MultiResultScenario.RunAsync(connection, "use corpus_scratch");
            var script = await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, "SampleSchema", "mysql.sql"), TestContext.Current.CancellationToken);
            await MultiResultScenario.RunAsync(connection, script);

            var failures = await SqlCorpusScenario.RunAsync(connection, DatabaseDialect.MySql, "mysql", "start transaction", "rollback");
            Assert.True(failures.Count == 0, string.Join("\n", failures));
        }
        finally
        {
            await MultiResultScenario.RunAsync(connection, "use " + MySqlFixture.Database);
            await MultiResultScenario.RunAsync(connection, "drop database if exists corpus_scratch");
        }
    }
}
