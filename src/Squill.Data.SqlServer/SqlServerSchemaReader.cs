using Microsoft.Data.SqlClient;
using Squill.Core;

namespace Squill.Data.SqlServer;

/// <summary>Reads the schema of the connected SQL Server database into a <see cref="SchemaSnapshot"/>.</summary>
internal static class SqlServerSchemaReader
{
    private const string TablesCte = """
        with objs as (
            select object_id, schema_id, name, 'U' as kind from sys.tables
            union all
            select object_id, schema_id, name, 'V' as kind from sys.views)
        """;

    private const string ColumnsSql = TablesCte + """

        select s.name, o.name, o.kind, c.name, ty.name, c.max_length, c.precision, c.scale, c.is_nullable, d.definition,
               cast(case when exists (
                   select 1 from sys.indexes pk
                   join sys.index_columns pkc on pkc.object_id = pk.object_id and pkc.index_id = pk.index_id
                   where pk.object_id = o.object_id and pk.is_primary_key = 1 and pkc.column_id = c.column_id) then 1 else 0 end as bit)
        from objs o
        join sys.schemas s on s.schema_id = o.schema_id
        join sys.columns c on c.object_id = o.object_id
        join sys.types ty on ty.user_type_id = c.user_type_id
        left join sys.default_constraints d on d.parent_object_id = c.object_id and d.parent_column_id = c.column_id
        where s.name not in ('sys', 'INFORMATION_SCHEMA')
        order by s.name, o.name, c.column_id
        """;

    // Heaps (type 0) and the primary key are skipped; the primary key is already flagged on the columns.
    private const string IndexesSql = TablesCte + """

        select s.name, o.name, i.name, i.is_unique, c.name
        from objs o
        join sys.schemas s on s.schema_id = o.schema_id
        join sys.indexes i on i.object_id = o.object_id
        join sys.index_columns ic on ic.object_id = i.object_id and ic.index_id = i.index_id
        join sys.columns c on c.object_id = ic.object_id and c.column_id = ic.column_id
        where s.name not in ('sys', 'INFORMATION_SCHEMA') and i.type > 0 and i.is_primary_key = 0
          and i.is_hypothetical = 0 and ic.is_included_column = 0
        order by s.name, o.name, i.name, ic.key_ordinal
        """;

    private const string ForeignKeysSql = """
        select ps.name, pt.name, f.name, pc.name, rs.name, rt.name, rc.name
        from sys.foreign_keys f
        join sys.foreign_key_columns fc on fc.constraint_object_id = f.object_id
        join sys.objects pt on pt.object_id = f.parent_object_id
        join sys.schemas ps on ps.schema_id = pt.schema_id
        join sys.columns pc on pc.object_id = fc.parent_object_id and pc.column_id = fc.parent_column_id
        join sys.objects rt on rt.object_id = f.referenced_object_id
        join sys.schemas rs on rs.schema_id = rt.schema_id
        join sys.columns rc on rc.object_id = fc.referenced_object_id and rc.column_id = fc.referenced_column_id
        where ps.name not in ('sys', 'INFORMATION_SCHEMA')
        order by ps.name, pt.name, f.name, fc.constraint_column_id
        """;

    /// <summary>Formats a column type as <c>type(length)</c>, <c>type(precision,scale)</c> or the bare type name.</summary>
    /// <param name="typeName">Type name from <c>sys.types</c>.</param>
    /// <param name="maxLength">Storage size in bytes from <c>sys.columns</c>; -1 means <c>max</c>.</param>
    /// <param name="precision">Precision from <c>sys.columns</c>.</param>
    /// <param name="scale">Scale from <c>sys.columns</c>.</param>
    /// <returns>The formatted type, for example <c>nvarchar(20)</c>, <c>decimal(10,2)</c> or <c>int</c>.</returns>
    public static string FormatDataType(string typeName, int maxLength, int precision, int scale) => typeName switch
    {
        "char" or "varchar" or "binary" or "varbinary" => $"{typeName}({(maxLength == -1 ? "max" : maxLength)})",
        "nchar" or "nvarchar" => $"{typeName}({(maxLength == -1 ? "max" : maxLength / 2)})",
        "decimal" or "numeric" => $"{typeName}({precision},{scale})",
        "datetime2" or "time" or "datetimeoffset" => $"{typeName}({scale})",
        _ => typeName,
    };

    /// <summary>Reads the tables and views of every non-system schema of the connected database.</summary>
    /// <param name="connection">Open connection to read from.</param>
    /// <param name="cancellationToken">Token that cancels the queries.</param>
    /// <returns>The schema snapshot, whose default schema is <c>schema_name()</c> of the connection.</returns>
    public static async ValueTask<SchemaSnapshot> ReadAsync(SqlConnection connection, CancellationToken cancellationToken)
    {
        var defaultSchema = (await ReadAsync(connection, "select schema_name()", r => r.IsDBNull(0) ? null : r.GetString(0), cancellationToken))[0];
        var columns = (await ReadAsync(connection, ColumnsSql, r => (
            Schema: r.GetString(0), Table: r.GetString(1), Kind: r.GetString(2) == "V" ? TableKind.View : TableKind.Table,
            Column: new ColumnDefinition(r.GetString(3), FormatDataType(r.GetString(4), r.GetInt16(5), r.GetByte(6), r.GetByte(7)),
                r.GetBoolean(8), r.GetBoolean(10), r.IsDBNull(9) ? null : r.GetString(9))), cancellationToken))
            .GroupBy(c => (c.Schema, c.Table, c.Kind))
            .ToList();
        var indexes = (await ReadAsync(connection, IndexesSql, r => (
            Schema: r.GetString(0), Table: r.GetString(1), Name: r.GetString(2), Unique: r.GetBoolean(3), Column: r.GetString(4)), cancellationToken))
            .GroupBy(i => (i.Schema, i.Table, i.Name, i.Unique))
            .ToLookup(g => (g.Key.Schema, g.Key.Table), g => new IndexInfo(g.Key.Name, g.Key.Unique, g.Select(i => i.Column).ToList()));
        var foreignKeys = (await ReadAsync(connection, ForeignKeysSql, r => (
            Schema: r.GetString(0), Table: r.GetString(1), Name: r.GetString(2), Column: r.GetString(3),
            RefSchema: r.GetString(4), RefTable: r.GetString(5), RefColumn: r.GetString(6)), cancellationToken))
            .GroupBy(f => (f.Schema, f.Table, f.Name, f.RefSchema, f.RefTable))
            .ToLookup(g => (g.Key.Schema, g.Key.Table),
                g => new ForeignKeyInfo(g.Key.Name, g.Select(f => f.Column).ToList(), g.Key.RefSchema, g.Key.RefTable, g.Select(f => f.RefColumn).ToList()));

        var tables = columns
            .Select(g => new TableInfo(g.Key.Schema, g.Key.Table, g.Key.Kind,
                g.Select(c => c.Column).ToList(), indexes[(g.Key.Schema, g.Key.Table)].ToList(), foreignKeys[(g.Key.Schema, g.Key.Table)].ToList()))
            .ToList();
        return new SchemaSnapshot(defaultSchema, tables);
    }

    private static async Task<List<T>> ReadAsync<T>(SqlConnection connection, string sql, Func<SqlDataReader, T> map, CancellationToken cancellationToken)
    {
        await using var command = new SqlCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var rows = new List<T>();
        while (await reader.ReadAsync(cancellationToken))
            rows.Add(map(reader));
        return rows;
    }
}
