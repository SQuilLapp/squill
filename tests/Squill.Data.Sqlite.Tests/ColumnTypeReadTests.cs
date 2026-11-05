using Squill.Core;
using Squill.Data.Tests.Scenarios;
using Xunit;

namespace Squill.Data.Sqlite.Tests;

public sealed class ColumnTypeReadTests
{
    [Fact]
    public async Task AllTypesRowIsReadAndFormatted()
    {
        await using var connection = await new SqliteDatabaseProvider().OpenAsync(
            new ConnectionProfile(Guid.NewGuid(), "t", DatabaseDialect.Sqlite, null, null, null, null, ":memory:", null), null, CancellationToken.None);
        var script = await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, "SampleSchema", "sqlite.sql"), TestContext.Current.CancellationToken);
        await using (var load = await connection.ExecuteAsync(script, [], TestContext.Current.CancellationToken))
            Assert.Null(await load.ReadBatchAsync(1, TestContext.Current.CancellationToken));
        await using (var insert = await connection.ExecuteAsync(
            "insert into all_types (c_integer, c_numeric, c_real, c_text, c_blob, c_datetime, c_boolean, c_untyped) values (42, 12.5, 1.5, 'héllo', x'cafe', '2024-05-06 07:08:09', 1, 3.25)", [], TestContext.Current.CancellationToken))
            Assert.Null(await insert.ReadBatchAsync(1, TestContext.Current.CancellationToken));

        List<CapturedColumn> capture = [];
        var row = await ColumnTypeScenario.ReadAsync(connection, "select * from all_types", capture);
        await ColumnTypeScenario.ReadAsync(connection, "select 'a,b' as s_comma, 'say \"hi\"' as s_quote, 'l1' || char(13) || char(10) || 'l2' as s_crlf, 'héllo 😀 日本' as s_unicode, '' as s_empty, null as s_null", capture);
        ExportSnapshotScenario.WriteIfRequested(DatabaseDialect.Sqlite, capture);
        Assert.Equal(
            """
            c_id|Int64|1
            c_integer|Int64|42
            c_numeric|Double|12.5
            c_real|Double|1.5
            c_text|String|héllo
            c_blob|Byte[]|0xcafe
            c_datetime|String|2024-05-06 07:08:09
            c_boolean|Int64|1
            c_untyped|Double|3.25
            """.ReplaceLineEndings("\n"),
            ColumnTypeScenario.Dump(row));
    }
}
