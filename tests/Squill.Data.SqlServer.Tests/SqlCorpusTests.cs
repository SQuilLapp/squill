using Squill.Core;
using Squill.Data.RoundTrip;
using Xunit;

namespace Squill.Data.SqlServer.Tests;

public sealed class SqlCorpusTests(SqlServerFixture server) : IClassFixture<SqlServerFixture>
{
    [Fact(Skip = DockerGate.SkipReason, SkipUnless = nameof(DockerGate.Enabled), SkipType = typeof(DockerGate))]
    public async Task EveryValidStatement_IsAcceptedByTheServer()
    {
        await using var connection = await new SqlServerDatabaseProvider().OpenAsync(
            new ConnectionProfile(Guid.NewGuid(), "corpus", DatabaseDialect.SqlServer, server.Host, server.Port, SqlServerFixture.Database, SqlServerFixture.UserName, null, null),
            SqlServerFixture.Password,
            CancellationToken.None);

        // GO is a client convention, not T-SQL: the sample script is sent batch by batch.
        var script = await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, "SampleSchema", "sqlserver.sql"), TestContext.Current.CancellationToken);
        foreach (var batch in script.ReplaceLineEndings("\n").Split("\nGO\n"))
            await MultiResultScenario.RunAsync(connection, batch);

        var failures = await SqlCorpusScenario.RunAsync(connection, DatabaseDialect.SqlServer, "sqlserver", "begin tran", "if @@trancount > 0 rollback");
        Assert.True(failures.Count == 0, string.Join("\n", failures));
    }
}
