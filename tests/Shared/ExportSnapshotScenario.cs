using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using Squill.Core;
using Squill.Presentation.Results;

namespace Squill.Data.Tests.Scenarios;

/// <summary>One captured result column: what the driver returned for the single row, in a form that reads back into the same CLR value.</summary>
/// <param name="Name">Column name.</param>
/// <param name="DataType">Driver type name.</param>
/// <param name="Clr">CLR type name of the value, <c>null</c> for SQL NULL, or <c>opaque</c> for a type replayed as its invariant text.</param>
/// <param name="Text">Invariant text of a scalar.</param>
/// <param name="Items">Invariant text of each element of a one-dimensional array.</param>
public sealed record CapturedColumn(string Name, string DataType, string Clr, string? Text, string?[]? Items);

/// <summary>The captured all-types row of one engine.</summary>
/// <param name="Columns">The columns in result order.</param>
public sealed record ExportSnapshot(CapturedColumn[] Columns);

/// <summary>JSON context of the export snapshots (test code only).</summary>
[JsonSerializable(typeof(ExportSnapshot))]
[JsonSourceGenerationOptions(WriteIndented = true)]
internal sealed partial class ExportSnapshotJsonContext : JsonSerializerContext;

/// <summary>Captures the rows the engines return (written by the Docker-gated driver tests) and replays them in the Presentation tests.</summary>
public static class ExportSnapshotScenario
{
    private const int MaxCapturedBytes = 4096;

    /// <summary>Encodes one cell.</summary>
    /// <param name="column">The column.</param>
    /// <param name="value">The driver value.</param>
    /// <returns>The captured column, or <see langword="null"/> for a binary value over 4 KB (too large to commit).</returns>
    public static CapturedColumn? Capture(ColumnInfo column, object? value) => value switch
    {
        null => new(column.Name, column.DataTypeName, "null", null, null),
        byte[] { Length: > MaxCapturedBytes } => null,
        byte[] b => new(column.Name, column.DataTypeName, "Byte[]", Encode(b), null),
        Array a when a.GetType().GetElementType() is { } e && Scalar(e) => new(column.Name, column.DataTypeName, value.GetType().Name, null, [.. a.Cast<object?>().Select(Encode)]),
        _ when Scalar(value.GetType()) => new(column.Name, column.DataTypeName, value.GetType().Name, Encode(value), null),
        _ => new(column.Name, column.DataTypeName, "opaque", CellFormatter.Format(value, column), null),
    };

    /// <summary>Writes the snapshot of <paramref name="dialect"/> into the Presentation test resources when <c>SQUILL_UPDATE_SNAPSHOTS=1</c>.</summary>
    /// <param name="dialect">The engine.</param>
    /// <param name="columns">Everything captured by the test.</param>
    public static void WriteIfRequested(DatabaseDialect dialect, IEnumerable<CapturedColumn> columns)
    {
        if (Environment.GetEnvironmentVariable("SQUILL_UPDATE_SNAPSHOTS") != "1")
            return;
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Squill.slnx")))
            dir = dir.Parent;
        var target = Path.Combine(dir?.FullName ?? throw new InvalidOperationException("Squill.slnx not found."), "tests", "Squill.Presentation.Tests", "Results", "Snapshots");
        Directory.CreateDirectory(target);
        File.WriteAllText(Path.Combine(target, $"export-{dialect}.json"), JsonSerializer.Serialize(new ExportSnapshot([.. columns]), ExportSnapshotJsonContext.Default.ExportSnapshot));
    }

    /// <summary>Reads a committed snapshot back into columns and the row of values.</summary>
    /// <param name="path">JSON file written by <see cref="WriteIfRequested"/>.</param>
    /// <returns>The columns and the values.</returns>
    public static (ColumnInfo[] Columns, object?[] Row) Load(string path)
    {
        var snapshot = JsonSerializer.Deserialize(File.ReadAllText(path), ExportSnapshotJsonContext.Default.ExportSnapshot)!;
        var columns = snapshot.Columns.Select((c, i) => new ColumnInfo(c.Name, c.DataType, typeof(object), true, i)).ToArray();
        return (columns, [.. snapshot.Columns.Select(Decode)]);
    }

    private static bool Scalar(Type t) =>
        t == typeof(string) || t == typeof(bool) || t == typeof(byte) || t == typeof(sbyte) || t == typeof(short) || t == typeof(int) || t == typeof(long)
        || t == typeof(decimal) || t == typeof(float) || t == typeof(double) || t == typeof(Guid) || t == typeof(byte[]) || t == typeof(DateTime)
        || t == typeof(DateTimeOffset) || t == typeof(DateOnly) || t == typeof(TimeOnly) || t == typeof(TimeSpan);

    private static string? Encode(object? v) => v switch
    {
        null => null,
        byte[] b => Convert.ToHexString(b),
        DateTime d => d.ToString("o", CultureInfo.InvariantCulture),
        DateTimeOffset d => d.ToString("o", CultureInfo.InvariantCulture),
        DateOnly d => d.ToString("o", CultureInfo.InvariantCulture),
        TimeOnly t => t.ToString("o", CultureInfo.InvariantCulture),
        TimeSpan t => t.ToString("c", CultureInfo.InvariantCulture),
        float f => f.ToString("R", CultureInfo.InvariantCulture),
        double f => f.ToString("R", CultureInfo.InvariantCulture),
        bool b => b ? "true" : "false",
        IFormattable f => f.ToString(null, CultureInfo.InvariantCulture),
        _ => v.ToString(),
    };

    private static object? Decode(CapturedColumn c)
    {
        if (c.Items is { } items)
        {
            var values = items.Select(i => DecodeScalar(c.Clr[..^2], i));
            return c.Clr switch
            {
                "Int16[]" => values.Cast<short>().ToArray(),
                "Int32[]" => values.Cast<int>().ToArray(),
                "Int64[]" => values.Cast<long>().ToArray(),
                "Boolean[]" => values.Cast<bool>().ToArray(),
                "Double[]" => values.Cast<double>().ToArray(),
                "Decimal[]" => values.Cast<decimal>().ToArray(),
                "Guid[]" => values.Cast<Guid>().ToArray(),
                "String[]" => values.Cast<string?>().ToArray(),
                _ => throw new InvalidOperationException("Unknown captured array " + c.Clr),
            };
        }

        return c.Clr switch
        {
            "null" => null,
            "opaque" => c.Text,
            _ => DecodeScalar(c.Clr, c.Text),
        };
    }

    private static object? DecodeScalar(string clr, string? t)
    {
        if (t is null)
            return null;
        var ic = CultureInfo.InvariantCulture;
        return clr switch
        {
            "String" => t,
            "Boolean" => t == "true",
            "Byte" => byte.Parse(t, ic),
            "SByte" => sbyte.Parse(t, ic),
            "Int16" => short.Parse(t, ic),
            "Int32" => int.Parse(t, ic),
            "Int64" => long.Parse(t, ic),
            "Decimal" => decimal.Parse(t, ic),
            "Single" => float.Parse(t, ic),
            "Double" => double.Parse(t, ic),
            "Guid" => Guid.Parse(t),
            "Byte[]" => Convert.FromHexString(t),
            "DateTime" => DateTime.Parse(t, ic, DateTimeStyles.RoundtripKind),
            "DateTimeOffset" => DateTimeOffset.Parse(t, ic),
            "DateOnly" => DateOnly.Parse(t, ic),
            "TimeOnly" => TimeOnly.Parse(t, ic),
            "TimeSpan" => TimeSpan.Parse(t, ic),
            _ => throw new InvalidOperationException("Unknown captured type " + clr),
        };
    }
}
