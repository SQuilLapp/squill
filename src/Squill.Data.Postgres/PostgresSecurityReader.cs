using Npgsql;
using Squill.Core.Security;

namespace Squill.Data.Postgres;

/// <summary>Reads roles, memberships and table privileges from the PostgreSQL catalogs, one query per call.</summary>
internal static class PostgresSecurityReader
{
    private const string RolesSql = """
        select r.rolname::text, r.rolcanlogin, r.rolsuper, r.rolinherit,
               array(select g.rolname::text from pg_auth_members m join pg_roles g on g.oid = m.roleid
                     where m.member = r.oid and ($1 or g.rolname not like 'pg\_%') order by 1),
               array(select u.rolname::text from pg_auth_members m join pg_roles u on u.oid = m.member
                     where m.roleid = r.oid and ($1 or u.rolname not like 'pg\_%') order by 1)
        from pg_roles r
        where $1 or r.rolname not like 'pg\_%'
        order by r.rolname
        """;

    private const string SchemasSql = """
        select nspname::text from pg_namespace
        where nspname not in ('pg_catalog', 'information_schema', 'pg_toast') and nspname not like 'pg\_temp\_%' and nspname not like 'pg\_toast\_temp\_%'
        order by 1
        """;

    // Direct: the subject is a grantee. Inherited: PUBLIC, or a role the subject belongs to with inheritance (pg_has_role USAGE);
    // the source is a real role in preference to PUBLIC, then the first by name. acldefault gives the owner all privileges when relacl is null.
    private const string PrivilegesSql = """
        with s as (select oid from pg_roles where rolname::text = $1)
        select n.nspname::text, c.relname::text, c.relkind::text, x.priv, x.direct, x.src
        from pg_class c
        join pg_namespace n on n.oid = c.relnamespace
        cross join s
        left join lateral (
            select a.privilege_type as priv, bool_or(a.grantee = s.oid) as direct,
                   (array_agg(case when a.grantee = 0 then 'PUBLIC' else g.rolname::text end order by (a.grantee = 0), g.rolname)
                        filter (where a.grantee <> s.oid))[1] as src
            from aclexplode(coalesce(c.relacl, acldefault('r', c.relowner))) a
            left join pg_roles g on g.oid = a.grantee
            where a.privilege_type in ('SELECT', 'INSERT', 'UPDATE', 'DELETE', 'TRUNCATE', 'REFERENCES', 'TRIGGER')
              and case when a.grantee in (s.oid, 0) then true else pg_has_role(s.oid, a.grantee, 'USAGE') end
            group by a.privilege_type) x on true
        where c.relkind in ('r', 'p', 'v', 'm') and n.nspname::text = any($2)
        order by 1, 2
        """;

    /// <summary>Reads the roles.</summary>
    /// <param name="dataSource">Source of the reading session, separate from the user's.</param>
    /// <param name="includeSystem">Whether the predefined <c>pg_*</c> roles are returned.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The roles ordered by name.</returns>
    public static async ValueTask<IReadOnlyList<RoleInfo>> ReadRolesAsync(NpgsqlDataSource dataSource, bool includeSystem, CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand(RolesSql, connection);
        command.Parameters.Add(new NpgsqlParameter { Value = includeSystem });
        var roles = new List<RoleInfo>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
            roles.Add(new RoleInfo(reader.GetString(0), reader.GetBoolean(1), reader.GetBoolean(2), reader.GetBoolean(3), reader.GetFieldValue<string[]>(4), reader.GetFieldValue<string[]>(5)));
        return roles;
    }

    /// <summary>Lists the user schemas.</summary>
    /// <param name="dataSource">Source of the reading session, separate from the user's.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The schema names ordered by name.</returns>
    public static async ValueTask<IReadOnlyList<string>> ListSchemasAsync(NpgsqlDataSource dataSource, CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand(SchemasSql, connection);
        var schemas = new List<string>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
            schemas.Add(reader.GetString(0));
        return schemas;
    }

    /// <summary>Reads the privileges of one role.</summary>
    /// <param name="dataSource">Source of the reading session, separate from the user's.</param>
    /// <param name="role">The subject role.</param>
    /// <param name="schemas">The schemas to read.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>One entry per object, ordered by schema then name.</returns>
    public static async ValueTask<IReadOnlyList<ObjectPrivileges>> ReadPrivilegesAsync(
        NpgsqlDataSource dataSource, string role, IReadOnlyList<string> schemas, CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand(PrivilegesSql, connection);
        command.Parameters.Add(new NpgsqlParameter { Value = role });
        command.Parameters.Add(new NpgsqlParameter { Value = schemas.ToArray() });
        var objects = new List<ObjectPrivileges>();
        Dictionary<PrivilegeKind, PrivilegeState>? states = null;
        string? schema = null, name = null;
        var kind = ObjectKind.Table;
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var rowSchema = reader.GetString(0);
            var rowName = reader.GetString(1);
            if (states is null || rowSchema != schema || rowName != name)
            {
                if (states is not null)
                    objects.Add(new ObjectPrivileges(schema!, name!, kind, states));
                (schema, name, states) = (rowSchema, rowName, []);
                kind = reader.GetString(2) switch { "v" => ObjectKind.View, "m" => ObjectKind.MaterializedView, _ => ObjectKind.Table };
            }

            if (!reader.IsDBNull(3) && ParsePrivilege(reader.GetString(3)) is { } privilege)
                states[privilege] = reader.GetBoolean(4)
                    ? PrivilegeState.Granted
                    : PrivilegeState.Inherited(reader.IsDBNull(5) ? "PUBLIC" : reader.GetString(5));
        }

        if (states is not null)
            objects.Add(new ObjectPrivileges(schema!, name!, kind, states));
        return objects;
    }

    private static PrivilegeKind? ParsePrivilege(string type) => type switch
    {
        "SELECT" => PrivilegeKind.Select,
        "INSERT" => PrivilegeKind.Insert,
        "UPDATE" => PrivilegeKind.Update,
        "DELETE" => PrivilegeKind.Delete,
        "TRUNCATE" => PrivilegeKind.Truncate,
        "REFERENCES" => PrivilegeKind.References,
        "TRIGGER" => PrivilegeKind.Trigger,
        _ => null,
    };
}
