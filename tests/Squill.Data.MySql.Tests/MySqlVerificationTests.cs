using System.Globalization;
using Squill.Core;
using Squill.Data.RoundTrip;
using Squill.Sql.Completion;
using Xunit;

namespace Squill.Data.MySql.Tests;

/// <summary>MySQL-only behaviour the verification card lists: upsert counts, zero dates, a refused login, table-name case and backtick quoting.</summary>
public sealed class MySqlVerificationTests(MySqlFixture server) : IClassFixture<MySqlFixture>
{
    private ConnectionProfile Profile => new(Guid.NewGuid(), "my", DatabaseDialect.MySql, server.Host, server.Port, MySqlFixture.Database, MySqlFixture.UserName, null, null);

    private ValueTask<IDatabaseConnection> OpenAsync() => new MySqlDatabaseProvider().OpenAsync(Profile, MySqlFixture.Password, CancellationToken.None);

    [Fact(Skip = DockerGate.SkipReason, SkipUnless = nameof(DockerGate.Enabled), SkipType = typeof(DockerGate))]
    public async Task OnDuplicateKeyUpdate_RunsAndReportsMySqlAffectedCounts()
    {
        await using var connection = await OpenAsync();
        await MultiResultScenario.RunAsync(connection, "drop table if exists vf_dup");
        await MultiResultScenario.RunAsync(connection, "create table vf_dup (id int primary key, n int)");
        Assert.Equal(1, (await MultiResultScenario.ReadAllSetsAsync(connection, "insert into vf_dup values (1, 1) on duplicate key update n = n + 1")).Affected);
        Assert.Equal(2, (await MultiResultScenario.ReadAllSetsAsync(connection, "insert into vf_dup values (1, 1) on duplicate key update n = n + 1")).Affected); // MySQL counts an update as 2
        var (sets, _, _) = await MultiResultScenario.ReadAllSetsAsync(connection, "select n from vf_dup");
        Assert.Equal(2, Convert.ToInt32(Assert.Single(Assert.Single(sets).Rows)[0], CultureInfo.InvariantCulture));
    }

    [Fact(Skip = DockerGate.SkipReason, SkipUnless = nameof(DockerGate.Enabled), SkipType = typeof(DockerGate))]
    public async Task ZeroDate_IsReadWithoutFailing()
    {
        await using var connection = await OpenAsync();
        await MultiResultScenario.RunAsync(connection, "drop table if exists vf_zero");
        await MultiResultScenario.RunAsync(connection, "create table vf_zero (id int primary key, d date, t datetime)");
        await MultiResultScenario.RunAsync(connection, "set session sql_mode = ''");
        await MultiResultScenario.RunAsync(connection, "insert into vf_zero values (1, '0000-00-00', '0000-00-00 00:00:00')");
        var (sets, _, _) = await MultiResultScenario.ReadAllSetsAsync(connection, "select d, t from vf_zero");
        var row = Assert.Single(Assert.Single(sets).Rows);
        Assert.Equal(2, row.Length);
    }

    [Fact(Skip = DockerGate.SkipReason, SkipUnless = nameof(DockerGate.Enabled), SkipType = typeof(DockerGate))]
    public async Task WrongPassword_FailsWithAReadableMessage()
    {
        var error = await Assert.ThrowsAnyAsync<Exception>(async () => await new MySqlDatabaseProvider().OpenAsync(Profile, "not-the-password", CancellationToken.None));
        Assert.Contains("denied", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact(Skip = DockerGate.SkipReason, SkipUnless = nameof(DockerGate.Enabled), SkipType = typeof(DockerGate))]
    public async Task TableNameCase_IsKeptAsTheServerStoresIt_AndBacktickNamesAreQuotedByCompletion()
    {
        await using var connection = await OpenAsync();
        await MultiResultScenario.RunAsync(connection, "drop table if exists `MixedCase`");
        await MultiResultScenario.RunAsync(connection, "drop table if exists `odd table`");
        await MultiResultScenario.RunAsync(connection, "create table `MixedCase` (id int primary key)");
        await MultiResultScenario.RunAsync(connection, "create table `odd table` (id int primary key)");
        var (sets, _, _) = await MultiResultScenario.ReadAllSetsAsync(connection, "select @@lower_case_table_names");
        var folded = Convert.ToInt32(Assert.Single(Assert.Single(sets).Rows)[0], CultureInfo.InvariantCulture) != 0;
        var schema = await connection.ReadSchemaAsync(CancellationToken.None);
        Assert.Contains(schema.Tables, t => t.Name == (folded ? "mixedcase" : "MixedCase"));

        var provider = new CompositeCompletionProvider(new KeywordCompletionProvider(), new SchemaCompletionProvider());
        var items = provider.GetCompletions(new SqlRequest("select * from `od", 17, DatabaseDialect.MySql, schema));
        Assert.Equal("`odd table`", Assert.Single(items, i => i.Label == "odd table").InsertText);
    }
}
