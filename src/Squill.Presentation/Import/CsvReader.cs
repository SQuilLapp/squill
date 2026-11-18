using System.Runtime.CompilerServices;
using System.Text;

namespace Squill.Presentation.Import;

/// <summary>One parsed CSV record.</summary>
/// <param name="Fields">The fields; an empty field is the empty string (the mapping layer decides what it means).</param>
/// <param name="LineNumber">The 1-based physical line where the record starts.</param>
/// <param name="IsRagged">Whether the field count differs from the first record of the file.</param>
public sealed record CsvRow(string[] Fields, long LineNumber, bool IsRagged);

/// <summary>Forward-only RFC 4180 reader that never loads the whole file.</summary>
public static class CsvReader
{
    /// <summary>Reads the records of a CSV stream, the header included.</summary>
    /// <param name="stream">The stream to read; it is left open.</param>
    /// <param name="options">Encoding, delimiter and quote (a BOM overrides the encoding).</param>
    /// <param name="cancellationToken">Cancels the enumeration.</param>
    /// <returns>
    /// The records in file order. Quoted fields may hold the delimiter, doubled quotes and line breaks; <c>\r\n</c> and
    /// <c>\n</c> end a record; lines with no character at all are skipped.
    /// </returns>
    public static async IAsyncEnumerable<CsvRow> ReadRowsAsync(
        Stream stream,
        CsvOptions options,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(stream);
        ArgumentNullException.ThrowIfNull(options);
        using var reader = new StreamReader(stream, options.Encoding, true, 64 * 1024, leaveOpen: true);
        var buffer = new char[16 * 1024];
        var field = new StringBuilder();
        List<string> fields = [];
        long line = 1;
        var rowLine = 1L;
        var expected = -1;
        var inQuotes = false;
        var afterQuote = false; // a closing quote was just read: a second quote makes it a literal one
        var hasContent = false;
        var atFieldStart = true;
        int read;
        while ((read = await reader.ReadAsync(buffer.AsMemory(), cancellationToken).ConfigureAwait(false)) > 0)
        {
            for (var i = 0; i < read; i++)
            {
                var c = buffer[i];
                if (inQuotes)
                {
                    if (c == options.Quote)
                    {
                        (inQuotes, afterQuote) = (false, true);
                    }
                    else
                    {
                        field.Append(c);
                        if (c == '\n')
                        {
                            line++;
                        }
                    }

                    continue;
                }

                if (afterQuote)
                {
                    afterQuote = false;
                    if (c == options.Quote)
                    {
                        field.Append(c);
                        inQuotes = true;
                        continue;
                    }
                }

                if (c == '\n')
                {
                    line++;
                    if (hasContent)
                    {
                        fields.Add(field.ToString());
                        yield return Finish(fields, rowLine, ref expected);
                        fields = [];
                        field.Clear();
                    }

                    (hasContent, atFieldStart, rowLine) = (false, true, line);
                }
                else if (c == '\r')
                {
                    // dropped outside quotes: \r\n ends the record at the \n.
                }
                else if (c == options.Delimiter)
                {
                    fields.Add(field.ToString());
                    field.Clear();
                    (hasContent, atFieldStart) = (true, true);
                }
                else if (c == options.Quote && atFieldStart)
                {
                    (inQuotes, hasContent, atFieldStart) = (true, true, false);
                }
                else
                {
                    field.Append(c);
                    (hasContent, atFieldStart) = (true, false);
                }
            }
        }

        if (hasContent)
        {
            fields.Add(field.ToString());
            yield return Finish(fields, rowLine, ref expected);
        }
    }

    private static CsvRow Finish(List<string> fields, long rowLine, ref int expected)
    {
        if (expected < 0)
        {
            expected = fields.Count;
        }

        return new CsvRow([.. fields], rowLine, fields.Count != expected);
    }
}
