using Squill.Core;
using Squill.Data.Tests.Scenarios;
using Xunit;

namespace Squill.Data.Postgres.Tests;

public sealed class ColumnTypeReadTests(PostgresFixture server) : IClassFixture<PostgresFixture>
{
    // name|CLR type|invariant text. Types the AOT-safe slim data source cannot map read as <type> (enum: <unsupported type>).
    private const string Expected =
        """
        c_smallint|Int16|7
        c_integer|Int32|70000
        c_bigint|Int64|5000000000
        c_numeric|Decimal|1234.5000
        c_real|Single|1.5
        c_double|Double|2.25
        c_boolean|Boolean|true
        c_text|String|héllo
        c_varchar|String|vc
        c_char|String|ab 
        c_bytea|Byte[]|0xcafe
        c_date|DateOnly|2024-05-06
        c_time|TimeOnly|07:08:09
        c_timestamp|DateTime|2024-05-06 07:08:09
        c_timestamptz|DateTime|2024-05-06 05:08:09
        c_interval|TimeSpan|1.02:03:04
        c_uuid|Guid|a0eebc99-9c0b-4ef8-bb6d-6bb9bd380a11
        c_json|String|{"a": 1}
        c_jsonb|String|{"b": [1, 2]}
        c_inet|IPAddress|192.168.1.5
        c_int_array|Int32[]|{1,2,3}
        c_text_array|String[]|{a,b c}
        c_enum|String|<->
        c_serial|Int32|1
        c_identity|Int32|1
        c_range|NpgsqlRange`1|[1,5)
        c_mac|PhysicalAddress|08002B010203
        c_nan|Double|NaN
        c_infinity|Double|Infinity
        c_cidr|IPNetwork|10.0.0.0/8
        c_negative_interval|TimeSpan|-1.02:03:04.5
        c_month_interval|String|<interval>
        c_null_array|null|
        c_composite|Object[]|{1,x}
        c_plain|String|a=>1
        c_big_bytea|Byte[]|0x6161616161616161616161616161616161616161616161616161616161616161…
        """;

    [Fact(Skip = DockerGate.SkipReason, SkipUnless = nameof(DockerGate.Enabled), SkipType = typeof(DockerGate))]
    public async Task AllTypesRowIsReadAndFormatted()
    {
        await using var connection = await new PostgresDatabaseProvider().OpenAsync(
            new ConnectionProfile(Guid.NewGuid(), "pg", DatabaseDialect.PostgreSql, server.Host, server.Port, PostgresFixture.Database, PostgresFixture.UserName, null, null),
            PostgresFixture.Password,
            CancellationToken.None);
        var script = await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, "SampleSchema", "postgres.sql"), TestContext.Current.CancellationToken);
        await using (var load = await connection.ExecuteAsync(script, [], TestContext.Current.CancellationToken))
            Assert.Null(await load.ReadBatchAsync(1, TestContext.Current.CancellationToken));
        await using (var insert = await connection.ExecuteAsync(
            """
            insert into sample.all_types (c_smallint, c_integer, c_bigint, c_numeric, c_real, c_double, c_boolean, c_text, c_varchar, c_char, c_bytea, c_date, c_time, c_timestamp, c_timestamptz, c_interval, c_uuid, c_json, c_jsonb, c_inet, c_int_array, c_text_array, c_enum)
            values (7, 70000, 5000000000, 1234.5, 1.5, 2.25, true, 'héllo', 'vc', 'ab', '\xcafe', '2024-05-06', '07:08:09', '2024-05-06 07:08:09', '2024-05-06 07:08:09+02', '1 day 02:03:04', 'A0EEBC99-9C0B-4EF8-BB6D-6BB9BD380A11', '{"a": 1}', '{"b":[1,2]}', '192.168.1.5/24', '{1,2,3}', '{a,"b c"}', (select min(e) from unnest(enum_range(null::sample.mood)) e))
            """, [], TestContext.Current.CancellationToken))
            Assert.Null(await insert.ReadBatchAsync(1, TestContext.Current.CancellationToken));
        var row = await ColumnTypeScenario.ReadAsync(connection, "select * from sample.all_types");
        var extras = await ColumnTypeScenario.ReadAsync(connection,
            "select '[1,5)'::int4range as c_range, '08:00:2b:01:02:03'::macaddr as c_mac, 'NaN'::float8 as c_nan, 'Infinity'::float8 as c_infinity, '10.0.0.0/8'::cidr as c_cidr, '-1 day -02:03:04.5'::interval as c_negative_interval, '1 year 2 mons'::interval as c_month_interval, null::int[] as c_null_array, row(1, 'x') as c_composite, ('a=>1')::text as c_plain, decode(repeat('61', 1048576), 'hex') as c_big_bytea");
        foreach (var p in extras)
            row[p.Key] = p.Value;
        Assert.Equal(Expected.ReplaceLineEndings("\n"), ColumnTypeScenario.Dump(row));
    }
}
