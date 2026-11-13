using System.Globalization;
using Squill.Core;

namespace Squill.Presentation.Editing;

/// <summary>Which editor a grid cell gets, chosen from the type of its column.</summary>
public enum CellEditorKind
{
    /// <summary>Free text.</summary>
    Text,

    /// <summary>A whole number.</summary>
    Whole,

    /// <summary>A number with a fractional part.</summary>
    Fractional,

    /// <summary>True, false or NULL.</summary>
    Boolean,

    /// <summary>A calendar date.</summary>
    Date,

    /// <summary>A time of day or a duration.</summary>
    Time,

    /// <summary>A date with a time of day.</summary>
    DateTime,

    /// <summary>A JSON document.</summary>
    Json,
}

/// <summary>Chooses the editor of a column and knows what text it accepts.</summary>
public static class CellEditorKinds
{
    /// <summary>Gets the values a boolean cell offers: the text to record and the label shown for it (empty text is SQL NULL).</summary>
    public static IReadOnlyList<(string Text, string Label)> BooleanChoices { get; } = [("true", "true"), ("false", "false"), ("", "NULL")];

    /// <summary>Gets the text of the choice that leaves a cell to the default value of its column.</summary>
    public const string DefaultLabel = "DEFAULT";

    /// <summary>Picks the editor of a result column.</summary>
    /// <param name="column">The column being edited.</param>
    /// <returns>The kind of editor to use.</returns>
    public static CellEditorKind For(ColumnInfo column)
    {
        ArgumentNullException.ThrowIfNull(column);
        var type = column.ClrType;
        if (type == typeof(bool))
            return CellEditorKind.Boolean;
        if (type == typeof(byte) || type == typeof(short) || type == typeof(int) || type == typeof(long)
            || type == typeof(sbyte) || type == typeof(ushort) || type == typeof(uint) || type == typeof(ulong))
            return CellEditorKind.Whole;
        if (type == typeof(float) || type == typeof(double) || type == typeof(decimal))
            return CellEditorKind.Fractional;
        if (type == typeof(DateOnly))
            return CellEditorKind.Date;
        if (type == typeof(TimeOnly) || type == typeof(TimeSpan))
            return CellEditorKind.Time;
        if (type == typeof(DateTime) || type == typeof(DateTimeOffset))
            return CellEditorKind.DateTime;
        return type == typeof(string) && column.DataTypeName.Contains("json", StringComparison.OrdinalIgnoreCase)
            ? CellEditorKind.Json
            : CellEditorKind.Text;
    }

    /// <summary>Gets a short format hint shown in an empty editor, or <see langword="null"/> when none helps.</summary>
    /// <param name="kind">The editor kind.</param>
    /// <returns>For example <c>yyyy-MM-dd</c>.</returns>
    public static string? Hint(CellEditorKind kind) => kind switch
    {
        CellEditorKind.Date => "yyyy-MM-dd",
        CellEditorKind.Time => "HH:mm:ss",
        CellEditorKind.DateTime => "yyyy-MM-dd HH:mm:ss",
        CellEditorKind.Whole => "123",
        CellEditorKind.Fractional => "123.45",
        _ => null,
    };

    /// <summary>Tells whether the editor of the kind offers a popup next to the text box: a choice for a boolean, a calendar for a date.</summary>
    /// <param name="kind">The editor kind.</param>
    /// <returns><see langword="true"/> for a boolean, a date or a date with a time.</returns>
    public static bool HasPicker(CellEditorKind kind) => kind is CellEditorKind.Boolean or CellEditorKind.Date or CellEditorKind.DateTime;

    /// <summary>Reads the date and time the popup starts from.</summary>
    /// <param name="text">The text of the editor.</param>
    /// <returns>The date it holds, or the current moment when it holds none.</returns>
    public static DateTime ParseMoment(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        return DateTime.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.None, out var moment) ? moment : DateTime.Now;
    }

    /// <summary>Builds a date or date-time value from the parts the popup shows; a day past the end of the month falls on the last day.</summary>
    /// <param name="kind">Whether the text keeps the time of day.</param>
    /// <param name="year">The year, 1 to 9999.</param>
    /// <param name="month">The month, 1 to 12.</param>
    /// <param name="day">The day of the month.</param>
    /// <param name="hour">The hour.</param>
    /// <param name="minute">The minute.</param>
    /// <param name="second">The second.</param>
    /// <returns>The text to put in the editor, for example <c>2026-11-13 07:44:16</c>.</returns>
    public static string FormatMoment(CellEditorKind kind, int year, int month, int day, int hour, int minute, int second)
    {
        year = Math.Clamp(year, 1, 9999);
        month = Math.Clamp(month, 1, 12);
        var moment = new DateTime(year, month, Math.Clamp(day, 1, DateTime.DaysInMonth(year, month)), Math.Clamp(hour, 0, 23), Math.Clamp(minute, 0, 59), Math.Clamp(second, 0, 59));
        return moment.ToString(kind == CellEditorKind.Date ? "yyyy-MM-dd" : "yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
    }

    /// <summary>Removes the characters that can never be part of a value of the kind, so a number box does not take letters.</summary>
    /// <param name="kind">The editor kind.</param>
    /// <param name="text">The text typed so far.</param>
    /// <returns>The text without the refused characters.</returns>
    public static string Sanitize(CellEditorKind kind, string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        return kind switch
        {
            CellEditorKind.Whole => Keep(text, static c => char.IsAsciiDigit(c) || c is '+' or '-'),
            CellEditorKind.Fractional => Keep(text, static c => char.IsAsciiDigit(c) || c is '+' or '-' or '.' or 'e' or 'E'),
            _ => text,
        };
    }

    private static string Keep(string text, Func<char, bool> accept)
    {
        foreach (var c in text)
        {
            if (!accept(c))
                return string.Concat(text.Where(accept));
        }

        return text;
    }
}
