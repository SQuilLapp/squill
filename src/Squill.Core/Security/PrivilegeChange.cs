namespace Squill.Core.Security;

/// <summary>Direction of a pending privilege change.</summary>
public enum PrivilegeChangeKind
{
    /// <summary>Give the privilege.</summary>
    Grant,

    /// <summary>Take the privilege away.</summary>
    Revoke,
}

/// <summary>One pending privilege change.</summary>
/// <param name="Change">Grant or revoke.</param>
/// <param name="Schema">Schema of the object.</param>
/// <param name="ObjectName">Name of the object.</param>
/// <param name="Kind">Kind of the object.</param>
/// <param name="Privilege">The privilege.</param>
/// <param name="Role">The role receiving or losing the privilege.</param>
public sealed record PrivilegeChange(
    PrivilegeChangeKind Change,
    string Schema,
    string ObjectName,
    ObjectKind Kind,
    PrivilegeKind Privilege,
    string Role);
