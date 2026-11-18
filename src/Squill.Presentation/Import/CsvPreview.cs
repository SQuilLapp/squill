namespace Squill.Presentation.Import;

/// <summary>The first rows of a CSV file, as step 1 of the import wizard shows them.</summary>
/// <param name="ColumnNames">The header names, or <c>col1..colN</c> when the file has no header.</param>
/// <param name="Sample">The first data rows.</param>
/// <param name="TotalRows">The data row count; <see langword="null"/> until <see cref="CountRowsAsync"/> lands.</param>
/// <param name="FileBytes">The file size in bytes.</param>
public sealed record CsvPreview(
    IReadOnlyList<string> ColumnNames,
    IReadOnlyList<string?[]> Sample,
    long? TotalRows,
    long FileBytes)
{
    /// <summary>Reads the header and a sample of a CSV file.</summary>
    /// <param name="path">The file to read.</param>
    /// <param name="options">How to decode and split it.</param>
    /// <param name="sampleRows">How many data rows to keep.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The preview; an empty file gives no column and no row.</returns>
    public static async Task<CsvPreview> ReadAsync(
        string path,
        CsvOptions options,
        int sampleRows = 1000,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);
        ArgumentNullException.ThrowIfNull(options);
        await using var stream = OpenRead(path);
        List<string?[]> sample = [];
        string[]? header = null;
        await foreach (var row in CsvReader.ReadRowsAsync(stream, options, cancellationToken).ConfigureAwait(false))
        {
            if (header is null)
            {
                header = options.HasHeader ? row.Fields : [.. Enumerable.Range(1, row.Fields.Length).Select(n => "col" + n)];
                if (options.HasHeader)
                {
                    continue;
                }
            }

            if (sample.Count >= sampleRows)
            {
                break;
            }

            sample.Add(row.Fields);
        }

        return new CsvPreview(header ?? [], sample, null, new FileInfo(path).Length);
    }

    /// <summary>Counts the data rows of a CSV file with one streaming pass.</summary>
    /// <param name="path">The file to read.</param>
    /// <param name="options">How to decode and split it.</param>
    /// <param name="cancellationToken">Cancels the count.</param>
    /// <returns>The number of records, the header excluded.</returns>
    public static async Task<long> CountRowsAsync(string path, CsvOptions options, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);
        ArgumentNullException.ThrowIfNull(options);
        await using var stream = OpenRead(path);
        long count = 0;
        await foreach (var unused in CsvReader.ReadRowsAsync(stream, options, cancellationToken).ConfigureAwait(false))
        {
            count++;
        }

        return options.HasHeader && count > 0 ? count - 1 : count;
    }

    private static FileStream OpenRead(string path) =>
        new(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, 64 * 1024, FileOptions.Asynchronous | FileOptions.SequentialScan);
}
