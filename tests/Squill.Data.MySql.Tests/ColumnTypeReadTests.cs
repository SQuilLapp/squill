using Squill.Core;
using Squill.Data.Tests.Scenarios;
using Xunit;

namespace Squill.Data.MySql.Tests;

public sealed class ColumnTypeReadTests(MySqlFixture server) : IClassFixture<MySqlFixture>
{
    private static async Task RunAsync(IDatabaseConnection connection, string sql)
    {
        await using var reader = await connection.ExecuteAsync(sql, [], TestContext.Current.CancellationToken);
        Assert.Null(await reader.ReadBatchAsync(1, TestContext.Current.CancellationToken));
    }

    // name|CLR type|invariant text (zero dates read as DateTime.MinValue through ConvertZeroDateTime).
    private const string Expected =
        """
        c_tinyint|SByte|1
        c_smallint|Int16|7
        c_int|Int32|70000
        c_bigint|Int64|5000000000
        c_decimal|Decimal|1234.5000
        c_float|Single|1.5
        c_double|Double|2.25
        c_bit|Boolean|true
        c_bool|Boolean|true
        c_char|String|ab
        c_varchar|String|vc
        c_text|String|héllo
        c_blob|Byte[]|0xcafe
        c_date|DateTime|2024-05-06
        c_time|TimeSpan|-12:03:04
        c_datetime|DateTime|2024-05-06 07:08:09.123
        c_timestamp|DateTime|2024-05-06 07:08:09
        c_year|Int32|2024
        c_json|String|{"a": 1}
        c_enum|String|ok
        c_set|String|a,c
        c_auto|Int32|1
        c_zero_date|DateTime|0000-00-00
        c_zero_datetime|DateTime|0000-00-00 00:00:00
        c_bit8|Byte[]|0xaa
        c_double_cast|Double|1.5
        """;

    [Fact(Skip = DockerGate.SkipReason, SkipUnless = nameof(DockerGate.Enabled), SkipType = typeof(DockerGate))]
    public async Task AllTypesRowIsReadAndFormatted()
    {
        await using var connection = await new MySqlDatabaseProvider().OpenAsync(
            new ConnectionProfile(Guid.NewGuid(), "my", DatabaseDialect.MySql, server.Host, server.Port, MySqlFixture.Database, MySqlFixture.UserName, null, null),
            MySqlFixture.Password,
            CancellationToken.None);
        await RunAsync(connection, await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, "SampleSchema", "mysql.sql"), TestContext.Current.CancellationToken));
        await RunAsync(connection,
            """
            insert into all_types (c_tinyint, c_smallint, c_int, c_bigint, c_decimal, c_float, c_double, c_bit, c_bool, c_char, c_varchar, c_text, c_blob, c_date, c_time, c_datetime, c_timestamp, c_year, c_json, c_enum, c_set)
            values (1, 7, 70000, 5000000000, 1234.5, 1.5, 2.25, b'1', 1, 'ab', 'vc', 'héllo', 0xcafe, '2024-05-06', '-12:03:04', '2024-05-06 07:08:09.123', '2024-05-06 07:08:09', 2024, '{"a": 1}', 'ok', 'a,c')
            """);
        var row = await ColumnTypeScenario.ReadAsync(connection, "select * from all_types");
        await RunAsync(connection, "set session sql_mode = ''");
        await RunAsync(connection, "create temporary table zero_dates (d date, dt datetime)");
        await RunAsync(connection, "insert into zero_dates values ('0000-00-00', '0000-00-00 00:00:00')");
        foreach (var p in await ColumnTypeScenario.ReadAsync(connection, "select d as c_zero_date, dt as c_zero_datetime, b'10101010' as c_bit8, cast(1.5 as double) as c_double_cast from zero_dates"))
            row[p.Key] = p.Value;
        Assert.Equal(Expected.ReplaceLineEndings("\n"), ColumnTypeScenario.Dump(row));
    }
}
