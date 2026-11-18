namespace Squill.Core.Security;

/// <summary>A database role with its memberships.</summary>
/// <param name="Name">Role name.</param>
/// <param name="CanLogin">Whether the role may log in (a user rather than a pure group).</param>
/// <param name="IsSuperuser">Whether the role bypasses privilege checks.</param>
/// <param name="Inherits">Whether the role automatically uses the privileges of the roles it belongs to.</param>
/// <param name="MemberOf">Roles this role is a member of.</param>
/// <param name="Members">Roles that are members of this role.</param>
public sealed record RoleInfo(
    string Name,
    bool CanLogin,
    bool IsSuperuser,
    bool Inherits,
    IReadOnlyList<string> MemberOf,
    IReadOnlyList<string> Members)
{
    /// <summary>Whether this is a group role: it has members or cannot log in.</summary>
    public bool IsGroup => Members.Count > 0 || !CanLogin;

    /// <inheritdoc/>
    public bool Equals(RoleInfo? other) =>
        other is not null && Name == other.Name && CanLogin == other.CanLogin && IsSuperuser == other.IsSuperuser
        && Inherits == other.Inherits && MemberOf.SequenceEqual(other.MemberOf) && Members.SequenceEqual(other.Members);

    /// <inheritdoc/>
    public override int GetHashCode() => HashCode.Combine(Name, CanLogin, IsSuperuser, Inherits, MemberOf.Count, Members.Count);
}
