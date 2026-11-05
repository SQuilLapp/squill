using Squill.Core;
using Squill.Data.Tests.Scenarios;
using Xunit;

namespace Squill.Data.SqlServer.Tests;

public sealed class ColumnTypeReadTests(SqlServerFixture server) : IClassFixture<SqlServerFixture>
{
    private static async Task RunAsync(IDatabaseConnection connection, string sql)
    {
        await using var reader = await connection.ExecuteAsync(sql, [], TestContext.Current.CancellationToken);
        Assert.Null(await reader.ReadBatchAsync(1, TestContext.Current.CancellationToken));
    }

    // name|CLR type|invariant text. CLR user-defined types (hierarchyid, geography, geometry) read as their serialized bytes; the 1 MB varbinary is cut to 32 bytes.
    private const string Expected =
        """
        c_tinyint|Byte|1
        c_smallint|Int16|7
        c_int|Int32|70000
        c_bigint|Int64|5000000000
        c_decimal|Decimal|1234.5000
        c_real|Single|1.5
        c_float|Double|2.25
        c_bit|Boolean|true
        c_char|String|ab 
        c_nvarchar|String|héllo
        c_varchar_max|String|vc
        c_varbinary_max|Byte[]|0xcafe
        c_date|DateTime|2024-05-06
        c_time|TimeSpan|07:08:09.123
        c_datetime2|DateTime|2024-05-06 07:08:09.123
        c_datetimeoffset|DateTimeOffset|2024-05-06 07:08:09.123+02:00
        c_smalldatetime|DateTime|2024-05-06 07:08:00
        c_money|Decimal|12.3456
        c_guid|Guid|a0eebc99-9c0b-4ef8-bb6d-6bb9bd380a11
        c_xml|String|<a x="1" />
        c_rowversion|Byte[]|0x<8 bytes>
        c_identity|Int32|1
        c_variant|Decimal|1.5
        c_hierarchy|Byte[]|0x5b40
        c_geography|Byte[]|0xe6100000010ccdcccccccccc47403333333333935ec0
        c_geometry|Byte[]|0x00000000010c000000000000f03f0000000000000040
        c_big_varbinary|Byte[]|0x6161616161616161616161616161616161616161616161616161616161616161…
        """;

    [Fact(Skip = DockerGate.SkipReason, SkipUnless = nameof(DockerGate.Enabled), SkipType = typeof(DockerGate))]
    public async Task AllTypesRowIsReadAndFormatted()
    {
        await using var connection = await new SqlServerDatabaseProvider().OpenAsync(
            new ConnectionProfile(Guid.NewGuid(), "ms", DatabaseDialect.SqlServer, server.Host, server.Port, SqlServerFixture.Database, SqlServerFixture.UserName, null, null),
            SqlServerFixture.Password,
            CancellationToken.None);
        var script = await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, "SampleSchema", "sqlserver.sql"), TestContext.Current.CancellationToken);
        foreach (var batch in script.Split("\nGO", StringSplitOptions.RemoveEmptyEntries))
            await RunAsync(connection, batch);
        await RunAsync(connection,
            """
            insert into sample.all_types (c_tinyint, c_smallint, c_int, c_bigint, c_decimal, c_real, c_float, c_bit, c_char, c_nvarchar, c_varchar_max, c_varbinary_max, c_date, c_time, c_datetime2, c_datetimeoffset, c_smalldatetime, c_money, c_guid, c_xml)
            values (1, 7, 70000, 5000000000, 1234.5, 1.5, 2.25, 1, 'ab', N'héllo', 'vc', 0xcafe, '2024-05-06', '07:08:09.123', '2024-05-06 07:08:09.123', '2024-05-06 07:08:09.123 +02:00', '2024-05-06 07:08:00', 12.3456, 'A0EEBC99-9C0B-4EF8-BB6D-6BB9BD380A11', '<a x="1"/>')
            """);
        List<CapturedColumn> capture = [];
        var row = await ColumnTypeScenario.ReadAsync(connection, "select * from sample.all_types", capture);
        foreach (var p in await ColumnTypeScenario.ReadAsync(connection,
            "select cast(1.5 as sql_variant) as c_variant, hierarchyid::Parse('/1/2/') as c_hierarchy, geography::Point(47.6, -122.3, 4326) as c_geography, geometry::Point(1, 2, 0) as c_geometry, cast(replicate(cast('a' as varchar(max)), 1048576) as varbinary(max)) as c_big_varbinary", capture))
            row[p.Key] = p.Value;
        // The rowversion counter depends on what ran before in the database.
        var dump = System.Text.RegularExpressions.Regex.Replace(ColumnTypeScenario.Dump(row), @"c_rowversion\|Byte\[\]\|0x[0-9a-f]{16}", "c_rowversion|Byte[]|0x<8 bytes>");
        await ColumnTypeScenario.ReadAsync(connection, "select 'a,b' as s_comma, 'say \"hi\"' as s_quote, 'l1' + char(13) + char(10) + 'l2' as s_crlf, N'héllo 😀 日本' as s_unicode, '' as s_empty, cast(null as nvarchar(10)) as s_null, cast(12345678901234567890.123456789 as decimal(30,9)) as s_decimal", capture);
        ExportSnapshotScenario.WriteIfRequested(DatabaseDialect.SqlServer, capture);
        Assert.Equal(Expected.ReplaceLineEndings("\n"), dump);
    }
}
