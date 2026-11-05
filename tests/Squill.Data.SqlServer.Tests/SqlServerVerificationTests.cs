using System.Globalization;
using Squill.Core;
using Squill.Data.RoundTrip;
using Squill.Presentation.Editor;
using Squill.Sql.Completion;
using Xunit;

namespace Squill.Data.SqlServer.Tests;

/// <summary>SQL Server-only behaviour the verification card lists: refused login, untrusted certificate, GO batches, Unicode, paging and bracket quoting.</summary>
public sealed class SqlServerVerificationTests(SqlServerFixture server) : IClassFixture<SqlServerFixture>
{
    private ConnectionProfile Profile => new(Guid.NewGuid(), "ms", DatabaseDialect.SqlServer, "127.0.0.1", server.Port, SqlServerFixture.Database, SqlServerFixture.UserName, null, null);

    private ValueTask<IDatabaseConnection> OpenAsync() => new SqlServerDatabaseProvider().OpenAsync(Profile, SqlServerFixture.Password, CancellationToken.None);

    private static string Cell(object?[] row, int i) => Convert.ToString(row[i], CultureInfo.InvariantCulture) ?? "";

    [Fact(Skip = DockerGate.SkipReason, SkipUnless = nameof(DockerGate.Enabled), SkipType = typeof(DockerGate))]
    public async Task WrongPassword_FailsWithAReadableMessage()
    {
        var error = await Assert.ThrowsAnyAsync<Exception>(async () => await new SqlServerDatabaseProvider().OpenAsync(Profile, "not-the-password", CancellationToken.None));
        Assert.Contains("Login failed", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact(Skip = DockerGate.SkipReason, SkipUnless = nameof(DockerGate.Enabled), SkipType = typeof(DockerGate))]
    public async Task UntrustedCertificate_FailsWithAMessageThatNamesTheCertificate()
    {
        var error = await Assert.ThrowsAnyAsync<Exception>(async () => await new SqlServerDatabaseProvider().OpenAsync(Profile with { Tls = TlsMode.VerifyFull }, SqlServerFixture.Password, CancellationToken.None));
        Assert.Contains("certificat", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact(Skip = DockerGate.SkipReason, SkipUnless = nameof(DockerGate.Enabled), SkipType = typeof(DockerGate))]
    public async Task GoBatches_RunAllSendsEachBatchAndUnicodeSurvives()
    {
        await using var connection = await OpenAsync();
        await MultiResultScenario.RunAsync(connection, "drop table if exists dbo.vf_go");
        var editor = new StubSqlEditor { Text = "create table dbo.vf_go (id int primary key, n nvarchar(50))\nGO\ninsert into dbo.vf_go values (1, N'日本語 ✓ é')\nGO\nselect n from dbo.vf_go" };
        var batches = RunTarget.RunAll(editor, DatabaseDialect.SqlServer);
        Assert.Equal(3, batches.Count);
        List<object?[]> last = [];
        foreach (var batch in batches)
            last = Assert.Single((await MultiResultScenario.ReadAllSetsAsync(connection, batch)).Sets.DefaultIfEmpty(([], []))).Rows;
        Assert.Equal("日本語 ✓ é", Cell(Assert.Single(last), 0));
    }

    [Fact(Skip = DockerGate.SkipReason, SkipUnless = nameof(DockerGate.Enabled), SkipType = typeof(DockerGate))]
    public async Task OffsetFetchPaging_ReturnsTheRequestedPage()
    {
        await using var connection = await OpenAsync();
        var (sets, _, _) = await MultiResultScenario.ReadAllSetsAsync(connection, "select n from (values (1),(2),(3),(4),(5),(6),(7)) v(n) order by n offset 2 rows fetch next 3 rows only");
        Assert.Equal(["3", "4", "5"], Assert.Single(sets).Rows.Select(r => Cell(r, 0)));
    }

    [Fact(Skip = DockerGate.SkipReason, SkipUnless = nameof(DockerGate.Enabled), SkipType = typeof(DockerGate))]
    public async Task BracketedNames_AreQuotedByCompletion_AndAnOpeningBracketIsReplaced()
    {
        await using var connection = await OpenAsync();
        await MultiResultScenario.RunAsync(connection, "drop table if exists dbo.[odd table]");
        await MultiResultScenario.RunAsync(connection, "create table dbo.[odd table] (id int primary key)");
        var schema = await connection.ReadSchemaAsync(CancellationToken.None);
        var provider = new CompositeCompletionProvider(new KeywordCompletionProvider(), new SchemaCompletionProvider());
        var items = provider.GetCompletions(new SqlRequest("select * from dbo.[od", 21, DatabaseDialect.SqlServer, schema));
        var item = Assert.Single(items, i => i.Label == "odd table");
        Assert.Equal("[odd table]", item.InsertText);
        Assert.Equal(18, item.ReplaceRange.Start);
    }
}
