using Squill.Core;
using Squill.Data;
using Squill.Presentation.Localization;
using Squill.Presentation.Results;
using Xunit;

namespace Squill.Presentation.Tests.Results;

public sealed class DisplayFormatTests
{
    private static readonly ColumnInfo Col = new("c", "t", typeof(object), true, 0);

    private static string? Display(object? value, UiLanguage language)
    {
        using var _ = UiText.Override(language);
        return CellFormatter.FormatDisplay(value, Col);
    }

    [Fact]
    public void DoubleUsesTheGroupAndDecimalSeparatorsOfTheLanguage()
    {
        Assert.Equal("1 234 567,5", Display(1234567.5, UiLanguage.French));
        Assert.Equal("1,234,567.5", Display(1234567.5, UiLanguage.English));
    }

    [Fact]
    public void DecimalKeepsItsScale()
    {
        Assert.Equal("1,50", Display(1.50m, UiLanguage.French));
        Assert.Equal("1.50", Display(1.50m, UiLanguage.English));
    }

    [Fact]
    public void IntegersAndMachineValuesAreNotGrouped()
    {
        Assert.Equal("1234567", Display(1234567, UiLanguage.French));
        Assert.Equal("true", Display(true, UiLanguage.French));
        Assert.Null(Display(null, UiLanguage.English));
    }

    [Fact]
    public void DatesUseTheStandardPatternsOfTheCulture()
    {
        var d = new DateTime(2026, 11, 3, 14, 5, 9);
        Assert.Equal("03/11/2026 14:05:09", Display(d, UiLanguage.French));
        Assert.Equal("11/3/2026 2:05:09 PM", Display(d, UiLanguage.English));
        Assert.Equal("03/11/2026", Display(new DateOnly(2026, 11, 3), UiLanguage.French));
        Assert.Equal("11/3/2026", Display(new DateOnly(2026, 11, 3), UiLanguage.English));
    }

    [Fact]
    public void InvariantFormatIsUnchangedUnderBothLanguages()
    {
        foreach (var language in new[] { UiLanguage.French, UiLanguage.English })
        {
            using var _ = UiText.Override(language);
            Assert.Equal("1234567.5", CellFormatter.Format(1234567.5, Col));
            Assert.Equal("2026-11-03 14:05:09", CellFormatter.Format(new DateTime(2026, 11, 3, 14, 5, 9), Col));
        }
    }

    [Fact]
    public async Task ExportsAreInvariantUnderBothLanguages()
    {
        var source = new Source();
        foreach (var language in new[] { UiLanguage.French, UiLanguage.English })
        {
            using var _ = UiText.Override(language);
            using var csv = new MemoryStream();
            await new CsvResultExporter().ExportAsync(source, csv, TestContext.Current.CancellationToken);
            Assert.Contains("1234567.5", System.Text.Encoding.UTF8.GetString(csv.ToArray()));
            using var json = new MemoryStream();
            await new JsonResultExporter().ExportAsync(source, json, TestContext.Current.CancellationToken);
            Assert.Contains("1234567.5", System.Text.Encoding.UTF8.GetString(json.ToArray()));
        }
    }

    [Fact]
    public void CountAndDurationFollowTheLanguage()
    {
        using (UiText.Override(UiLanguage.French))
        {
            Assert.Equal("1 234", DisplayFormat.Count(1234));
            Assert.Equal("120 ms", DisplayFormat.Duration(TimeSpan.FromMilliseconds(120)));
            Assert.Equal("1,2 s", DisplayFormat.Duration(TimeSpan.FromMilliseconds(1200)));
            Assert.Equal("0:01,234", DisplayFormat.Clock(TimeSpan.FromMilliseconds(1234)));
        }
        using (UiText.Override(UiLanguage.English))
        {
            Assert.Equal("1,234", DisplayFormat.Count(1234));
            Assert.Equal("1.2 s", DisplayFormat.Duration(TimeSpan.FromMilliseconds(1200)));
            Assert.Equal("0:01.234", DisplayFormat.Clock(TimeSpan.FromMilliseconds(1234)));
        }
    }

    [Fact]
    public void RecentTimeShowsOnlyTheTimeForToday()
    {
        var now = new DateTimeOffset(2026, 11, 3, 15, 0, 0, TimeSpan.Zero);
        using var _ = UiText.Override(UiLanguage.French);
        Assert.Equal("14:05", DisplayFormat.RecentTime(now.AddMinutes(-55), now));
        Assert.Equal("01/11/2026 14:05", DisplayFormat.RecentTime(now.AddDays(-2).AddMinutes(-55), now));
    }

    private sealed class Source : IGridDataSource
    {
        public IReadOnlyList<ColumnInfo> Columns { get; } = [new("n", "t", typeof(double), true, 0)];
        public long RowCount => 1;
        public bool IsComplete => true;
        public bool IsTruncated => false;
        public object? GetCell(long row, int column) => 1234567.5;
        public event EventHandler? Changed { add { } remove { } }
    }
}
