namespace Squill.Platform.Storage;

/// <summary>Persists executed queries, newest last.</summary>
public interface IQueryHistoryStore
{
    /// <summary>Appends an entry, dropping the oldest ones beyond the cap of 5,000.</summary>
    /// <param name="e">Entry to add.</param>
    /// <param name="ct">Cancellation token.</param>
    ValueTask AppendAsync(HistoryEntry e, CancellationToken ct);

    /// <summary>Lists the most recent entries.</summary>
    /// <param name="max">Maximum number of entries to return.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>At most <paramref name="max"/> entries, oldest first.</returns>
    ValueTask<IReadOnlyList<HistoryEntry>> ListAsync(int max, CancellationToken ct);

    /// <summary>Deletes every entry.</summary>
    /// <param name="ct">Cancellation token.</param>
    ValueTask ClearAsync(CancellationToken ct);
}
