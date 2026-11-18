namespace Squill.Core.Security;

/// <summary>Result of toggling one privilege cell.</summary>
public enum ToggleResult
{
    /// <summary>A change was added.</summary>
    Added,

    /// <summary>A pending change was removed.</summary>
    Removed,

    /// <summary>The cell is inherited; it can only be revoked at the source role.</summary>
    InheritedCannotRevoke,
}

/// <summary>The pending, not yet applied, privilege changes of one role.</summary>
/// <param name="role">The role the changes apply to.</param>
public sealed class PrivilegeChangeSet(string role)
{
    private readonly List<PrivilegeChange> _changes = [];

    /// <summary>The pending changes, in the order they were made.</summary>
    public IReadOnlyList<PrivilegeChange> Changes => _changes;

    /// <summary>Number of pending changes.</summary>
    public int Count => _changes.Count;

    /// <summary>Finds the pending change of a cell.</summary>
    /// <param name="key">The object.</param>
    /// <param name="kind">The privilege.</param>
    /// <returns>The change, or <see langword="null"/>.</returns>
    public PrivilegeChange? Pending(PrivilegeObjectKey key, PrivilegeKind kind) =>
        _changes.Find(c => c.Schema == key.Schema && c.ObjectName == key.Name && c.Kind == key.Kind && c.Privilege == kind);

    /// <summary>Toggles one cell: none adds a grant, granted adds a revoke, a pending change is cancelled.</summary>
    /// <param name="key">The object.</param>
    /// <param name="kind">The privilege.</param>
    /// <param name="currentState">The state on the server, ignoring pending changes.</param>
    /// <returns>What happened.</returns>
    public ToggleResult Toggle(PrivilegeObjectKey key, PrivilegeKind kind, PrivilegeState currentState)
    {
        if (Pending(key, kind) is { } pending)
        {
            _changes.Remove(pending);
            return ToggleResult.Removed;
        }

        if (currentState.Kind == PrivilegeStateKind.Inherited)
        {
            return ToggleResult.InheritedCannotRevoke;
        }

        var change = currentState.Kind == PrivilegeStateKind.Granted ? PrivilegeChangeKind.Revoke : PrivilegeChangeKind.Grant;
        _changes.Add(new PrivilegeChange(change, key.Schema, key.Name, key.Kind, kind, role));
        return ToggleResult.Added;
    }

    /// <summary>Toggles a whole row: reverses every direct privilege when all are effectively held, otherwise grants the missing ones. Inherited cells are skipped.</summary>
    /// <param name="key">The object.</param>
    /// <param name="current">The states on the server, ignoring pending changes.</param>
    public void ToggleRow(PrivilegeObjectKey key, IReadOnlyDictionary<PrivilegeKind, PrivilegeState> current)
    {
        var cells = Enum.GetValues<PrivilegeKind>()
            .Select(k => (Kind: k, State: current.TryGetValue(k, out var s) ? s : PrivilegeState.None))
            .Where(c => c.State.Kind != PrivilegeStateKind.Inherited)
            .Select(c => (c.Kind, c.State, On: IsOn(key, c.Kind, c.State)))
            .ToList();
        var allOn = cells.All(c => c.On);
        foreach (var c in cells.Where(c => allOn || !c.On))
        {
            Toggle(key, c.Kind, c.State);
        }
    }

    private bool IsOn(PrivilegeObjectKey key, PrivilegeKind kind, PrivilegeState state) =>
        Pending(key, kind) is { } p ? p.Change == PrivilegeChangeKind.Grant : state.Kind == PrivilegeStateKind.Granted;

    /// <summary>Drops every pending change.</summary>
    public void Clear() => _changes.Clear();
}
