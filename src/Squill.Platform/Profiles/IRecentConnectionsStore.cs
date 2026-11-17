namespace Squill.Platform.Profiles;

/// <summary>Remembers when each connection profile was last opened.</summary>
public interface IRecentConnectionsStore
{
    /// <summary>Lists the recent connections.</summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>At most 20 entries, newest first; empty when nothing is stored or the file is unreadable.</returns>
    Task<IReadOnlyList<RecentConnection>> ListAsync(CancellationToken ct);

    /// <summary>Moves a profile to the front, replacing its previous entry and dropping the oldest beyond 20.</summary>
    /// <param name="profileId">Identifier of the opened profile.</param>
    /// <param name="openedAt">Time the profile was opened.</param>
    /// <param name="ct">Cancellation token.</param>
    Task TouchAsync(Guid profileId, DateTimeOffset openedAt, CancellationToken ct);

    /// <summary>Removes a profile from the list; an unknown id is a no-op.</summary>
    /// <param name="profileId">Identifier of the profile.</param>
    /// <param name="ct">Cancellation token.</param>
    Task ForgetAsync(Guid profileId, CancellationToken ct);
}
