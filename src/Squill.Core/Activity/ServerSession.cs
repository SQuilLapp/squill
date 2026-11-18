namespace Squill.Core.Activity;

/// <summary>One session (backend, connection) of a database server.</summary>
/// <param name="Id">Engine session identifier, as text (a pid or session id).</param>
/// <param name="User">Login of the session, or <see langword="null"/> when not visible.</param>
/// <param name="Application">Client application name, or <see langword="null"/>.</param>
/// <param name="Client">Client address (host and port), or <see langword="null"/>.</param>
/// <param name="State">Engine-neutral state.</param>
/// <param name="StateText">The engine's own wording of the state, for display (for example <c>idle in transaction</c>).</param>
/// <param name="Duration">How long the current statement or state has lasted, or <see langword="null"/>.</param>
/// <param name="WaitEvent">What the session waits for, or <see langword="null"/>.</param>
/// <param name="Query">Current or last statement, cut to a bounded length, or <see langword="null"/> when not visible.</param>
/// <param name="TransactionStart">Start of the open transaction, or <see langword="null"/>.</param>
/// <param name="StateChange">Time of the last state change, or <see langword="null"/>.</param>
/// <param name="LocksHeld">Number of locks held, or <see langword="null"/> when unknown.</param>
/// <param name="IsSystem">The session is a server background process, not a client.</param>
public sealed record ServerSession(
    string Id,
    string? User,
    string? Application,
    string? Client,
    SessionState State,
    string StateText,
    TimeSpan? Duration,
    string? WaitEvent,
    string? Query,
    DateTimeOffset? TransactionStart,
    DateTimeOffset? StateChange,
    int? LocksHeld,
    bool IsSystem);
