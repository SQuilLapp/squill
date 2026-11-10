using Squill.Core;

namespace Squill.Sql.Completion;

/// <summary>Decides how a table is written in a statement: bare when the engine resolves the bare name to it, schema-qualified otherwise.</summary>
internal static class TableReference
{
    /// <summary>Gets whether the bare name of <paramref name="table"/> would not resolve to it (an engine with a default schema resolves bare names only there; without one, a unique name resolves).</summary>
    /// <param name="table">The table.</param>
    /// <param name="schema">The snapshot that holds every table.</param>
    /// <returns><see langword="true"/> when the schema must be written.</returns>
    public static bool NeedsSchema(TableInfo table, SchemaSnapshot schema) =>
        table.Schema is not null
        && !string.Equals(table.Schema, schema.DefaultSchema, StringComparison.OrdinalIgnoreCase)
        && !(schema.DefaultSchema is null && ReferenceEquals(schema.FindTable(null, table.Name), table));

    /// <summary>Gets the quoted text that references <paramref name="table"/>.</summary>
    /// <param name="table">The table.</param>
    /// <param name="schema">The snapshot that holds every table.</param>
    /// <param name="dialect">The dialect that decides the quoting.</param>
    /// <returns>The bare quoted name, or <c>schema.name</c> when the bare name would not resolve to this table.</returns>
    public static string Insert(TableInfo table, SchemaSnapshot schema, DatabaseDialect dialect)
    {
        var name = IdentifierQuoting.Quote(table.Name, dialect);
        return NeedsSchema(table, schema) ? IdentifierQuoting.Quote(table.Schema!, dialect) + "." + name : name;
    }
}
