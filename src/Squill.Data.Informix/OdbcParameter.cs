using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;

namespace Squill.Data.Informix;

/// <summary>One input value for <c>SQLBindParameter</c>: ODBC types plus buffers that never move, so they stay valid until the statement has run.</summary>
internal sealed class OdbcParameter
{
    internal const short CWChar = -8, CBinary = -2, SqlVarchar = 12, SqlVarbinary = -3, SqlInteger = 4, SqlSmallint = 5, SqlBigint = -5, SqlDecimal = 3, SqlReal = 7, SqlDouble = 8, SqlDate = 91, SqlTimestamp = 93;
    private const long NullData = -1;

    private OdbcParameter(short cType, short sqlType, byte[] data, int length, int columnSize = 0, int digits = 0)
    {
        CType = cType;
        SqlType = sqlType;
        ColumnSize = Math.Max(columnSize, 1);
        DecimalDigits = digits;
        Data = GC.AllocateArray<byte>(Math.Max(data.Length, 2), pinned: true);
        data.CopyTo(Data, 0);
        Indicator = GC.AllocateArray<long>(1, pinned: true);
        Indicator[0] = length;
    }

    /// <summary>Gets the C type of the buffer (<c>SQL_C_WCHAR</c> or <c>SQL_C_BINARY</c>).</summary>
    public short CType { get; }

    /// <summary>Gets the ODBC SQL type the server converts the value to.</summary>
    public short SqlType { get; }

    /// <summary>Gets the column size: characters for text, digits for numbers, bytes for binary.</summary>
    public int ColumnSize { get; }

    /// <summary>Gets the number of digits after the decimal point, or the fraction digits of a timestamp.</summary>
    public int DecimalDigits { get; }

    /// <summary>Gets the pinned value buffer.</summary>
    public byte[] Data { get; }

    /// <summary>Gets the pinned length indicator: the byte length of the value, or -1 for SQL NULL.</summary>
    public long[] Indicator { get; }

    /// <summary>Gets the address of <see cref="Data"/>.</summary>
    public nint DataPointer => Marshal.UnsafeAddrOfPinnedArrayElement(Data, 0);

    /// <summary>Gets the address of <see cref="Indicator"/>.</summary>
    public nint IndicatorPointer => Marshal.UnsafeAddrOfPinnedArrayElement(Indicator, 0);

    /// <summary>Gets the value as UTF-16 text, or <see langword="null"/> for SQL NULL and binary values.</summary>
    public string? Text => CType == CWChar && Indicator[0] != NullData ? Encoding.Unicode.GetString(Data, 0, (int)Indicator[0]) : null;

    /// <summary>Builds the parameter for a CLR value: numbers, dates and booleans travel as invariant text and are converted by the server to the declared SQL type.</summary>
    /// <param name="value">The value; <see langword="null"/> is SQL NULL.</param>
    /// <returns>The pinned parameter.</returns>
    public static OdbcParameter From(object? value)
    {
        var inv = CultureInfo.InvariantCulture;
        return value switch
        {
            null => new OdbcParameter(CWChar, SqlVarchar, [], (int)NullData),
            byte[] bytes => new OdbcParameter(CBinary, SqlVarbinary, bytes, bytes.Length, bytes.Length),
            string s => FromText(s, SqlVarchar, s.Length),
            bool b => FromText(b ? "t" : "f", SqlVarchar, 1),
            int i => FromText(i.ToString(inv), SqlInteger, 10),
            long l => FromText(l.ToString(inv), SqlBigint, 19),
            short sh => FromText(sh.ToString(inv), SqlSmallint, 5),
            byte by => FromText(by.ToString(inv), SqlSmallint, 5),
            double d => FromText(d.ToString("R", inv), SqlDouble, 15),
            float f => FromText(f.ToString("R", inv), SqlReal, 7),
            decimal m => FromDecimal(m.ToString(inv)),
            DateTime t => FromText(t.ToString("yyyy-MM-dd HH:mm:ss.fffff", inv), SqlTimestamp, 25, 5),
            DateOnly d => FromText(d.ToString("yyyy-MM-dd", inv), SqlDate, 10),
            IFormattable f => FromText(f.ToString(null, inv), SqlVarchar, 0),
            _ => FromText(value.ToString() ?? "", SqlVarchar, 0),
        };
    }

    private static OdbcParameter FromDecimal(string text)
    {
        var dot = text.IndexOf('.', StringComparison.Ordinal);
        var digits = text.Count(char.IsAsciiDigit);
        return FromText(text, SqlDecimal, digits, dot < 0 ? 0 : text.Length - dot - 1);
    }

    private static OdbcParameter FromText(string text, short sqlType, int size, int digits = 0) =>
        new(CWChar, sqlType, Encoding.Unicode.GetBytes(text), text.Length * sizeof(char), size == 0 ? text.Length : size, digits);
}
