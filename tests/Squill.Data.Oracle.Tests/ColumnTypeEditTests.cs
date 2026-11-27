using Squill.Core;
using Squill.Data.Tests.Scenarios;
using Xunit;

namespace Squill.Data.Oracle.Tests;

public sealed class ColumnTypeEditTests(OracleFixture server) : IClassFixture<OracleFixture>
{
    [Fact(Skip = DockerGate.SkipReason, SkipUnless = nameof(DockerGate.Enabled), SkipType = typeof(DockerGate))]
    public async Task TypedTextIsWrittenAndReadBackEqual()
    {
        await using var connection = await new OracleDatabaseProvider().OpenAsync(
            new ConnectionProfile(Guid.NewGuid(), "ora", DatabaseDialect.Oracle, server.Host, server.Port, OracleFixture.Service, OracleFixture.UserName, null, null),
            OracleFixture.Password,
            CancellationToken.None);
        await using (var create = await connection.ExecuteAsync(
            "create table \"all_types\" (\"c_number\" number(18,4), \"c_int\" number(10), \"c_varchar2\" varchar2(100), \"c_clob\" clob, \"c_blob\" blob, \"c_date\" date, \"c_ts\" timestamp(6), \"c_tstz\" timestamp(6) with time zone, \"c_raw\" raw(16))",
            [], TestContext.Current.CancellationToken))
            await create.ReadAllAsync(1, TestContext.Current.CancellationToken);
        try
        {
            await ColumnTypeEditScenario.RunAsync(connection, DatabaseDialect.Oracle, null,
                ("c_number", "NUMBER(18,4)", "1234.5"), ("c_int", "NUMBER(10)", "70000"), ("c_varchar2", "VARCHAR2(100)", "héllo ' \"q\""),
                ("c_clob", "CLOB", "a long text\nwith two lines"), ("c_blob", "BLOB", "0xcafe"),
                ("c_date", "DATE", "2024-05-06 07:08:09"), ("c_date", "DATE", "2024-05-06"),
                ("c_ts", "TIMESTAMP(6)", "2024-05-06 07:08:09.123"), ("c_tstz", "TIMESTAMP(6) WITH TIME ZONE", "2024-05-06 07:08:09.123+02:00"),
                ("c_raw", "RAW(16)", "0x00ff10"));
        }
        finally
        {
            await using var drop = await connection.ExecuteAsync("drop table \"all_types\"", [], CancellationToken.None);
            await drop.ReadAllAsync(1, CancellationToken.None);
        }
    }
}
