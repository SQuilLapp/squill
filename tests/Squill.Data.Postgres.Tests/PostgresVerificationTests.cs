using System.Globalization;
using Squill.Core;
using Squill.Data.RoundTrip;
using Squill.Sql.Completion;
using Xunit;

namespace Squill.Data.Postgres.Tests;

/// <summary>PostgreSQL-only behaviour the verification card lists: RETURNING, a refused login and quoted mixed-case identifiers.</summary>
public sealed class PostgresVerificationTests(PostgresFixture server) : IClassFixture<PostgresFixture>
{
    private ConnectionProfile Profile => new(Guid.NewGuid(), "pg", DatabaseDialect.PostgreSql, server.Host, server.Port, PostgresFixture.Database, PostgresFixture.UserName, null, null);

    private ValueTask<IDatabaseConnection> OpenAsync() => new PostgresDatabaseProvider().OpenAsync(Profile, PostgresFixture.Password, CancellationToken.None);

    [Fact(Skip = DockerGate.SkipReason, SkipUnless = nameof(DockerGate.Enabled), SkipType = typeof(DockerGate))]
    public async Task InsertReturning_ReturnsTheGeneratedRowAsAResultSet()
    {
        await using var connection = await OpenAsync();
        await MultiResultScenario.RunAsync(connection, "drop table if exists public.vf_ret; create table public.vf_ret (id serial primary key, name text)");
        var (sets, _, _) = await MultiResultScenario.ReadAllSetsAsync(connection, "insert into public.vf_ret (name) values ('a'), ('b') returning id, name");
        var set = Assert.Single(sets);
        Assert.Equal(["id", "name"], set.Columns);
        Assert.Equal(["1|a", "2|b"], set.Rows.Select(r => string.Join('|', r.Select(c => Convert.ToString(c, CultureInfo.InvariantCulture)))));
    }

    [Fact(Skip = DockerGate.SkipReason, SkipUnless = nameof(DockerGate.Enabled), SkipType = typeof(DockerGate))]
    public async Task WrongPassword_FailsWithAReadableMessage()
    {
        var error = await Assert.ThrowsAnyAsync<Exception>(async () => await new PostgresDatabaseProvider().OpenAsync(Profile, "not-the-password", CancellationToken.None));
        Assert.Contains("password", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact(Skip = DockerGate.SkipReason, SkipUnless = nameof(DockerGate.Enabled), SkipType = typeof(DockerGate))]
    public async Task QuotedMixedCaseTable_IsOfferedByCompletion()
    {
        await using var connection = await OpenAsync();
        await MultiResultScenario.RunAsync(connection, "drop table if exists public.\"MixedCase\"; create table public.\"MixedCase\" (\"Id\" int primary key, \"Full Name\" text)");
        var schema = await connection.ReadSchemaAsync(CancellationToken.None);
        var provider = new CompositeCompletionProvider(new KeywordCompletionProvider(), new SchemaCompletionProvider());
        foreach (var text in new[] { "select * from public.", "select * from \"Mix" })
        {
            var items = provider.GetCompletions(new SqlRequest(text, text.Length, DatabaseDialect.PostgreSql, schema));
            var item = Assert.Single(items, i => i.Label.Contains("MixedCase", StringComparison.Ordinal));
            Assert.Contains("\"MixedCase\"", item.InsertText, StringComparison.Ordinal);
        }
    }
}
