namespace Squill.Presentation.Results;

/// <summary>Writes a complete result set to a file format.</summary>
public interface IResultExporter
{
    /// <summary>Gets the file extension without the leading dot.</summary>
    string FileExtension { get; }

    /// <summary>Writes every row of <paramref name="source"/> to <paramref name="destination"/>.</summary>
    /// <param name="source">The result to export; it must be complete.</param>
    /// <param name="destination">The stream to write to; left open.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A task that completes when everything is written and flushed.</returns>
    /// <exception cref="InvalidOperationException"><paramref name="source"/> is not complete.</exception>
    ValueTask ExportAsync(IGridDataSource source, Stream destination, CancellationToken ct);
}
