namespace Squill.Platform.Updates;

/// <summary>Persists the <see cref="UpdateState"/>.</summary>
public interface IUpdateStateStore
{
    /// <summary>Loads the state; a missing or corrupt file gives the empty state.</summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The stored state.</returns>
    ValueTask<UpdateState> LoadAsync(CancellationToken ct);

    /// <summary>Replaces the stored state.</summary>
    /// <param name="state">The state to store.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A task that completes when the state is written.</returns>
    ValueTask SaveAsync(UpdateState state, CancellationToken ct);
}
