using Squill.Core;
using Squill.Data.Tests.Scenarios;
using Xunit;

namespace Squill.Data.MySql.Tests;

public sealed class ColumnTypeEditTests(MySqlFixture server) : IClassFixture<MySqlFixture>
{
    [Fact(Skip = DockerGate.SkipReason, SkipUnless = nameof(DockerGate.Enabled), SkipType = typeof(DockerGate))]
    public async Task TypedTextIsWrittenAndReadBackEqual()
    {
        await using var connection = await new MySqlDatabaseProvider().OpenAsync(
            new ConnectionProfile(Guid.NewGuid(), "my", DatabaseDialect.MySql, server.Host, server.Port, MySqlFixture.Database, MySqlFixture.UserName, null, null),
            MySqlFixture.Password,
            CancellationToken.None);
        await using (var load = await connection.ExecuteAsync(
            await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, "SampleSchema", "mysql.sql"), TestContext.Current.CancellationToken), [], TestContext.Current.CancellationToken))
        {
            while (await load.ReadBatchAsync(1, TestContext.Current.CancellationToken) is not null)
            {
            }
        }

        await ColumnTypeEditScenario.RunAsync(connection, DatabaseDialect.MySql, null,
            ("c_tinyint", "tinyint", "1"), ("c_smallint", "smallint", "7"), ("c_int", "int", "70000"), ("c_bigint", "bigint", "5000000000"),
            ("c_decimal", "decimal(18,4)", "1234.5000"), ("c_float", "float", "1.5"), ("c_double", "double", "2.25"), ("c_bit", "bit(1)", "true"),
            ("c_char", "char(3)", "ab"), ("c_text", "text", "héllo"), ("c_blob", "blob", "0xcafe"), ("c_date", "date", "2024-05-06"),
            ("c_time", "time", "-12:03:04"), ("c_datetime", "datetime(3)", "2024-05-06 07:08:09.123"), ("c_timestamp", "timestamp", "2024-05-06 07:08:09"),
            ("c_year", "year", "2024"), ("c_json", "json", "{\"a\": 1}"), ("c_enum", "enum('sad','ok','happy')", "ok"), ("c_set", "set('a','b','c')", "a,c"));
    }
}
