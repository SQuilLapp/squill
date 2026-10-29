using MySqlConnector;
using Squill.Core;

namespace Squill.Data.MySql;

/// <summary>Reads the schema of the connected MySQL or MariaDB database into a <see cref="SchemaSnapshot"/>.</summary>
internal static class MySqlSchemaReader
{
    private const string ColumnsSql = """
        select t.TABLE_NAME, t.TABLE_TYPE, c.COLUMN_NAME, c.COLUMN_TYPE, c.IS_NULLABLE, c.COLUMN_DEFAULT, c.COLUMN_KEY
        from information_schema.TABLES t
        join information_schema.COLUMNS c on c.TABLE_SCHEMA = t.TABLE_SCHEMA and c.TABLE_NAME = t.TABLE_NAME
        where t.TABLE_SCHEMA = database() and t.TABLE_TYPE in ('BASE TABLE', 'VIEW')
        order by t.TABLE_NAME, c.ORDINAL_POSITION
        """;

    // Expression index parts have no COLUMN_NAME and are skipped; the primary key is already flagged on the columns.
    private const string IndexesSql = """
        select TABLE_NAME, INDEX_NAME, NON_UNIQUE, COLUMN_NAME
        from information_schema.STATISTICS
        where TABLE_SCHEMA = database() and INDEX_NAME <> 'PRIMARY' and COLUMN_NAME is not null
        order by TABLE_NAME, INDEX_NAME, SEQ_IN_INDEX
        """;

    private const string ForeignKeysSql = """
        select k.TABLE_NAME, k.CONSTRAINT_NAME, k.COLUMN_NAME, k.REFERENCED_TABLE_SCHEMA, k.REFERENCED_TABLE_NAME, k.REFERENCED_COLUMN_NAME
        from information_schema.KEY_COLUMN_USAGE k
        join information_schema.REFERENTIAL_CONSTRAINTS r
          on r.CONSTRAINT_SCHEMA = k.CONSTRAINT_SCHEMA and r.CONSTRAINT_NAME = k.CONSTRAINT_NAME and r.TABLE_NAME = k.TABLE_NAME
        where k.TABLE_SCHEMA = database()
        order by k.TABLE_NAME, k.CONSTRAINT_NAME, k.ORDINAL_POSITION
        """;

    /// <summary>Reads the tables and views of the connected database.</summary>
    /// <param name="connection">Open connection to read from.</param>
    /// <param name="cancellationToken">Token that cancels the queries.</param>
    /// <returns>The schema snapshot; the database name is both the default schema and the schema of every table.</returns>
    public static async ValueTask<SchemaSnapshot> ReadAsync(MySqlConnection connection, CancellationToken cancellationToken)
    {
        var database = (await ReadAsync(connection, "select database()", r => r.IsDBNull(0) ? null : r.GetString(0), cancellationToken))[0];
        var columns = (await ReadAsync(connection, ColumnsSql, r => (
            Table: r.GetString(0), Kind: r.GetString(1) == "VIEW" ? TableKind.View : TableKind.Table,
            Column: new ColumnDefinition(r.GetString(2), r.GetString(3), r.GetString(4) == "YES", r.GetString(6) == "PRI", r.IsDBNull(5) ? null : r.GetString(5))), cancellationToken))
            .GroupBy(c => (c.Table, c.Kind))
            .ToList();
        var indexes = (await ReadAsync(connection, IndexesSql, r => (
            Table: r.GetString(0), Name: r.GetString(1), Unique: r.GetInt64(2) == 0, Column: r.GetString(3)), cancellationToken))
            .GroupBy(i => (i.Table, i.Name, i.Unique))
            .ToLookup(g => g.Key.Table, g => new IndexInfo(g.Key.Name, g.Key.Unique, g.Select(i => i.Column).ToList()));
        var foreignKeys = (await ReadAsync(connection, ForeignKeysSql, r => (
            Table: r.GetString(0), Name: r.GetString(1), Column: r.GetString(2),
            RefSchema: r.GetString(3), RefTable: r.GetString(4), RefColumn: r.GetString(5)), cancellationToken))
            .GroupBy(f => (f.Table, f.Name, f.RefSchema, f.RefTable))
            .ToLookup(g => g.Key.Table, g => new ForeignKeyInfo(g.Key.Name, g.Select(f => f.Column).ToList(), g.Key.RefSchema, g.Key.RefTable, g.Select(f => f.RefColumn).ToList()));

        var tables = columns
            .Select(g => new TableInfo(database!, g.Key.Table, g.Key.Kind,
                g.Select(c => c.Column).ToList(), indexes[g.Key.Table].ToList(), foreignKeys[g.Key.Table].ToList()))
            .ToList();
        return new SchemaSnapshot(database, tables);
    }

    private static async Task<List<T>> ReadAsync<T>(MySqlConnection connection, string sql, Func<MySqlDataReader, T> map, CancellationToken cancellationToken)
    {
        await using var command = new MySqlCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var rows = new List<T>();
        while (await reader.ReadAsync(cancellationToken))
            rows.Add(map(reader));
        return rows;
    }
}
