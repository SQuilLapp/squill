using Squill.Core;
using Squill.Presentation.Import;
using Xunit;

namespace Squill.Presentation.Tests.Import;

public sealed class ImportMappingTests
{
    private static ColumnDefinition Col(string name, string type, bool nullable = true, string? def = null, bool auto = false) =>
        new(name, type, nullable, false, def, auto);

    private static readonly ImportFormat French = new() { DecimalSeparator = ',', ThousandsSeparator = ' ' };

    [Fact]
    public void AutoMap_by_name_ignores_case_underscore_and_dash()
    {
        var map = ImportMapper.AutoMap(["Order_Id", "total-ttc", "x"], [Col("orderid", "int"), Col("totalttc", "numeric"), Col("y", "text"), Col("z", "text")]);
        Assert.Equal("orderid", map.Columns[0].Target?.Name);
        Assert.Equal("totalttc", map.Columns[1].Target?.Name);
        Assert.Null(map.Columns[2].Target);
        Assert.False(map.Columns[2].Enabled);
    }

    [Fact]
    public void AutoMap_by_position_only_when_counts_match()
    {
        var table = new[] { Col("a", "int"), Col("b", "int") };
        Assert.Equal("b", ImportMapper.AutoMap(["a", "other"], table).Columns[1].Target?.Name);
        Assert.Null(ImportMapper.AutoMap(["a", "other", "more"], table).Columns[1].Target);
    }

    [Fact]
    public void Converts_french_decimal_with_space_thousands()
    {
        var r = ValueConverter.Convert("1 298,00", Col("t", "numeric(10,2)"), French);
        Assert.Equal(1298.00m, r.Value);
        Assert.Null(r.Issue);
        Assert.Equal(1298m, ValueConverter.Convert("1 298,00", Col("t", "numeric"), French).Value);
    }

    [Fact]
    public void Converts_paris_date_to_the_right_instant()
    {
        var format = new ImportFormat { DateFormat = "dd/MM/yyyy HH:mm", TimeZoneId = "Europe/Paris" };
        var winter = ValueConverter.Convert("03/01/2026 09:14", Col("d", "timestamptz"), format);
        var summer = ValueConverter.Convert("03/07/2026 09:14", Col("d", "timestamptz"), format);
        Assert.Equal(new DateTime(2026, 1, 3, 8, 14, 0, DateTimeKind.Utc), winter.Value);
        Assert.Equal(new DateTime(2026, 7, 3, 7, 14, 0, DateTimeKind.Utc), summer.Value);
    }

    [Fact]
    public void Unknown_time_zone_is_rejected()
    {
        Assert.False(ImportFormat.TryResolveTimeZone("Mars/Olympus", out _));
        var r = ValueConverter.Convert("01/01/2026", Col("d", "timestamp"), new ImportFormat { DateFormat = "dd/MM/yyyy", TimeZoneId = "Mars/Olympus" });
        Assert.Equal(ColumnIssueKind.BadTimeZone, r.Issue?.Kind);
    }

    [Fact]
    public void Boolean_words_and_custom_lists()
    {
        Assert.Equal(true, ValueConverter.Convert("YES", Col("b", "boolean"), ImportFormat.Default).Value);
        var oui = new ImportFormat { TrueValues = ["oui"], FalseValues = ["non"] };
        Assert.Equal(false, ValueConverter.Convert("non", Col("b", "boolean"), oui).Value);
        Assert.Equal(ColumnIssueKind.Invalid, ValueConverter.Convert("peut-etre", Col("b", "boolean"), oui).Issue?.Kind);
    }

    [Fact]
    public void Empty_and_null_token_become_null()
    {
        Assert.Null(ValueConverter.Convert("", Col("n", "int"), ImportFormat.Default).Value);
        Assert.Null(ValueConverter.Convert("\\N", Col("n", "text"), new ImportFormat { NullToken = "\\N" }).Value);
        Assert.Equal(string.Empty, ValueConverter.Convert("", Col("n", "text"), new ImportFormat { EmptyAsNull = false }).Value);
        Assert.Equal(ColumnIssueKind.NullNotAllowed, ValueConverter.Convert("", Col("n", "int", nullable: false), ImportFormat.Default).Issue?.Kind);
    }

    [Fact]
    public void Text_passes_through_and_json_is_validated()
    {
        Assert.Equal(" a ", ValueConverter.Convert(" a ", Col("t", "varchar(10)"), ImportFormat.Default).Value);
        Assert.Null(ValueConverter.Convert("{\"a\":1}", Col("j", "jsonb"), ImportFormat.Default).Issue);
        Assert.Equal(ColumnIssueKind.NotJson, ValueConverter.Convert("hello", Col("j", "jsonb"), ImportFormat.Default).Issue?.Kind);
    }

    [Fact]
    public void Validator_counts_failures_and_reports_first_example()
    {
        var table = new[] { Col("n", "int") };
        var map = ImportMapper.AutoMap(["n"], table);
        List<string?[]> rows = [["1"], ["abc"], ["2"], ["zzz"]];
        var report = ImportValidator.Validate(map, table, rows, ImportFormat.Default).Columns.Single();
        Assert.Equal(4, report.Checked);
        Assert.Equal(2, report.Failed);
        Assert.Equal("abc", report.FirstExample);
        Assert.Equal(ColumnIssueKind.Invalid, report.Kind);
    }

    [Fact]
    public void Validator_flags_required_unmapped_and_duplicate_target()
    {
        var id = Col("id", "int", nullable: false);
        var table = new[] { id, Col("serial", "int", nullable: false, auto: true), Col("name", "text") };
        var unmapped = ImportValidator.Validate(new ImportMapping([new ColumnMap(0, "name", table[2], true)]), table, [], ImportFormat.Default);
        Assert.Equal(TableIssueKind.RequiredUnmapped, Assert.Single(unmapped.TableIssues).Kind);
        Assert.False(unmapped.CanImport);

        var dup = ImportValidator.Validate(new ImportMapping([new ColumnMap(0, "a", id, true), new ColumnMap(1, "b", id, true)]), [id], [], ImportFormat.Default);
        Assert.Equal(TableIssueKind.DuplicateTarget, Assert.Single(dup.TableIssues).Kind);
    }

    [Fact]
    public void Row_converter_returns_values_or_rejection()
    {
        var table = new[] { Col("a", "int"), Col("b", "text") };
        var converter = new ImportRowConverter(ImportMapper.AutoMap(["a", "b"], table), ImportFormat.Default);
        Assert.Equal([5, "x"], converter.Convert(["5", "x"], 2).Values);
        var bad = converter.Convert(["oops", "x"], 7);
        Assert.Null(bad.Values);
        Assert.Equal(7, bad.Rejection?.LineNumber);
        Assert.Equal("a", bad.Rejection?.ColumnName);
        Assert.Equal("oops", bad.Rejection?.RawText);
    }
}
