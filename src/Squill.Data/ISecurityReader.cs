using Squill.Core.Security;

namespace Squill.Data;

/// <summary>
/// Optional capability of a connection: a read-only picture of the roles and of the table privileges of one role. Callers test
/// <c>connection as ISecurityReader</c>; <see langword="null"/> means unsupported (every engine but PostgreSQL for now).
/// </summary>
/// <remarks>Each call is one catalog query on a separate server session, so it never disturbs the user's transaction.</remarks>
public interface ISecurityReader
{
    /// <summary>Reads the roles with their memberships in both directions.</summary>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The roles, system roles excluded, ordered by name.</returns>
    ValueTask<IReadOnlyList<RoleInfo>> ReadRolesAsync(CancellationToken cancellationToken);

    /// <summary>Reads the privileges one role holds on the tables, views and materialized views of some schemas.</summary>
    /// <param name="role">The subject role.</param>
    /// <param name="schemas">The schemas to read; objects of other schemas are not returned.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>One entry per object, including objects the role holds nothing on, ordered by schema then name.</returns>
    ValueTask<IReadOnlyList<ObjectPrivileges>> ReadPrivilegesAsync(string role, IReadOnlyList<string> schemas, CancellationToken cancellationToken);

    /// <summary>Lists the user schemas.</summary>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The schema names, system schemas excluded, ordered by name.</returns>
    ValueTask<IReadOnlyList<string>> ListSchemasAsync(CancellationToken cancellationToken);
}
