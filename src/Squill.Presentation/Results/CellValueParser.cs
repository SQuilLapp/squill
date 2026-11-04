using System.Globalization;
using System.Numerics;
using Squill.Core;
using Squill.Presentation.Localization;

namespace Squill.Presentation.Results;

/// <summary>Turns text typed into a grid cell back into the CLR value passed as a statement parameter; the inverse of <see cref="CellFormatter"/>.</summary>
public static class CellValueParser
{
    private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;

    private static readonly string[] DateTimeFormats =
    [
        "yyyy-MM-dd HH:mm:ss.FFFFFFFK", "yyyy-MM-ddTHH:mm:ss.FFFFFFFK", "yyyy-MM-dd",
    ];

    private static readonly string[] DateTimeOffsetFormats =
    [
        "yyyy-MM-dd HH:mm:ss.FFFFFFFzzz", "yyyy-MM-ddTHH:mm:ss.FFFFFFFzzz",
        "yyyy-MM-dd HH:mm:ss.FFFFFFF'Z'", "yyyy-MM-ddTHH:mm:ss.FFFFFFF'Z'",
    ];

    // DateOnly.ToString() uses the invariant short pattern, which CellFormatter shows.
    private static readonly string[] DateFormats = ["yyyy-MM-dd", "MM/dd/yyyy"];

    /// <summary>Parses the text typed into a cell.</summary>
    /// <param name="text">The typed text, or <c>null</c> for SQL NULL.</param>
    /// <param name="column">The column being edited; its <see cref="ColumnInfo.ClrType"/> selects the target type.</param>
    /// <param name="value">The parsed value, <c>null</c> for SQL NULL.</param>
    /// <param name="error">A short user-facing message when parsing fails, otherwise <c>null</c>.</param>
    /// <returns><c>true</c> when the text is valid for the column type.</returns>
    public static bool TryParse(string? text, ColumnInfo column, out object? value, out string? error)
    {
        ArgumentNullException.ThrowIfNull(column);
        value = null;
        error = null;
        if (text is null)
        {
            return true;
        }

        var type = column.ClrType;
        if (type == typeof(string))
        {
            value = text;
            return true;
        }

        var s = text.Trim();
        if (s.Length == 0 && IsKnown(type))
        {
            return true;
        }

        if (type == typeof(bool)) { return Set(ParseBool(s), out value, out error, text, "true/false"); }
        if (type == typeof(byte)) { return Integer<byte>(s, text, "byte", out value, out error); }
        if (type == typeof(short)) { return Integer<short>(s, text, "short", out value, out error); }
        if (type == typeof(int)) { return Integer<int>(s, text, "int", out value, out error); }
        if (type == typeof(long)) { return Integer<long>(s, text, "long", out value, out error); }
        if (type == typeof(sbyte)) { return Integer<sbyte>(s, text, "sbyte", out value, out error); }
        if (type == typeof(ushort)) { return Integer<ushort>(s, text, "ushort", out value, out error); }
        if (type == typeof(uint)) { return Integer<uint>(s, text, "uint", out value, out error); }
        if (type == typeof(ulong)) { return Integer<ulong>(s, text, "ulong", out value, out error); }
        if (type == typeof(float)) { return Set(float.TryParse(s, NumberStyles.Float, Invariant, out var f) ? f : null, out value, out error, text, UiText.Get("results.type.number")); }
        if (type == typeof(double)) { return Set(double.TryParse(s, NumberStyles.Float, Invariant, out var d) ? d : null, out value, out error, text, UiText.Get("results.type.number")); }
        if (type == typeof(decimal)) { return Set(decimal.TryParse(s, NumberStyles.Float, Invariant, out var m) ? m : null, out value, out error, text, UiText.Get("results.type.number")); }
        if (type == typeof(DateTime))
        {
            if (s is "0000-00-00" or "0000-00-00 00:00:00") { value = DateTime.MinValue; return true; }
            // Npgsql refuses Kind=Unspecified for timestamptz; the grid shows these values in UTC.
            var style = column.DataTypeName.Contains("TIMESTAMPTZ", StringComparison.OrdinalIgnoreCase) ? DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal : DateTimeStyles.RoundtripKind;
            return Set(DateTime.TryParseExact(s, DateTimeFormats, Invariant, style, out var dt) ? dt : null, out value, out error, text, UiText.Get("results.type.dateTime"));
        }

        if (type == typeof(DateTimeOffset))
        {
            return Set(DateTimeOffset.TryParseExact(s, DateTimeOffsetFormats, Invariant, DateTimeStyles.AssumeUniversal, out var dto) ? dto : null, out value, out error, text, UiText.Get("results.type.dateTime"));
        }

        if (type == typeof(DateOnly)) { return Set(DateOnly.TryParseExact(s, DateFormats, Invariant, DateTimeStyles.None, out var da) ? da : null, out value, out error, text, UiText.Get("results.type.date")); }
        if (type == typeof(TimeOnly)) { return Set(TimeOnly.TryParse(s, Invariant, DateTimeStyles.None, out var to) ? to : null, out value, out error, text, UiText.Get("results.type.time")); }
        if (type == typeof(TimeSpan)) { return Set(TimeSpan.TryParse(s, Invariant, out var ts) ? ts : null, out value, out error, text, UiText.Get("results.type.time")); }
        if (type == typeof(Guid)) { return Set(Guid.TryParse(s, out var g) ? g : null, out value, out error, text, "GUID"); }
        if (type == typeof(byte[])) { return Set(ParseHex(s), out value, out error, text, UiText.Get("results.type.binary")); }
        if (type == typeof(int[])) { return SetArray(s, text, v => int.TryParse(v, NumberStyles.Integer, Invariant, out var n) ? (int?)n : null, out value, out error, "int[]"); }
        if (type == typeof(string[])) { return SetTextArray(s, text, out value, out error, "text[]"); }

        value = text;
        return true;
    }

    /// <summary>Whether text typed into a column of this CLR type can be turned back into a value the driver accepts.</summary>
    /// <param name="type">The CLR type the driver reports for the column.</param>
    /// <returns><c>false</c> for types with no text form to write back (ranges, network types, composites).</returns>
    public static bool IsSupported(Type type) => type == typeof(string) || type == typeof(int[]) || type == typeof(string[]) || type == typeof(object) || IsKnown(type);

    private static bool SetArray<T>(string s, string text, Func<string, T?> convert, out object? value, out string? error, string friendly)
        where T : struct
    {
        var items = SplitArray(s);
        var result = new T[items?.Count ?? 0];
        for (var i = 0; items is not null && i < items.Count; i++)
        {
            if (items[i] is not { } item || convert(item) is not { } parsed)
            {
                items = null;
                break;
            }

            result[i] = parsed;
        }

        value = items is null ? null : result;
        error = items is null ? UiText.Format("results.invalidValue", text, friendly) : null;
        return items is not null;
    }

    private static bool SetTextArray(string s, string text, out object? value, out string? error, string friendly)
    {
        var items = SplitArray(s);
        value = items?.ToArray();
        error = items is null ? UiText.Format("results.invalidValue", text, friendly) : null;
        return items is not null;
    }

    // Splits a PostgreSQL array literal such as {1,"b c",NULL}; an unquoted NULL is a null element. Returns null when the braces do not match.
    private static List<string?>? SplitArray(string s)
    {
        if (s.Length < 2 || s[0] != '{' || s[^1] != '}')
        {
            return null;
        }

        var items = new List<string?>();
        var body = s[1..^1];
        if (body.Trim().Length == 0)
        {
            return items;
        }

        var current = new System.Text.StringBuilder();
        var quoted = false;
        var inQuotes = false;
        for (var i = 0; i < body.Length; i++)
        {
            var c = body[i];
            if (inQuotes && c == '\\' && i + 1 < body.Length)
            {
                current.Append(body[++i]);
            }
            else if (c == '"')
            {
                inQuotes = !inQuotes;
                quoted = true;
            }
            else if (c == ',' && !inQuotes)
            {
                items.Add(Take());
            }
            else
            {
                current.Append(c);
            }
        }

        if (inQuotes)
        {
            return null;
        }

        items.Add(Take());
        return items;

        string? Take()
        {
            var item = quoted ? current.ToString() : current.ToString().Trim();
            var isNull = !quoted && item.Equals("NULL", StringComparison.OrdinalIgnoreCase);
            current.Clear();
            quoted = false;
            return isNull ? null : item;
        }
    }

    private static bool IsKnown(Type t) =>
        t == typeof(bool) || t == typeof(byte) || t == typeof(short) || t == typeof(int) || t == typeof(long)
        || t == typeof(sbyte) || t == typeof(ushort) || t == typeof(uint) || t == typeof(ulong)
        || t == typeof(float) || t == typeof(double) || t == typeof(decimal)
        || t == typeof(DateTime) || t == typeof(DateTimeOffset) || t == typeof(DateOnly) || t == typeof(TimeOnly)
        || t == typeof(TimeSpan) || t == typeof(Guid) || t == typeof(byte[]);

    private static bool Set(object? parsed, out object? value, out string? error, string text, string friendly)
    {
        value = parsed;
        error = parsed is null ? UiText.Format("results.invalidValue", text, friendly) : null;
        return parsed is not null;
    }

    private static bool Integer<T>(string s, string text, string typeName, out object? value, out string? error)
        where T : INumberBase<T>
    {
        if (T.TryParse(s, NumberStyles.Integer, Invariant, out var parsed))
        {
            value = parsed;
            error = null;
            return true;
        }

        value = null;
        error = BigInteger.TryParse(s, NumberStyles.Integer, Invariant, out _)
            ? UiText.Format("results.outOfRange", text, typeName)
            : UiText.Format("results.invalidInteger", text);
        return false;
    }

    private static object? ParseBool(string s) => s.ToLowerInvariant() switch
    {
        "true" or "1" or "yes" => true,
        "false" or "0" or "no" => false,
        _ => null,
    };

    private static byte[]? ParseHex(string s)
    {
        if (!s.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        try
        {
            return Convert.FromHexString(s.AsSpan(2));
        }
        catch (FormatException)
        {
            return null;
        }
    }
}
