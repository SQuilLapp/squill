using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using Squill.Core;
using Squill.Data.Tests.Scenarios;
using Squill.Presentation.Results;
using Xunit;

namespace Squill.Presentation.Tests.Results;

/// <summary>
/// Exports the rows the four engines returned (JSON captured by the Docker-gated driver tests, see <see cref="ExportSnapshotScenario"/>) and checks every cell in CSV and JSON.
/// Each line of an expected table is <c>column|CSV field|JSON value</c>; the JSON value is <c>s:</c> string, <c>n:</c> number, <c>b:</c> boolean, <c>null</c> or <c>j:</c> compact JSON; CR and LF show as &lt;CR&gt; and &lt;LF&gt;.
/// </summary>
public sealed class ExportSnapshotTests
{
    private const string Sqlite = """
        c_id|1|n:1
        c_integer|42|n:42
        c_numeric|12.5|n:12.5
        c_real|1.5|n:1.5
        c_text|héllo|s:héllo
        c_blob|0xcafe|s:yv4=
        c_datetime|2024-05-06 07:08:09|s:2024-05-06 07:08:09
        c_boolean|1|n:1
        c_untyped|3.25|n:3.25
        s_comma|a,b|s:a,b
        s_quote|say "hi"|s:say "hi"
        s_crlf|l1<CR><LF>l2|s:l1<CR><LF>l2
        s_unicode|héllo 😀 日本|s:héllo 😀 日本
        s_empty||s:
        s_null||null
        """;
    private const string PostgreSql = """
        c_smallint|7|n:7
        c_integer|70000|n:70000
        c_bigint|5000000000|n:5000000000
        c_numeric|1234.5000|n:1234.5000
        c_real|1.5|n:1.5
        c_double|2.25|n:2.25
        c_boolean|true|b:true
        c_text|héllo|s:héllo
        c_varchar|vc|s:vc
        c_char|ab |s:ab 
        c_bytea|0xcafe|s:yv4=
        c_date|2024-05-06|s:2024-05-06
        c_time|07:08:09|s:07:08:09
        c_timestamp|2024-05-06 07:08:09|s:2024-05-06T07:08:09
        c_timestamptz|2024-05-06 05:08:09|s:2024-05-06T05:08:09
        c_interval|1.02:03:04|s:1.02:03:04
        c_uuid|a0eebc99-9c0b-4ef8-bb6d-6bb9bd380a11|s:a0eebc99-9c0b-4ef8-bb6d-6bb9bd380a11
        c_json|{"a": 1}|j:{"a":1}
        c_jsonb|{"b": [1, 2]}|j:{"b":[1,2]}
        c_inet|192.168.1.5|s:192.168.1.5
        c_int_array|{1,2,3}|j:[1,2,3]
        c_text_array|{a,b c}|j:["a","b c"]
        c_enum|<->|s:<->
        c_serial|1|n:1
        c_identity|1|n:1
        c_range|[1,5)|s:[1,5)
        c_mac|08002B010203|s:08002B010203
        c_nan|NaN|s:NaN
        c_infinity|Infinity|s:Infinity
        c_cidr|10.0.0.0/8|s:10.0.0.0/8
        c_negative_interval|-1.02:03:04.5|s:-1.02:03:04.5
        c_month_interval|<interval>|s:<interval>
        c_null_array||null
        c_composite|{1,x}|s:{1,x}
        c_plain|a=>1|s:a=>1
        s_comma|a,b|s:a,b
        s_quote|say "hi"|s:say "hi"
        s_crlf|l1<CR><LF>l2|s:l1<CR><LF>l2
        s_unicode|héllo 😀 日本|s:héllo 😀 日本
        s_empty||s:
        s_null||null
        s_decimal|12345678901234567890.123456789|n:12345678901234567890.123456789
        """;
    private const string MySql = """
        c_tinyint|1|n:1
        c_smallint|7|n:7
        c_int|70000|n:70000
        c_bigint|5000000000|n:5000000000
        c_decimal|1234.5000|n:1234.5000
        c_float|1.5|n:1.5
        c_double|2.25|n:2.25
        c_bit|true|b:true
        c_bool|true|b:true
        c_char|ab|s:ab
        c_varchar|vc|s:vc
        c_text|héllo|s:héllo
        c_blob|0xcafe|s:yv4=
        c_date|2024-05-06|s:2024-05-06
        c_time|-12:03:04|s:-12:03:04
        c_datetime|2024-05-06 07:08:09.123|s:2024-05-06T07:08:09.123
        c_timestamp|2024-05-06 07:08:09|s:2024-05-06T07:08:09
        c_year|2024|n:2024
        c_json|{"a": 1}|j:{"a":1}
        c_enum|ok|s:ok
        c_set|a,c|s:a,c
        c_auto|1|n:1
        c_zero_date|0000-00-00|s:0000-00-00
        c_zero_datetime|0000-00-00 00:00:00|s:0000-00-00 00:00:00
        c_bit8|0xaa|s:qg==
        c_double_cast|1.5|n:1.5
        s_comma|a,b|s:a,b
        s_quote|say "hi"|s:say "hi"
        s_crlf|l1<CR><LF>l2|s:l1<CR><LF>l2
        s_unicode|héllo 😀 日本|s:héllo 😀 日本
        s_empty||s:
        s_null||null
        s_decimal|12345678901234567890.123456789|n:12345678901234567890.123456789
        """;
    private const string SqlServer = """
        c_tinyint|1|n:1
        c_smallint|7|n:7
        c_int|70000|n:70000
        c_bigint|5000000000|n:5000000000
        c_decimal|1234.5000|n:1234.5000
        c_real|1.5|n:1.5
        c_float|2.25|n:2.25
        c_bit|true|b:true
        c_char|ab |s:ab 
        c_nvarchar|héllo|s:héllo
        c_varchar_max|vc|s:vc
        c_varbinary_max|0xcafe|s:yv4=
        c_date|2024-05-06|s:2024-05-06
        c_time|07:08:09.123|s:07:08:09.123
        c_datetime2|2024-05-06 07:08:09.123|s:2024-05-06T07:08:09.123
        c_datetimeoffset|2024-05-06 07:08:09.123+02:00|s:2024-05-06T07:08:09.123+02:00
        c_smalldatetime|2024-05-06 07:08:00|s:2024-05-06T07:08:00
        c_money|12.3456|n:12.3456
        c_guid|a0eebc99-9c0b-4ef8-bb6d-6bb9bd380a11|s:a0eebc99-9c0b-4ef8-bb6d-6bb9bd380a11
        c_xml|<a x="1" />|s:<a x="1" />
        c_identity|1|n:1
        c_variant|1.5|n:1.5
        c_hierarchy|0x5b40|s:W0A=
        c_geography|0xe6100000010ccdcccccccccc47403333333333935ec0|s:5hAAAAEMzczMzMzMR0AzMzMzM5NewA==
        c_geometry|0x00000000010c000000000000f03f0000000000000040|s:AAAAAAEMAAAAAAAA8D8AAAAAAAAAQA==
        s_comma|a,b|s:a,b
        s_quote|say "hi"|s:say "hi"
        s_crlf|l1<CR><LF>l2|s:l1<CR><LF>l2
        s_unicode|héllo 😀 日本|s:héllo 😀 日本
        s_empty||s:
        s_null||null
        s_decimal|12345678901234567890.123456789|n:12345678901234567890.123456789
        """;

    public static TheoryData<DatabaseDialect, string> Cases() =>
        new()
        {
            { DatabaseDialect.Sqlite, Sqlite },
            { DatabaseDialect.PostgreSql, PostgreSql },
            { DatabaseDialect.MySql, MySql },
            { DatabaseDialect.SqlServer, SqlServer },
        };

    private sealed class RowSource((ColumnInfo[] Columns, object?[] Row) captured) : IGridDataSource
    {
        public IReadOnlyList<ColumnInfo> Columns => captured.Columns;
        public long RowCount => 1;
        public bool IsComplete => true;
        public bool IsTruncated => false;
        public object? GetCell(long row, int column) => captured.Row[column];
        public event EventHandler? Changed { add { } remove { } }
    }

    private static string Escape(string text) => text.Replace("\r", "<CR>").Replace("\n", "<LF>");

    private static string Describe(JsonElement e) => e.ValueKind switch
    {
        JsonValueKind.String => "s:" + Escape(e.GetString()!),
        JsonValueKind.Number => "n:" + e.GetRawText(),
        JsonValueKind.True or JsonValueKind.False => "b:" + e.GetRawText(),
        JsonValueKind.Null => "null",
        _ => "j:" + Compact(e),
    };

    private static string Compact(JsonElement e)
    {
        using var ms = new MemoryStream();
        using (var w = new Utf8JsonWriter(ms, new JsonWriterOptions { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping }))
            e.WriteTo(w);
        return Encoding.UTF8.GetString(ms.ToArray());
    }

    private static async Task<string> ActualAsync(DatabaseDialect dialect)
    {
        var captured = ExportSnapshotScenario.Load(Path.Combine(AppContext.BaseDirectory, "Results", "Snapshots", $"export-{dialect}.json"));
        var source = new RowSource(captured);
        using var csv = new MemoryStream();
        await new CsvResultExporter().ExportAsync(source, csv, TestContext.Current.CancellationToken);
        using var json = new MemoryStream();
        await new JsonResultExporter().ExportAsync(source, json, TestContext.Current.CancellationToken);

        var records = Rfc4180.Parse(Encoding.UTF8.GetString(csv.ToArray()));
        Assert.Equal(2, records.Count);
        var (header, fields) = (records[0], records[1]);
        Assert.Equal(captured.Columns.Select(c => c.Name), header);
        Assert.Equal(header.Length, fields.Length);
        using var doc = JsonDocument.Parse(json.ToArray());
        var row = doc.RootElement[0];
        Assert.Equal(header, row.EnumerateObject().Select(p => p.Name));

        var lines = new List<string>();
        for (var i = 0; i < header.Length; i++)
        {
            // The rowversion counter depends on what ran before in the database.
            if (header[i] != "c_rowversion")
                lines.Add($"{header[i]}|{Escape(fields[i])}|{Describe(row.GetProperty(header[i]))}");
        }

        return string.Join('\n', lines);
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public async Task EveryCell_IsExportedAsDocumented(DatabaseDialect dialect, string expected) =>
        Assert.Equal(expected.ReplaceLineEndings("\n"), await ActualAsync(dialect));
}

/// <summary>Minimal RFC 4180 reader: the round trip parses the export without any helper from the exporter.</summary>
internal static class Rfc4180
{
    public static List<string[]> Parse(string text)
    {
        var records = new List<string[]>();
        var fields = new List<string>();
        var field = new StringBuilder();
        var quoted = false;
        for (var i = 0; i < text.Length; i++)
        {
            var ch = text[i];
            if (quoted)
            {
                if (ch == '"' && i + 1 < text.Length && text[i + 1] == '"')
                {
                    field.Append('"');
                    i++;
                }
                else if (ch == '"')
                {
                    quoted = false;
                }
                else
                {
                    field.Append(ch);
                }
            }
            else if (ch == '"')
            {
                quoted = true;
            }
            else if (ch == ',')
            {
                fields.Add(field.ToString());
                field.Clear();
            }
            else if (ch == '\r' && i + 1 < text.Length && text[i + 1] == '\n')
            {
                fields.Add(field.ToString());
                field.Clear();
                records.Add([.. fields]);
                fields.Clear();
                i++;
            }
            else
            {
                field.Append(ch);
            }
        }

        Assert.False(quoted);
        Assert.Empty(fields);
        Assert.Equal(0, field.Length);
        return records;
    }
}
