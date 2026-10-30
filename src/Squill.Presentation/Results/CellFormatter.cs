using System.Globalization;
using Squill.Core;

namespace Squill.Presentation.Results;

/// <summary>Turns result cell values into display text.</summary>
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
}
