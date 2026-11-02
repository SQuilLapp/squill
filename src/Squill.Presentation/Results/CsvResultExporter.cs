using System.Text;

namespace Squill.Presentation.Results;

/// <summary>Exports a result as RFC 4180 CSV: UTF-8 without BOM, <c>\r\n</c> line ends, header row.</summary>
public sealed class CsvResultExporter : IResultExporter
{
    private static readonly char[] SpecialChars = [',', '"', '\r', '\n'];

    /// <inheritdoc/>
    public string FileExtension => "csv";

    /// <inheritdoc/>
    public async ValueTask ExportAsync(IGridDataSource source, Stream destination, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(destination);
        if (!source.IsComplete)
            throw new InvalidOperationException("The result is still loading and cannot be exported.");

        var columns = source.Columns;
        var rows = source.RowCount;
        var writer = new StreamWriter(destination, new UTF8Encoding(false), 1 << 16, leaveOpen: true);
        var line = new StringBuilder();
        await using (writer.ConfigureAwait(false))
        {
            for (var c = 0; c < columns.Count; c++)
            {
                if (c > 0)
                    line.Append(',');
                AppendField(line, columns[c].Name);
            }

            line.Append("\r\n");
            for (var r = 0L; r < rows; r++)
            {
                if ((r & 1023) == 0)
                    ct.ThrowIfCancellationRequested();
                await writer.WriteAsync(line, ct).ConfigureAwait(false);
                line.Clear();
                for (var c = 0; c < columns.Count; c++)
                {
                    if (c > 0)
                        line.Append(',');
                    AppendField(line, CellFormatter.Format(source.GetCell(r, c), columns[c]));
                }

                line.Append("\r\n");
            }

            await writer.WriteAsync(line, ct).ConfigureAwait(false);
            await writer.FlushAsync(ct).ConfigureAwait(false);
        }
    }

    private static void AppendField(StringBuilder line, string? text)
    {
        if (string.IsNullOrEmpty(text))
            return;
        if (text.AsSpan().IndexOfAny(SpecialChars) < 0)
        {
            line.Append(text);
            return;
        }

        line.Append('"').Append(text.Replace("\"", "\"\"", StringComparison.Ordinal)).Append('"');
    }
}
