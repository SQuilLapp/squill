using Squill.Core;
using Squill.Data.RoundTrip;
using Xunit;

namespace Squill.Data.Sqlite.Tests;

public sealed class SqlCorpusTests
{
    [Fact]
    public async Task EveryValidStatement_IsAcceptedByTheEngine()
    {
        await using var connection = await new SqliteDatabaseProvider().OpenAsync(
            new ConnectionProfile(Guid.NewGuid(), "corpus", DatabaseDialect.Sqlite, null, null, null, null, ":memory:", null), null, CancellationToken.None);
        var script = await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, "SampleSchema", "sqlite.sql"), TestContext.Current.CancellationToken);
        await MultiResultScenario.RunAsync(connection, script);

        var failures = await SqlCorpusScenario.RunAsync(connection, DatabaseDialect.Sqlite, "sqlite", "begin", "rollback");
        Assert.True(failures.Count == 0, string.Join("\n", failures));
    }
}
