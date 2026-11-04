using System.Globalization;

namespace Squill.Data.Oracle;

/// <summary>Converts CLR values to and from the typed cells of the bridge protocol.</summary>
internal static class WireValue
{
    private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;

    /// <summary>Encodes a value; <c>null</c> and <see cref="DBNull"/> become <c>null</c>, unknown types their invariant text.</summary>
    public static WireCell? Encode(object? value) => value switch
    {
        null or DBNull => null,
        string s => new("s", s),
        bool b => new("b", b ? "1" : "0"),
        byte or sbyte or short or ushort or int or uint or long => new("i", Convert.ToString(value, Invariant)!),
        ulong or decimal => new("d", Convert.ToString(value, Invariant)!),
        float or double => new("f", Convert.ToDouble(value, Invariant).ToString("R", Invariant)),
        DateTime t => new("t", t.ToString("O", Invariant)),
        DateTimeOffset o => new("o", o.ToString("O", Invariant)),
        byte[] x => new("x", Convert.ToBase64String(x)),
        _ => new("s", Convert.ToString(value, Invariant) ?? ""),
    };

    /// <summary>Decodes a cell; <c>null</c> stays <c>null</c>, an unknown kind throws <see cref="FormatException"/>.</summary>
    public static object? Decode(WireCell? cell) => cell is null ? null : cell.K switch
    {
        "s" => cell.V,
        "i" => long.Parse(cell.V, NumberStyles.Integer, Invariant),
        "d" => decimal.Parse(cell.V, NumberStyles.Float, Invariant),
        "f" => double.Parse(cell.V, NumberStyles.Float, Invariant),
        "b" => cell.V == "1",
        "t" => DateTime.Parse(cell.V, Invariant, DateTimeStyles.RoundtripKind),
        "o" => DateTimeOffset.Parse(cell.V, Invariant, DateTimeStyles.RoundtripKind),
        "x" => Convert.FromBase64String(cell.V),
        _ => throw new FormatException($"Unknown wire kind '{cell.K}'."),
    };

    /// <summary>The CLR type a column of the given wire kind is read as.</summary>
    public static Type ClrType(string kind) => kind switch
    {
        "i" => typeof(long),
        "d" => typeof(decimal),
        "f" => typeof(double),
        "b" => typeof(bool),
        "t" => typeof(DateTime),
        "o" => typeof(DateTimeOffset),
        "x" => typeof(byte[]),
        _ => typeof(string),
    };

    /// <summary>The wire kind for a driver field type, used as the column hint.</summary>
    public static string KindOf(Type type) =>
        type == typeof(string) ? "s"
        : type == typeof(bool) ? "b"
        : type == typeof(decimal) ? "d"
        : type == typeof(float) || type == typeof(double) ? "f"
        : type == typeof(byte) || type == typeof(short) || type == typeof(int) || type == typeof(long) ? "i"
        : type == typeof(DateTime) ? "t"
        : type == typeof(DateTimeOffset) ? "o"
        : type == typeof(byte[]) ? "x"
        : "s";
}
