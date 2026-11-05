using Squill.Core;
using Squill.Data.Tests.Scenarios;
using Xunit;

namespace Squill.Data.SqlServer.Tests;

public sealed class ColumnTypeEditTests(SqlServerFixture server) : IClassFixture<SqlServerFixture>
{
    [Fact(Skip = DockerGate.SkipReason, SkipUnless = nameof(DockerGate.Enabled), SkipType = typeof(DockerGate))]
    public async Task TypedTextIsWrittenAndReadBackEqual()
    {
        await using var connection = await new SqlServerDatabaseProvider().OpenAsync(
            new ConnectionProfile(Guid.NewGuid(), "ms", DatabaseDialect.SqlServer, server.Host, server.Port, SqlServerFixture.Database, SqlServerFixture.UserName, null, null),
            SqlServerFixture.Password,
            CancellationToken.None);
        var script = await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, "SampleSchema", "sqlserver.sql"), TestContext.Current.CancellationToken);
        foreach (var batch in script.Split("\nGO", StringSplitOptions.RemoveEmptyEntries))
        {
            await using var load = await connection.ExecuteAsync(batch, [], TestContext.Current.CancellationToken);
            while (await load.ReadBatchAsync(1, TestContext.Current.CancellationToken) is not null)
            {
            }
        }

        await ColumnTypeEditScenario.RunAsync(connection, DatabaseDialect.SqlServer, "sample",
            ("c_tinyint", "tinyint", "7"), ("c_smallint", "smallint", "7"), ("c_int", "int", "70000"), ("c_bigint", "bigint", "5000000000"),
            ("c_decimal", "decimal(18,4)", "1234.5000"), ("c_real", "real", "1.5"), ("c_float", "float", "2.25"), ("c_bit", "bit", "true"),
            ("c_nvarchar", "nvarchar(50)", "héllo"), ("c_varbinary_max", "varbinary(max)", "0xcafe"), ("c_date", "date", "2024-05-06"),
            ("c_time", "time(7)", "07:08:09.123"), ("c_datetime2", "datetime2(3)", "2024-05-06 07:08:09.123"),
            ("c_datetimeoffset", "datetimeoffset", "2024-05-06 07:08:09.123+02:00"), ("c_money", "money", "12.3456"),
            ("c_guid", "uniqueidentifier", "a0eebc99-9c0b-4ef8-bb6d-6bb9bd380a11"), ("c_xml", "xml", "<a x=\"1\" />"));
    }
}
