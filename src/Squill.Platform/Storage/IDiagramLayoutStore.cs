namespace Squill.Platform.Storage;

/// <summary>Persists the ER diagram layout of each connection profile.</summary>
public interface IDiagramLayoutStore
{
    /// <summary>Loads the saved layout.</summary>
    /// <param name="profileId">Connection profile identifier.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The layout, or <see langword="null"/> when none is saved or the file is unreadable.</returns>
    ValueTask<DiagramLayout?> LoadAsync(Guid profileId, CancellationToken ct);

    /// <summary>Saves a layout, replacing any previous one.</summary>
    /// <param name="profileId">Connection profile identifier.</param>
    /// <param name="layout">Layout to save.</param>
    /// <param name="ct">Cancellation token.</param>
    ValueTask SaveAsync(Guid profileId, DiagramLayout layout, CancellationToken ct);

    /// <summary>Deletes the saved layout; a missing one is a no-op.</summary>
    /// <param name="profileId">Connection profile identifier.</param>
    /// <param name="ct">Cancellation token.</param>
    ValueTask DeleteAsync(Guid profileId, CancellationToken ct);
}
