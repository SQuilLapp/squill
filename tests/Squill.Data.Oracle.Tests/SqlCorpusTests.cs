using Squill.Core;
using Squill.Data.RoundTrip;
using Xunit;

namespace Squill.Data.Oracle.Tests;

public sealed class SqlCorpusTests(OracleFixture server) : IClassFixture<OracleFixture>
{
    [Fact(Skip = DockerGate.SkipReason, SkipUnless = nameof(DockerGate.Enabled), SkipType = typeof(DockerGate))]
    public async Task EveryValidStatement_IsAcceptedByTheServer()
    {
        await using var connection = await new OracleDatabaseProvider().OpenAsync(
            new ConnectionProfile(Guid.NewGuid(), "corpus", DatabaseDialect.Oracle, server.Host, server.Port, OracleFixture.Service, OracleFixture.UserName, null, null),
            OracleFixture.Password,
            CancellationToken.None);

        var failures = await SqlCorpusScenario.RunAsync(connection, DatabaseDialect.Oracle, "oracle", "set transaction read write", "rollback");
        Assert.True(failures.Count == 0, string.Join("\n", failures));
    }
}
