using System.Globalization;
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
        return value switch
        {
            null => null,
            DateTime d => d.ToString(d.Millisecond == 0 ? "yyyy-MM-dd HH:mm:ss" : "yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture),
            DateTimeOffset d => d.ToString(d.Millisecond == 0 ? "yyyy-MM-dd HH:mm:sszzz" : "yyyy-MM-dd HH:mm:ss.fffzzz", CultureInfo.InvariantCulture),
            bool b => b ? "true" : "false",
            byte[] bytes => bytes.Length > MaxHexBytes
                ? "0x" + Convert.ToHexStringLower(bytes.AsSpan(0, MaxHexBytes)) + "…"
                : "0x" + Convert.ToHexStringLower(bytes),
            IFormattable f => f.ToString(null, CultureInfo.InvariantCulture),
            _ => value.ToString(),
        };
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
