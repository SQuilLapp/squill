using System.Globalization;

namespace Squill.Data.Informix;

/// <summary>Maps ODBC SQL type codes to a type name and a CLR type, and converts the text the driver returns to a value.</summary>
internal static class OdbcValue
{
    private const int MaxDecimalDigits = 28;

    /// <summary>Tells whether a column is read as bytes (<c>SQL_C_BINARY</c>) instead of text.</summary>
    /// <param name="sqlType">The ODBC SQL type code.</param>
    /// <returns><see langword="true"/> for <c>BINARY</c>, <c>VARBINARY</c>, <c>LONGVARBINARY</c> and the DB2 <c>BLOB</c>.</returns>
    public static bool IsBinary(short sqlType) => sqlType is -2 or -3 or -4 or -98;

    /// <summary>Names a type code and gives the CLR type its values have.</summary>
    /// <param name="sqlType">The ODBC SQL type code.</param>
    /// <returns>The name for display and the CLR type; unknown codes are shown as text.</returns>
    public static (string Name, Type Clr) Describe(short sqlType) => sqlType switch
    {
        4 => ("INTEGER", typeof(int)),
        5 => ("SMALLINT", typeof(short)),
        -6 => ("TINYINT", typeof(short)),
        -5 => ("BIGINT", typeof(long)),
        2 => ("NUMERIC", typeof(decimal)),
        3 => ("DECIMAL", typeof(decimal)),
        6 => ("FLOAT", typeof(double)),
        8 => ("DOUBLE", typeof(double)),
        7 => ("REAL", typeof(float)),
        -7 or 16 => ("BOOLEAN", typeof(bool)),
        91 => ("DATE", typeof(DateTime)),
        93 => ("DATETIME", typeof(DateTime)),
        92 => ("TIME", typeof(TimeSpan)),
        -2 => ("BINARY", typeof(byte[])),
        -3 => ("VARBINARY", typeof(byte[])),
        -4 => ("LONGVARBINARY", typeof(byte[])),
        -98 => ("BLOB", typeof(byte[])), // the driver reports a BYTE column as BLOB too,
        1 => ("CHAR", typeof(string)),
        12 => ("VARCHAR", typeof(string)),
        -1 => ("LVARCHAR", typeof(string)),
        -8 => ("NCHAR", typeof(string)),
        -9 => ("NVARCHAR", typeof(string)),
        -10 or 40 or -99 => ("TEXT", typeof(string)),
        _ => ("TYPE " + sqlType.ToString(CultureInfo.InvariantCulture), typeof(string)),
    };

    /// <summary>Converts the text of a non-binary column; a value that does not fit its type (a 32-digit <c>DECIMAL</c>, an unusual <c>DATETIME</c> qualifier) stays text.</summary>
    /// <param name="sqlType">The ODBC SQL type code.</param>
    /// <param name="text">The text from the driver, <see langword="null"/> for SQL NULL.</param>
    /// <returns>The value, or the text itself when it cannot be converted.</returns>
    public static object? Convert(short sqlType, string? text)
    {
        if (text is null)
            return null;
        // The driver writes numbers with the decimal separator of the process locale (a comma on a French machine) and never groups digits.
        if (sqlType is 2 or 3 or 6 or 7 or 8)
            text = text.Replace(',', '.');
        var inv = CultureInfo.InvariantCulture;
        switch (sqlType)
        {
            case 4 when int.TryParse(text, NumberStyles.Integer, inv, out var i):
                return i;
            case 5 or -6 when short.TryParse(text, NumberStyles.Integer, inv, out var s):
                return s;
            case -5 when long.TryParse(text, NumberStyles.Integer, inv, out var l):
                return l;
            case 2 or 3 when text.Count(char.IsAsciiDigit) <= MaxDecimalDigits && decimal.TryParse(text, NumberStyles.Number | NumberStyles.AllowExponent, inv, out var m):
                return m;
            case 6 or 8 when double.TryParse(text, NumberStyles.Float, inv, out var d):
                return d;
            case 7 when float.TryParse(text, NumberStyles.Float, inv, out var f):
                return f;
            case -7 or 16:
                return text is "1" or "t" or "T" or "true";
            case 91 or 93 when DateTime.TryParse(text, inv, DateTimeStyles.None, out var t):
                return t;
            case 92 when TimeSpan.TryParse(text, inv, out var ts):
                return ts;
            default:
                return text;
        }
    }
}
