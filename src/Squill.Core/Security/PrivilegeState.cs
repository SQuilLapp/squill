namespace Squill.Core.Security;

/// <summary>How a role holds one privilege on one object.</summary>
public enum PrivilegeStateKind
{
    /// <summary>Not held.</summary>
    None,

    /// <summary>Granted directly to the role.</summary>
    Granted,

    /// <summary>Held through a role the subject is a member of.</summary>
    Inherited,
}

/// <summary>State of one privilege cell.</summary>
/// <param name="Kind">Whether the privilege is missing, direct or inherited.</param>
/// <param name="FromRole">For <see cref="PrivilegeStateKind.Inherited"/>, the first role it comes from; otherwise <see langword="null"/>.</param>
public readonly record struct PrivilegeState(PrivilegeStateKind Kind, string? FromRole = null)
{
    /// <summary>The privilege is not held.</summary>
    public static PrivilegeState None => new(PrivilegeStateKind.None);

    /// <summary>The privilege is granted directly.</summary>
    public static PrivilegeState Granted => new(PrivilegeStateKind.Granted);

    /// <summary>Creates an inherited state.</summary>
    /// <param name="fromRole">The role the privilege comes from.</param>
    /// <returns>The inherited state.</returns>
    public static PrivilegeState Inherited(string fromRole) => new(PrivilegeStateKind.Inherited, fromRole);
}
