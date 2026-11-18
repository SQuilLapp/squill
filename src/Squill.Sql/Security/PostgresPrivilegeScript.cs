using Squill.Core;
using Squill.Core.Security;
using Squill.Sql.Completion;

namespace Squill.Sql.Security;

/// <summary>A generated GRANT / REVOKE script.</summary>
/// <param name="Text">The script text, header comment included; empty when there is no change.</param>
/// <param name="Statements">The statements to execute one by one, without the comment and without <c>BEGIN</c> / <c>COMMIT</c>.</param>
public sealed record PrivilegeScript(string Text, IReadOnlyList<string> Statements);

/// <summary>Builds the PostgreSQL statements for a <see cref="PrivilegeChangeSet"/>.</summary>
public static class PostgresPrivilegeScript
{
    /// <summary>Builds the script: grants first, then revokes, one statement per object merging its privileges.</summary>
    /// <param name="role">The role the changes apply to.</param>
    /// <param name="changes">The pending changes.</param>
    /// <param name="connectionName">The connection name shown in the header comment.</param>
    /// <returns>The script; empty text when there are no changes.</returns>
    public static PrivilegeScript Build(string role, PrivilegeChangeSet changes, string connectionName)
    {
        ArgumentNullException.ThrowIfNull(role);
        ArgumentNullException.ThrowIfNull(changes);
        if (changes.Count == 0)
        {
            return new PrivilegeScript(string.Empty, []);
        }

        // shortcut: reserved words (a role named "user") are not quoted; upgrade with a PostgreSQL keyword list.
        var quotedRole = IdentifierQuoting.Quote(role, DatabaseDialect.PostgreSql);
        var statements = new List<string>();
        foreach (var kind in new[] { PrivilegeChangeKind.Grant, PrivilegeChangeKind.Revoke })
        {
            var groups = changes.Changes
                .Where(c => c.Change == kind)
                .GroupBy(c => (c.Schema, c.ObjectName))
                .OrderBy(g => g.Key.Schema, StringComparer.Ordinal)
                .ThenBy(g => g.Key.ObjectName, StringComparer.Ordinal);
            foreach (var g in groups)
            {
                var privileges = string.Join(", ", g.Select(c => c.Privilege).Distinct().Order().Select(p => p.ToString().ToUpperInvariant()));
                var target = $"{IdentifierQuoting.Quote(g.Key.Schema, DatabaseDialect.PostgreSql)}.{IdentifierQuoting.Quote(g.Key.ObjectName, DatabaseDialect.PostgreSql)}";
                statements.Add(kind == PrivilegeChangeKind.Grant
                    ? $"GRANT {privileges} ON {target} TO {quotedRole};"
                    : $"REVOKE {privileges} ON {target} FROM {quotedRole};");
            }
        }

        var text = $"-- Rôle {role} . {connectionName}{Environment.NewLine}{string.Join(Environment.NewLine, statements)}";
        return new PrivilegeScript(text, statements);
    }
}
