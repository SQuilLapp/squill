using Microsoft.Data.Sqlite;
using Squill.Core;

namespace Squill.Data.Sqlite;

/// <summary>Reads the schema of a SQLite database into a <see cref="SchemaSnapshot"/>.</summary>
internal static class SqliteSchemaReader
{
    private const string Schema = "main";

    /// <summary>Reads the user-visible schema objects of the connected database.</summary>
    /// <param name="connection">Open connection to read from.</param>
    /// <param name="cancellationToken">Token that cancels the queries.</param>
    /// <returns>The schema snapshot.</returns>
    public static async ValueTask<SchemaSnapshot> ReadAsync(SqliteConnection connection, CancellationToken cancellationToken)
    {
        var objects = await ReadAsync(connection, "SELECT type, name, tbl_name, sql FROM sqlite_master WHERE type IN ('view','trigger')",
            r => (Type: r.GetString(0), Name: r.GetString(1), Table: r.GetString(2), Sql: r.IsDBNull(3) ? null : r.GetString(3)), cancellationToken);
        var viewSql = objects.Where(o => o.Type == "view").ToDictionary(o => o.Name, o => o.Sql, StringComparer.Ordinal);

        // pragma_table_list marks FTS and other shadow tables as 'shadow', so only real tables, virtual tables and views remain.
        var tables = new List<TableInfo>();
        foreach (var (name, kind) in await ReadAsync(connection, """SELECT name, type FROM pragma_table_list WHERE schema = 'main' AND type IN ('table','view','virtual') AND name NOT LIKE 'sqlite\_%' ESCAPE '\' ORDER BY name""",
                     r => (r.GetString(0), r.GetString(1) == "view" ? TableKind.View : TableKind.Table), cancellationToken))
        {
            var columns = await ReadColumnsAsync(connection, name, cancellationToken);
            var indexes = kind == TableKind.Table ? await ReadIndexesAsync(connection, name, cancellationToken) : [];
            var foreignKeys = kind == TableKind.Table ? await ReadForeignKeysAsync(connection, name, cancellationToken) : [];
            var definition = kind == TableKind.View && viewSql.TryGetValue(name, out var sql) && sql is not null ? ViewBody(sql) : null;
            tables.Add(new TableInfo(Schema, name, kind, columns, indexes, foreignKeys, Definition: definition));
        }

        var triggers = objects.Where(o => o.Type == "trigger" && o.Sql is not null)
            .Select(o => { var (timing, events) = TriggerHeader(o.Sql!); return new TriggerInfo(Schema, o.Table, o.Name, timing, events, o.Sql); })
            .OrderBy(t => t.Table, StringComparer.Ordinal).ThenBy(t => t.Name, StringComparer.Ordinal)
            .ToList();
        return new SchemaSnapshot(Schema, tables) { Triggers = triggers };
    }

    /// <summary>Splits a stored statement into top-level words, quoted identifiers and single punctuation characters (parentheses nest, their content is skipped).</summary>
    private static List<(string Text, int Start, int End)> Tokens(string sql)
    {
        var tokens = new List<(string, int, int)>();
        var depth = 0;
        for (var i = 0; i < sql.Length;)
        {
            var c = sql[i];
            if (char.IsWhiteSpace(c)) { i++; continue; }
            var start = i;
            var atTop = depth == 0;
            if (c is '"' or '\'' or '`' or '[')
            {
                var close = c == '[' ? ']' : c;
                for (i++; i < sql.Length; i++)
                {
                    if (sql[i] != close) continue;
                    if (close != ']' && i + 1 < sql.Length && sql[i + 1] == close) { i++; continue; } // doubled quote
                    break;
                }
                i++;
            }
            else if (char.IsLetterOrDigit(c) || c == '_')
            {
                while (i < sql.Length && (char.IsLetterOrDigit(sql[i]) || sql[i] == '_')) i++;
            }
            else
            {
                i++;
                if (c == '(') depth++;
                else if (c == ')') depth--;
            }
            if (atTop) tokens.Add((sql[start..Math.Min(i, sql.Length)], start, Math.Min(i, sql.Length)));
        }
        return tokens;
    }

    private static bool Is(string token, string word) => token.Equals(word, StringComparison.OrdinalIgnoreCase);

    /// <summary>Returns the SELECT after the top-level AS of a stored CREATE VIEW, or the whole statement when no AS is found.</summary>
    internal static string ViewBody(string sql)
    {
        var tokens = Tokens(sql);
        // shortcut: the first top-level AS ends the header; a view named AS must be quoted, which Tokens keeps apart.
        var at = tokens.FindIndex(t => Is(t.Text, "AS"));
        return at < 0 || at + 1 >= tokens.Count ? sql : sql[tokens[at + 1].Start..].TrimEnd().TrimEnd(';').TrimEnd();
    }

    /// <summary>Reads the timing (BEFORE by default) and the single event of a stored CREATE TRIGGER.</summary>
    internal static (string Timing, string Events) TriggerHeader(string sql)
    {
        var words = Tokens(sql).Select(t => t.Text).ToList();
        var i = words.FindIndex(w => Is(w, "TRIGGER")) + 1;
        if (i > 0 && i + 2 < words.Count && Is(words[i], "IF") && Is(words[i + 1], "NOT") && Is(words[i + 2], "EXISTS")) i += 3;
        if (i > 0) i++; // trigger name
        if (i < words.Count && words[i] == ".") i += 2;
        var timing = "BEFORE";
        if (i < words.Count && Is(words[i], "INSTEAD")) { timing = "INSTEAD OF"; i += 2; }
        else if (i < words.Count && (Is(words[i], "BEFORE") || Is(words[i], "AFTER"))) { timing = words[i].ToUpperInvariant(); i++; }
        var events = i >= 0 && i < words.Count ? words[i].ToUpperInvariant() : "";
        return (timing, events);
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
        var visible = rows.Where(c => c.Hidden == 0).ToList();
        var singleKey = visible.Count(c => c.Pk > 0) == 1;
        // A lone INTEGER primary key is a rowid alias, which SQLite generates itself.
        return visible
            .Select(c => new ColumnDefinition(c.Name, c.Type, !c.NotNull, c.Pk > 0, c.Default,
                singleKey && c.Pk > 0 && c.Type.Trim().Equals("INTEGER", StringComparison.OrdinalIgnoreCase)))
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
            r => (Id: r.GetInt32(0), Table: r.GetString(2), From: r.GetString(3), To: r.IsDBNull(4) ? null : r.GetString(4),
                  OnUpdate: r.GetString(5), OnDelete: r.GetString(6)), cancellationToken);
        var foreignKeys = new List<ForeignKeyInfo>();
        foreach (var group in rows.GroupBy(r => r.Id).OrderBy(g => g.Key))
        {
            var referenced = group.First().Table;
            // A NULL target column means the referenced table's primary key.
            var referencedColumns = group.All(r => r.To is not null)
                ? group.Select(r => r.To!).ToList()
                : (await ReadColumnsAsync(connection, referenced, cancellationToken)).Where(c => c.IsPrimaryKey).Select(c => c.Name).ToList();
            foreignKeys.Add(new ForeignKeyInfo($"fk_{table}_{group.Key}", group.Select(r => r.From).ToList(), Schema, referenced, referencedColumns,
                ReferentialActions.Parse(group.First().OnDelete) ?? ReferentialAction.NoAction,
                ReferentialActions.Parse(group.First().OnUpdate) ?? ReferentialAction.NoAction));
        }
        return foreignKeys;
    }
}
