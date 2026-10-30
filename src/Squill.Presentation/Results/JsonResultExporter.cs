using System.Globalization;
using System.Text.Json;
using Squill.Core;

namespace Squill.Presentation.Results;

/// <summary>Exports a result as an indented JSON array of objects, one per row.</summary>
public sealed class JsonResultExporter : IResultExporter
{
    /// <inheritdoc/>
    public string FileExtension => "json";

    /// <inheritdoc/>
    public async ValueTask ExportAsync(IGridDataSource source, Stream destination, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(destination);
        if (!source.IsComplete)
            throw new InvalidOperationException("The result is still loading and cannot be exported.");

        var columns = source.Columns;
        var names = UniqueNames(columns);
        var rows = source.RowCount;
        var writer = new Utf8JsonWriter(destination, new JsonWriterOptions { Indented = true });
        await using (writer.ConfigureAwait(false))
        {
            writer.WriteStartArray();
            for (var r = 0L; r < rows; r++)
            {
                if ((r & 1023) == 0)
                    ct.ThrowIfCancellationRequested();
                writer.WriteStartObject();
                for (var c = 0; c < columns.Count; c++)
                {
                    writer.WritePropertyName(names[c]);
                    WriteValue(writer, source.GetCell(r, c), columns[c]);
                }

                writer.WriteEndObject();
                if (writer.BytesPending > 1 << 16)
                    await writer.FlushAsync(ct).ConfigureAwait(false);
            }

            writer.WriteEndArray();
            await writer.FlushAsync(ct).ConfigureAwait(false);
        }
    }

    private static string[] UniqueNames(IReadOnlyList<ColumnInfo> columns)
    {
        var used = new HashSet<string>(StringComparer.Ordinal);
        var result = new string[columns.Count];
        for (var i = 0; i < result.Length; i++)
        {
            var candidate = columns[i].Name;
            for (var n = 2; !used.Add(candidate); n++)
                candidate = columns[i].Name + "_" + n.ToString(CultureInfo.InvariantCulture);
            result[i] = candidate;
        }

        return result;
    }

    private static void WriteValue(Utf8JsonWriter writer, object? value, ColumnInfo column)
    {
        switch (value)
        {
            case null: writer.WriteNullValue(); break;
            case bool b: writer.WriteBooleanValue(b); break;
            case sbyte v: writer.WriteNumberValue(v); break;
            case byte v: writer.WriteNumberValue(v); break;
            case short v: writer.WriteNumberValue(v); break;
            case ushort v: writer.WriteNumberValue(v); break;
            case int v: writer.WriteNumberValue(v); break;
            case uint v: writer.WriteNumberValue(v); break;
            case long v: writer.WriteNumberValue(v); break;
            case ulong v: writer.WriteNumberValue(v); break;
            case decimal v: writer.WriteNumberValue(v); break;
            case float v when float.IsFinite(v): writer.WriteNumberValue(v); break;
            case double v when double.IsFinite(v): writer.WriteNumberValue(v); break;
            default: writer.WriteStringValue(CellFormatter.Format(value, column)); break;
        }
    }
}
