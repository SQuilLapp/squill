namespace Squill.Core.Security;

/// <summary>The privileges one role holds on one table or view.</summary>
/// <param name="Schema">Schema of the object.</param>
/// <param name="Name">Name of the object.</param>
/// <param name="Kind">Kind of the object.</param>
/// <param name="States">State per privilege; a missing kind means <see cref="PrivilegeState.None"/>.</param>
public sealed record ObjectPrivileges(
    string Schema,
    string Name,
    ObjectKind Kind,
    IReadOnlyDictionary<PrivilegeKind, PrivilegeState> States)
{
    /// <summary>Key identifying the object.</summary>
    public PrivilegeObjectKey Key => new(Schema, Name, Kind);

    /// <summary>Gets the state of a privilege.</summary>
    /// <param name="kind">The privilege.</param>
    /// <returns>The state, <see cref="PrivilegeState.None"/> when absent.</returns>
    public PrivilegeState StateOf(PrivilegeKind kind) => States.TryGetValue(kind, out var s) ? s : PrivilegeState.None;

    /// <inheritdoc/>
    public bool Equals(ObjectPrivileges? other) =>
        other is not null && Key == other.Key
        && Enum.GetValues<PrivilegeKind>().All(k => StateOf(k) == other.StateOf(k));

    /// <inheritdoc/>
    public override int GetHashCode() => Key.GetHashCode();
}

/// <summary>Identifies a table or view in the privilege matrix.</summary>
/// <param name="Schema">Schema of the object.</param>
/// <param name="Name">Name of the object.</param>
/// <param name="Kind">Kind of the object.</param>
public readonly record struct PrivilegeObjectKey(string Schema, string Name, ObjectKind Kind);
