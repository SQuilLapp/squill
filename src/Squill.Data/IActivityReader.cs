using Squill.Core.Activity;

namespace Squill.Data;

/// <summary>
/// Optional capability of a connection: a read-only picture of the server activity, plus the two kill actions. Callers test
/// <c>connection as IActivityReader</c>; <see langword="null"/> means unsupported (SQLite, Oracle).
/// </summary>
/// <remarks>The snapshot reads on a separate server session, so it never disturbs the user's transaction or running statement.</remarks>
public interface IActivityReader
{
    /// <summary>Gets a value indicating whether <see cref="CancelQueryAsync"/> works on this engine (it does not on SQL Server).</summary>
    bool SupportsCancelQuery { get; }

    /// <summary>Takes a snapshot of the server activity.</summary>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The sessions and the blocking relations.</returns>
    /// <exception cref="ActivityUnavailableException">The login lacks the right to see the server activity.</exception>
    ValueTask<ActivitySnapshot> ReadAsync(CancellationToken cancellationToken);

    /// <summary>Cancels the statement a session is running; the session stays connected.</summary>
    /// <param name="sessionId">Id of a session of the last snapshot.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <exception cref="ArgumentException">The id is not a valid session id of this engine.</exception>
    /// <exception cref="ActivityActionFailedException">The server refused or found no such session.</exception>
    /// <exception cref="NotSupportedException"><see cref="SupportsCancelQuery"/> is <see langword="false"/>.</exception>
    ValueTask CancelQueryAsync(string sessionId, CancellationToken cancellationToken);

    /// <summary>Ends a session and rolls back its open transaction.</summary>
    /// <param name="sessionId">Id of a session of the last snapshot.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <exception cref="ArgumentException">The id is not a valid session id of this engine.</exception>
    /// <exception cref="ActivityActionFailedException">The server refused or found no such session.</exception>
    ValueTask TerminateSessionAsync(string sessionId, CancellationToken cancellationToken);
}
