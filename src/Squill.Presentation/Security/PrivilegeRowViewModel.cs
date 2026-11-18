using Squill.Core.Security;
using Squill.Presentation.Localization;

namespace Squill.Presentation.Security;

/// <summary>How a cell of the privilege matrix is drawn.</summary>
public enum PrivilegeCellDisplay
{
    /// <summary>Not held.</summary>
    Empty,

    /// <summary>Granted directly.</summary>
    Granted,

    /// <summary>Held through another role.</summary>
    Inherited,

    /// <summary>A grant waiting to be applied.</summary>
    PendingGrant,

    /// <summary>A revoke waiting to be applied.</summary>
    PendingRevoke,
}

/// <summary>One cell of the privilege matrix: a privilege of one object, with its pending change.</summary>
/// <param name="Kind">The privilege this column stands for.</param>
/// <param name="Display">How the cell is drawn.</param>
/// <param name="CanToggle">Whether a click or <c>Espace</c> changes it; false for inherited cells.</param>
/// <param name="Tooltip">The tooltip, empty unless the cell is inherited.</param>
public sealed record PrivilegeCellViewModel(PrivilegeKind Kind, PrivilegeCellDisplay Display, bool CanToggle, string Tooltip);

/// <summary>One row of the privilege matrix: a table, view or materialized view.</summary>
public sealed class PrivilegeRowViewModel
{
    internal PrivilegeRowViewModel(ObjectPrivileges privileges, PrivilegeChangeSet changes, string role, bool showInherited)
    {
        Privileges = privileges;
        Schema = privileges.Schema;
        Name = privileges.Name;
        Cells = [.. Enum.GetValues<PrivilegeKind>().Select(k => Cell(k, privileges, changes, role, showInherited))];
    }

    /// <summary>Gets the server state of the object's privileges, ignoring pending changes.</summary>
    public ObjectPrivileges Privileges { get; }

    /// <summary>Gets the schema of the object.</summary>
    public string Schema { get; }

    /// <summary>Gets the name of the object.</summary>
    public string Name { get; }

    /// <summary>Gets the cells, one per <see cref="PrivilegeKind"/> in enum order.</summary>
    public IReadOnlyList<PrivilegeCellViewModel> Cells { get; }

    private static PrivilegeCellViewModel Cell(PrivilegeKind kind, ObjectPrivileges privileges, PrivilegeChangeSet changes, string role, bool showInherited)
    {
        if (changes.Pending(privileges.Key, kind) is { } pending)
        {
            return new(kind, pending.Change == PrivilegeChangeKind.Grant ? PrivilegeCellDisplay.PendingGrant : PrivilegeCellDisplay.PendingRevoke, true, "");
        }

        var state = privileges.StateOf(kind);
        return state.Kind switch
        {
            PrivilegeStateKind.Granted => new(kind, PrivilegeCellDisplay.Granted, true, ""),
            PrivilegeStateKind.Inherited when showInherited => new(kind, PrivilegeCellDisplay.Inherited, false, InheritedTooltip(state.FromRole, privileges, kind)),
            PrivilegeStateKind.Inherited => new(kind, PrivilegeCellDisplay.Empty, false, ""),
            _ => new(kind, PrivilegeCellDisplay.Empty, true, ""),
        };
    }

    private static string InheritedTooltip(string? from, ObjectPrivileges privileges, PrivilegeKind kind) =>
        UiText.Format("security.inheritedFrom", from ?? "?", $"GRANT {kind.ToString().ToUpperInvariant()} ON {privileges.Schema}.{privileges.Name} TO {from}");
}
