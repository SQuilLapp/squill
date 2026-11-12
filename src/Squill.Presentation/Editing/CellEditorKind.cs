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
