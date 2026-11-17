namespace Squill.Core.Activity;

/// <summary>One "who blocks whom" relation between two sessions.</summary>
/// <param name="BlockerId">Id of the session holding the lock.</param>
/// <param name="BlockedId">Id of the session waiting for it.</param>
/// <param name="LockMode">Mode of the contested lock, or <see langword="null"/> when unknown.</param>
/// <param name="Relation">Name of the contested object, or <see langword="null"/> when unknown.</param>
public sealed record BlockingEdge(string BlockerId, string BlockedId, string? LockMode, string? Relation);
