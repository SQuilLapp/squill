using Microsoft.Data.Sqlite;
using Squill.Core;

namespace Squill.Data.Sqlite;

internal static class SqliteSchemaReader
{
    private const string Schema = "main";

    public static async ValueTask<SchemaSnapshot> ReadAsync(SqliteConnection connection, CancellationToken cancellationToken)
    {
        var tables = new List<TableInfo>();
        foreach (var (name, kind) in await ReadAsync(connection, """SELECT name, type FROM sqlite_master WHERE type IN ('table','view') AND name NOT LIKE 'sqlite\_%' ESCAPE '\' ORDER BY name""",
                     r => (r.GetString(0), r.GetString(1) == "view" ? TableKind.View : TableKind.Table), cancellationToken))
        {
            var columns = await ReadColumnsAsync(connection, name, cancellationToken);
            var indexes = kind == TableKind.Table ? await ReadIndexesAsync(connection, name, cancellationToken) : [];
            var foreignKeys = kind == TableKind.Table ? await ReadForeignKeysAsync(connection, name, cancellationToken) : [];
            tables.Add(new TableInfo(Schema, name, kind, columns, indexes, foreignKeys));
        }
        return new SchemaSnapshot(Schema, tables);
    }

    private static string Quote(string name) => "\"" + name.Replace("\"", "\"\"") + "\"";

    private static async Task<List<T>> ReadAsync<T>(SqliteConnection connection, string sql, Func<SqliteDataReader, T> map, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var rows = new List<T>();
        while (await reader.ReadAsync(cancellationToken))
            rows.Add(map(reader));
        return rows;
    }

    private static async Task<IReadOnlyList<ColumnDefinition>> ReadColumnsAsync(SqliteConnection connection, string table, CancellationToken cancellationToken)
    {
        var rows = await ReadAsync(connection, $"PRAGMA table_xinfo({Quote(table)})",
            r => (Name: r.GetString(1), Type: r.GetString(2), NotNull: r.GetInt32(3) != 0,
                  Default: r.IsDBNull(4) ? null : r.GetString(4), Pk: r.GetInt32(5), Hidden: r.GetInt32(6)), cancellationToken);
        return rows.Where(c => c.Hidden == 0)
            .Select(c => new ColumnDefinition(c.Name, c.Type, !c.NotNull, c.Pk > 0, c.Default))
            .ToList();
    }

    private static async Task<IReadOnlyList<IndexInfo>> ReadIndexesAsync(SqliteConnection connection, string table, CancellationToken cancellationToken)
    {
        var list = await ReadAsync(connection, $"PRAGMA index_list({Quote(table)})",
            r => (Name: r.GetString(1), Unique: r.GetInt32(2) != 0, Origin: r.GetString(3)), cancellationToken);
        var indexes = new List<IndexInfo>();
        foreach (var (name, unique, origin) in list.Where(i => i.Origin != "pk"))
        {
            // Expression columns have a NULL name and are skipped.
            var columns = await ReadAsync(connection, $"PRAGMA index_info({Quote(name)})",
                r => r.IsDBNull(2) ? null : r.GetString(2), cancellationToken);
            indexes.Add(new IndexInfo(name, unique, columns.OfType<string>().ToList()));
        }
        return indexes;
    }

    private static async Task<IReadOnlyList<ForeignKeyInfo>> ReadForeignKeysAsync(SqliteConnection connection, string table, CancellationToken cancellationToken)
    {
        var rows = await ReadAsync(connection, $"PRAGMA foreign_key_list({Quote(table)})",
            r => (Id: r.GetInt32(0), Table: r.GetString(2), From: r.GetString(3), To: r.IsDBNull(4) ? null : r.GetString(4)), cancellationToken);
        var foreignKeys = new List<ForeignKeyInfo>();
        foreach (var group in rows.GroupBy(r => r.Id).OrderBy(g => g.Key))
        {
            var referenced = group.First().Table;
            // A NULL target column means the referenced table's primary key.
            var referencedColumns = group.All(r => r.To is not null)
                ? group.Select(r => r.To!).ToList()
                : (await ReadColumnsAsync(connection, referenced, cancellationToken)).Where(c => c.IsPrimaryKey).Select(c => c.Name).ToList();
            foreignKeys.Add(new ForeignKeyInfo($"fk_{table}_{group.Key}", group.Select(r => r.From).ToList(), Schema, referenced, referencedColumns));
        }
        return foreignKeys;
    }
}
