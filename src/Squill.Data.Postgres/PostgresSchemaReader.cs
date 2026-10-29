using Npgsql;
using Squill.Core;

namespace Squill.Data.Postgres;

/// <summary>Reads the schema of a Postgres database into a <see cref="SchemaSnapshot"/>.</summary>
internal static class PostgresSchemaReader
{
    private const string UserSchemas = """
        n.nspname not in ('pg_catalog', 'information_schema', 'pg_toast')
        and n.nspname not like 'pg\_temp%' and n.nspname not like 'pg\_toast\_temp%'
        """;

    private const string ColumnsSql = $"""
        select n.nspname, c.relname, c.relkind::text, a.attname::text, format_type(a.atttypid, a.atttypmod),
               not a.attnotnull, pg_get_expr(d.adbin, d.adrelid),
               coalesce(a.attnum = any(pk.indkey::int2[]), false)
        from pg_class c
        join pg_namespace n on n.oid = c.relnamespace
        join pg_attribute a on a.attrelid = c.oid and a.attnum > 0 and not a.attisdropped
        left join pg_attrdef d on d.adrelid = c.oid and d.adnum = a.attnum
        left join pg_index pk on pk.indrelid = c.oid and pk.indisprimary
        where c.relkind in ('r', 'p', 'v', 'm') and {UserSchemas}
        order by n.nspname, c.relname, a.attnum
        """;

    // Expression columns have attnum 0, match no attribute and are skipped.
    private const string IndexesSql = $"""
        select n.nspname, t.relname, ic.relname, i.indisunique,
               array(select a.attname::text from unnest(i.indkey::int2[]) with ordinality k(attnum, ord)
                     join pg_attribute a on a.attrelid = t.oid and a.attnum = k.attnum order by k.ord)
        from pg_index i
        join pg_class t on t.oid = i.indrelid
        join pg_class ic on ic.oid = i.indexrelid
        join pg_namespace n on n.oid = t.relnamespace
        where not i.indisprimary and t.relkind in ('r', 'p', 'm') and {UserSchemas}
        order by n.nspname, t.relname, ic.relname
        """;

    private const string ForeignKeysSql = $"""
        select n.nspname, t.relname, c.conname,
               array(select a.attname::text from unnest(c.conkey) with ordinality k(attnum, ord)
                     join pg_attribute a on a.attrelid = c.conrelid and a.attnum = k.attnum order by k.ord),
               rn.nspname, rt.relname,
               array(select a.attname::text from unnest(c.confkey) with ordinality k(attnum, ord)
                     join pg_attribute a on a.attrelid = c.confrelid and a.attnum = k.attnum order by k.ord)
        from pg_constraint c
        join pg_class t on t.oid = c.conrelid
        join pg_namespace n on n.oid = t.relnamespace
        join pg_class rt on rt.oid = c.confrelid
        join pg_namespace rn on rn.oid = rt.relnamespace
        where c.contype = 'f' and {UserSchemas}
        order by n.nspname, t.relname, c.conname
        """;

    /// <summary>Reads the user-visible schema objects of the connected database.</summary>
    /// <param name="connection">Open connection to read from.</param>
    /// <param name="cancellationToken">Token that cancels the queries.</param>
    /// <returns>The schema snapshot.</returns>
    public static async ValueTask<SchemaSnapshot> ReadAsync(NpgsqlConnection connection, CancellationToken cancellationToken)
    {
        var defaultSchema = await ReadAsync(connection, "select current_schema()", r => r.IsDBNull(0) ? null : r.GetString(0), cancellationToken);
        var columns = (await ReadAsync(connection, ColumnsSql, r => (
            Schema: r.GetString(0), Table: r.GetString(1), Kind: r.GetString(2) is "v" or "m" ? TableKind.View : TableKind.Table,
            Column: new ColumnDefinition(r.GetString(3), r.GetString(4), r.GetBoolean(5), r.GetBoolean(7), r.IsDBNull(6) ? null : r.GetString(6))), cancellationToken))
            .GroupBy(c => (c.Schema, c.Table, c.Kind))
            .ToList();
        var indexes = (await ReadAsync(connection, IndexesSql, r => (
            Schema: r.GetString(0), Table: r.GetString(1), Index: new IndexInfo(r.GetString(2), r.GetBoolean(3), r.GetFieldValue<string[]>(4))), cancellationToken))
            .ToLookup(i => (i.Schema, i.Table), i => i.Index);
        var foreignKeys = (await ReadAsync(connection, ForeignKeysSql, r => (
            Schema: r.GetString(0), Table: r.GetString(1),
            Key: new ForeignKeyInfo(r.GetString(2), r.GetFieldValue<string[]>(3), r.GetString(4), r.GetString(5), r.GetFieldValue<string[]>(6))), cancellationToken))
            .ToLookup(f => (f.Schema, f.Table), f => f.Key);

        var tables = columns
            .Select(g => new TableInfo(g.Key.Schema, g.Key.Table, g.Key.Kind,
                g.Select(c => c.Column).ToList(), indexes[(g.Key.Schema, g.Key.Table)].ToList(), foreignKeys[(g.Key.Schema, g.Key.Table)].ToList()))
            .ToList();
        return new SchemaSnapshot(defaultSchema[0], tables);
    }

    private static async Task<List<T>> ReadAsync<T>(NpgsqlConnection connection, string sql, Func<NpgsqlDataReader, T> map, CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var rows = new List<T>();
        while (await reader.ReadAsync(cancellationToken))
            rows.Add(map(reader));
        return rows;
    }
}
