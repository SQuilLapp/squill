using System.Globalization;
using System.Numerics;
using Squill.Core;

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
        if (type == typeof(float)) { return Set(float.TryParse(s, NumberStyles.Float, Invariant, out var f) ? f : null, out value, out error, text, "number"); }
        if (type == typeof(double)) { return Set(double.TryParse(s, NumberStyles.Float, Invariant, out var d) ? d : null, out value, out error, text, "number"); }
        if (type == typeof(decimal)) { return Set(decimal.TryParse(s, NumberStyles.Float, Invariant, out var m) ? m : null, out value, out error, text, "number"); }
        if (type == typeof(DateTime))
        {
            return Set(DateTime.TryParseExact(s, DateTimeFormats, Invariant, DateTimeStyles.RoundtripKind, out var dt) ? dt : null, out value, out error, text, "date and time");
        }

        if (type == typeof(DateTimeOffset))
        {
            return Set(DateTimeOffset.TryParseExact(s, DateTimeOffsetFormats, Invariant, DateTimeStyles.AssumeUniversal, out var dto) ? dto : null, out value, out error, text, "date and time");
        }

        if (type == typeof(DateOnly)) { return Set(DateOnly.TryParseExact(s, DateFormats, Invariant, DateTimeStyles.None, out var da) ? da : null, out value, out error, text, "date"); }
        if (type == typeof(TimeOnly)) { return Set(TimeOnly.TryParse(s, Invariant, DateTimeStyles.None, out var to) ? to : null, out value, out error, text, "time"); }
        if (type == typeof(TimeSpan)) { return Set(TimeSpan.TryParse(s, Invariant, out var ts) ? ts : null, out value, out error, text, "time"); }
        if (type == typeof(Guid)) { return Set(Guid.TryParse(s, out var g) ? g : null, out value, out error, text, "GUID"); }
        if (type == typeof(byte[])) { return Set(ParseHex(s), out value, out error, text, "binary (0x...)"); }

        value = text;
        return true;
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
        error = parsed is null ? $"\"{text}\" is not a valid {friendly} value" : null;
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
            ? $"\"{text}\" is out of range for {typeName}"
            : $"\"{text}\" is not a valid integer value";
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
