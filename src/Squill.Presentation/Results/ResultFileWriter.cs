namespace Squill.Presentation.Results;

/// <summary>Writes an export to a file without ever leaving a partial one behind.</summary>
public static class ResultFileWriter
{
    /// <summary>Exports <paramref name="source"/> to a temporary file next to <paramref name="path"/>, then moves it over <paramref name="path"/>.</summary>
    /// <param name="exporter">The format to write.</param>
    /// <param name="source">The complete result to export.</param>
    /// <param name="path">The destination; an existing file is replaced only when the export succeeds.</param>
    /// <param name="ct">Cancellation token; on cancellation or failure the temporary file is deleted and <paramref name="path"/> is untouched.</param>
    /// <returns>A task that completes when the file is in place.</returns>
    public static async Task WriteAsync(IResultExporter exporter, IGridDataSource source, string path, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(exporter);
        ArgumentNullException.ThrowIfNull(source);
        ArgumentException.ThrowIfNullOrEmpty(path);
        var temp = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            var file = new FileStream(temp, FileMode.CreateNew, FileAccess.Write, FileShare.None, 1 << 16, FileOptions.Asynchronous);
            await using (file.ConfigureAwait(false))
                await exporter.ExportAsync(source, file, ct).ConfigureAwait(false);
            File.Move(temp, path, overwrite: true);
        }
        catch
        {
            File.Delete(temp);
            throw;
        }
    }
}
