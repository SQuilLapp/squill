namespace Squill.Core.Activity;

/// <summary>A point-in-time, read-only picture of what a server is doing.</summary>
/// <param name="TakenAt">When the snapshot was taken.</param>
/// <param name="ActiveConnections">Number of client connections.</param>
/// <param name="MaxConnections">Server connection limit, or <see langword="null"/> when unknown.</param>
/// <param name="TransactionCounter">Cumulative number of finished transactions, or <see langword="null"/> when the engine has no cheap counter.</param>
/// <param name="Sessions">The sessions, the reader's own excluded.</param>
/// <param name="Blocking">The blocking relations.</param>
/// <param name="BlockingUnavailable">The engine could not be asked who blocks whom (no right on the lock tables): <paramref name="Blocking"/> is then empty but means "not available", not "none".</param>
public sealed record ActivitySnapshot(
    DateTimeOffset TakenAt,
    int ActiveConnections,
    int? MaxConnections,
    long? TransactionCounter,
    IReadOnlyList<ServerSession> Sessions,
    IReadOnlyList<BlockingEdge> Blocking,
    bool BlockingUnavailable = false);
