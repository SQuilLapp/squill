namespace Squill.Platform.Storage;

/// <summary>Persists which tables the explorer hides, for each connection profile.</summary>
public interface ITableFilterStore
{
    /// <summary>Loads the saved filter.</summary>
    /// <param name="profileId">Connection profile identifier.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The filter, or <see langword="null"/> when none is saved or the file is unreadable.</returns>
    ValueTask<TableFilter?> LoadAsync(Guid profileId, CancellationToken ct);

    /// <summary>Saves a filter, replacing any previous one.</summary>
    /// <param name="profileId">Connection profile identifier.</param>
    /// <param name="filter">Filter to save.</param>
    /// <param name="ct">Cancellation token.</param>
    ValueTask SaveAsync(Guid profileId, TableFilter filter, CancellationToken ct);
}
