using System.Data.Common;

namespace Squill.Data;

/// <summary>Row-reading loop shared by the provider result readers.</summary>
public static class ResultRows
{
    /// <summary>Reads up to <paramref name="maxRows"/> rows from an open driver reader.</summary>
    /// <typeparam name="TReader">The driver's reader type, so <paramref name="readValue"/> can use its typed members.</typeparam>
    /// <param name="reader">The driver reader positioned in a result set.</param>
    /// <param name="columnCount">Number of columns of the current result set.</param>
    /// <param name="maxRows">Largest batch size, greater than zero.</param>
    /// <param name="alreadyFinished">Whether the result set was already read to its end, in which case nothing is read.</param>
    /// <param name="readValue">Reads the non-null value of one column.</param>
    /// <param name="cancellationToken">Cancels between rows and inside the driver.</param>
    /// <returns>The rows (null when there are none) and whether the end of the result set was reached.</returns>
    public static async ValueTask<(object?[][]? Rows, bool Finished)> ReadAsync<TReader>(
        TReader reader, int columnCount, int maxRows, bool alreadyFinished, Func<TReader, int, object?> readValue, CancellationToken cancellationToken)
        where TReader : DbDataReader
    {
        ArgumentNullException.ThrowIfNull(reader);
        ArgumentNullException.ThrowIfNull(readValue);
        var finished = alreadyFinished;
        var rows = new List<object?[]>();
        while (rows.Count < maxRows)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (finished || !await reader.ReadAsync(cancellationToken))
            {
                finished = true;
                break;
            }
            var row = new object?[columnCount];
            for (var i = 0; i < row.Length; i++)
                row[i] = reader.IsDBNull(i) ? null : readValue(reader, i);
            rows.Add(row);
        }
        return (rows.Count == 0 ? null : [.. rows], finished);
    }
}
