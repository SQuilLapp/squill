using System.Globalization;
using System.Text.Json;
using Squill.Core;
using Squill.Presentation.Localization;

namespace Squill.Presentation.Results;

/// <summary>Turns result cell values into text: <see cref="Format"/> is invariant (export, clipboard, editing), <see cref="FormatDisplay"/> follows the language.</summary>
public static class CellFormatter
{
    private const int MaxHexBytes = 32;

    /// <summary>Formats one cell with the invariant culture.</summary>
    /// <param name="value">The cell value.</param>
    /// <param name="column">The column the value belongs to.</param>
    /// <returns>The text, or <c>null</c> for SQL NULL (the view renders the placeholder).</returns>
    public static string? Format(object? value, ColumnInfo column)
    {
        ArgumentNullException.ThrowIfNull(column);
        try
        {
            return FormatCore(value, column);
        }
        catch (Exception e) when (e is not OutOfMemoryException)
        {
            return "<unsupported: " + value!.GetType().Name + ">";
        }
    }

    private static string? FormatCore(object? value, ColumnInfo column) => value switch
    {
        null => null,
        // MySqlConnector (ConvertZeroDateTime) reads the zero dates as DateTime.MinValue; show them back the way MySQL stores them.
        DateTime d when d == DateTime.MinValue => column.DataTypeName.Equals("DATE", StringComparison.OrdinalIgnoreCase) ? "0000-00-00" : "0000-00-00 00:00:00",
        DateTime d when d.TimeOfDay == TimeSpan.Zero && column.DataTypeName.Equals("DATE", StringComparison.OrdinalIgnoreCase) => d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
        DateTime d => d.ToString(d.Millisecond == 0 ? "yyyy-MM-dd HH:mm:ss" : "yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture),
        DateTimeOffset d => d.ToString(d.Millisecond == 0 ? "yyyy-MM-dd HH:mm:sszzz" : "yyyy-MM-dd HH:mm:ss.fffzzz", CultureInfo.InvariantCulture),
        DateOnly d => d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
        TimeOnly t => t.ToString(t.Ticks % TimeSpan.TicksPerSecond == 0 ? "HH:mm:ss" : "HH:mm:ss.FFFFFFF", CultureInfo.InvariantCulture),
        TimeSpan t => FormatTimeSpan(t),
        bool b => b ? "true" : "false",
        byte[] bytes => bytes.Length > MaxHexBytes
            ? "0x" + Convert.ToHexStringLower(bytes.AsSpan(0, MaxHexBytes)) + "…"
            : "0x" + Convert.ToHexStringLower(bytes),
        JsonDocument j => j.RootElement.GetRawText(),
        JsonElement j => j.GetRawText(),
        string s => s,
        Array a => "{" + string.Join(',', a.Cast<object?>().Select(e => FormatCore(e, column))) + "}",
        IFormattable f => f.ToString(null, CultureInfo.InvariantCulture),
        _ => value.ToString(),
    };

    // [-][d.]hh:mm:ss[.fffffff]
    private static string FormatTimeSpan(TimeSpan t)
    {
        var a = t.Duration();
        var text = (t < TimeSpan.Zero ? "-" : "") + (a.Days > 0 ? a.Days.ToString(CultureInfo.InvariantCulture) + "." : "") + a.ToString(@"hh\:mm\:ss", CultureInfo.InvariantCulture);
        var fraction = (a.Ticks % TimeSpan.TicksPerSecond).ToString("0000000", CultureInfo.InvariantCulture).TrimEnd('0');
        return fraction.Length == 0 ? text : text + "." + fraction;
    }

    /// <summary>Formats one cell for the grid with <see cref="UiText.Culture"/>: grouped numbers, localized dates and times. Never use it for text that is parsed back or exported.</summary>
    /// <param name="value">The cell value.</param>
    /// <param name="column">The column the value belongs to.</param>
    /// <returns>The text, or <c>null</c> for SQL NULL (the view renders the placeholder).</returns>
    public static string? FormatDisplay(object? value, ColumnInfo column)
    {
        ArgumentNullException.ThrowIfNull(column);
        var culture = UiText.Culture;
        return value switch
        {
            DateTime d => d.ToString("G", culture) + Fraction(d.Millisecond, culture),
            DateTimeOffset d => d.ToString("G", culture) + Fraction(d.Millisecond, culture) + d.ToString(" zzz", culture),
            DateOnly d => d.ToString("d", culture),
            TimeOnly t => t.ToString("T", culture),
            double or float => GroupedDecimal(Convert.ToDouble(value, culture), culture) ?? Format(value, column),
            decimal m => m.ToString("N" + m.Scale, culture),
            _ => Format(value, column),
        };
    }

    private static string Fraction(int milliseconds, CultureInfo culture) =>
        milliseconds == 0 ? "" : culture.NumberFormat.NumberDecimalSeparator + milliseconds.ToString("000", culture);

    // shortcut: very large/small doubles and NaN/Infinity keep the invariant round-trip text; fine for display.
    private static string? GroupedDecimal(double d, CultureInfo culture) =>
        double.IsFinite(d) && Math.Abs(d) is < 1e15 and (0 or >= 1e-6)
            ? d.ToString("#,0.################", culture)
            : null;
}
