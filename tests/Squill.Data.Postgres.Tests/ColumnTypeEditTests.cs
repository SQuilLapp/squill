using Squill.Core;
using Squill.Data.Tests.Scenarios;
using Xunit;

namespace Squill.Data.Postgres.Tests;

public sealed class ColumnTypeEditTests(PostgresFixture server) : IClassFixture<PostgresFixture>
{
    [Fact(Skip = DockerGate.SkipReason, SkipUnless = nameof(DockerGate.Enabled), SkipType = typeof(DockerGate))]
    public async Task TypedTextIsWrittenAndReadBackEqual()
    {
        await using var connection = await new PostgresDatabaseProvider().OpenAsync(
            new ConnectionProfile(Guid.NewGuid(), "pg", DatabaseDialect.PostgreSql, server.Host, server.Port, PostgresFixture.Database, PostgresFixture.UserName, null, null),
            PostgresFixture.Password,
            CancellationToken.None);
        var script = await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, "SampleSchema", "postgres.sql"), TestContext.Current.CancellationToken);
        await using (var load = await connection.ExecuteAsync(script, [], TestContext.Current.CancellationToken))
            Assert.Null(await load.ReadBatchAsync(1, TestContext.Current.CancellationToken));

        await ColumnTypeEditScenario.RunAsync(connection, DatabaseDialect.PostgreSql, "sample",
            ("c_smallint", "smallint", "7"), ("c_integer", "integer", "70000"), ("c_bigint", "bigint", "5000000000"), ("c_numeric", "numeric(18,4)", "1234.5000"),
            ("c_real", "real", "1.5"), ("c_double", "double precision", "2.25"), ("c_boolean", "boolean", "true"), ("c_text", "text", "héllo"),
            ("c_bytea", "bytea", "0xcafe"), ("c_date", "date", "2024-05-06"), ("c_time", "time without time zone", "07:08:09"),
            ("c_timestamp", "timestamp without time zone", "2024-05-06 07:08:09"), ("c_timestamptz", "timestamp with time zone", "2024-05-06 05:08:09"),
            ("c_interval", "interval", "1.02:03:04"), ("c_uuid", "uuid", "a0eebc99-9c0b-4ef8-bb6d-6bb9bd380a11"),
            ("c_json", "json", "{\"a\": 1}"), ("c_jsonb", "jsonb", "{\"b\": [1, 2]}"),
            ("c_int_array", "integer[]", "{1,2,3}"), ("c_text_array", "text[]", "{a,b c}"));
    }
}
