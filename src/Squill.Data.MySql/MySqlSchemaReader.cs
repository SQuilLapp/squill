using MySqlConnector;
using Squill.Core;

namespace Squill.Data.MySql;

/// <summary>Reads the schema of the connected MySQL or MariaDB database into a <see cref="SchemaSnapshot"/>.</summary>
internal static class MySqlSchemaReader
{
    private const string ColumnsSql = """
        select t.TABLE_NAME, t.TABLE_TYPE, c.COLUMN_NAME, c.COLUMN_TYPE, c.IS_NULLABLE, c.COLUMN_DEFAULT, c.COLUMN_KEY, c.EXTRA, v.VIEW_DEFINITION
        from information_schema.TABLES t
        join information_schema.COLUMNS c on c.TABLE_SCHEMA = t.TABLE_SCHEMA and c.TABLE_NAME = t.TABLE_NAME
        left join information_schema.VIEWS v on v.TABLE_SCHEMA = t.TABLE_SCHEMA and v.TABLE_NAME = t.TABLE_NAME
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
        select k.TABLE_NAME, k.CONSTRAINT_NAME, k.COLUMN_NAME, k.REFERENCED_TABLE_SCHEMA, k.REFERENCED_TABLE_NAME, k.REFERENCED_COLUMN_NAME,
               r.DELETE_RULE, r.UPDATE_RULE
        from information_schema.KEY_COLUMN_USAGE k
        join information_schema.REFERENTIAL_CONSTRAINTS r
          on r.CONSTRAINT_SCHEMA = k.CONSTRAINT_SCHEMA and r.CONSTRAINT_NAME = k.CONSTRAINT_NAME and r.TABLE_NAME = k.TABLE_NAME
        where k.TABLE_SCHEMA = database()
        order by k.TABLE_NAME, k.CONSTRAINT_NAME, k.ORDINAL_POSITION
        """;

    private const string RoutinesSql = """
        select ROUTINE_NAME, SPECIFIC_NAME, ROUTINE_TYPE, DTD_IDENTIFIER, ROUTINE_DEFINITION
        from information_schema.ROUTINES
        where ROUTINE_SCHEMA = database()
        order by ROUTINE_NAME
        """;

    // Position 0 is the return value of a function and is skipped.
    private const string ParametersSql = """
        select SPECIFIC_NAME, PARAMETER_MODE, PARAMETER_NAME, DTD_IDENTIFIER
        from information_schema.PARAMETERS
        where SPECIFIC_SCHEMA = database() and ORDINAL_POSITION > 0
        order by SPECIFIC_NAME, ORDINAL_POSITION
        """;

    private const string TriggersSql = """
        select TRIGGER_NAME, EVENT_OBJECT_TABLE, ACTION_TIMING, EVENT_MANIPULATION, ACTION_STATEMENT
        from information_schema.TRIGGERS
        where TRIGGER_SCHEMA = database()
        order by EVENT_OBJECT_TABLE, TRIGGER_NAME
        """;

    /// <summary>Reads the tables and views of the connected database.</summary>
    /// <param name="connection">Open connection to read from.</param>
    /// <param name="cancellationToken">Token that cancels the queries.</param>
    /// <returns>
    /// The schema snapshot; the database name is both the default schema and the schema of every table, routine and trigger.
    /// </returns>
    /// <remarks>
    /// Routine definitions are the body only (<c>ROUTINE_DEFINITION</c>), not a full <c>CREATE</c> statement: <c>show create</c> is too slow for
    /// many routines, so a script action wraps the body in a header built from the signature.
    /// </remarks>
    public static async ValueTask<SchemaSnapshot> ReadAsync(MySqlConnection connection, CancellationToken cancellationToken)
    {
        var database = (await ReadAsync(connection, "select database()", r => r.IsDBNull(0) ? null : r.GetString(0), cancellationToken))[0];
        var columns = (await ReadAsync(connection, ColumnsSql, r => (
            Table: r.GetString(0), Kind: r.GetString(1) == "VIEW" ? TableKind.View : TableKind.Table,
            Definition: r.IsDBNull(8) ? null : r.GetString(8),
            Column: new ColumnDefinition(r.GetString(2), r.GetString(3), r.GetString(4) == "YES", r.GetString(6) == "PRI", r.IsDBNull(5) ? null : r.GetString(5),
                r.GetString(7).Contains("auto_increment", StringComparison.OrdinalIgnoreCase),
                r.GetString(7).Contains(" GENERATED", StringComparison.OrdinalIgnoreCase))), cancellationToken))
            .GroupBy(c => (c.Table, c.Kind, c.Definition))
            .ToList();
        var indexes = (await ReadAsync(connection, IndexesSql, r => (
            Table: r.GetString(0), Name: r.GetString(1), Unique: r.GetInt64(2) == 0, Column: r.GetString(3)), cancellationToken))
            .GroupBy(i => (i.Table, i.Name, i.Unique))
            .ToLookup(g => g.Key.Table, g => new IndexInfo(g.Key.Name, g.Key.Unique, g.Select(i => i.Column).ToList()));
        var foreignKeys = (await ReadAsync(connection, ForeignKeysSql, r => (
            Table: r.GetString(0), Name: r.GetString(1), Column: r.GetString(2),
            RefSchema: r.GetString(3), RefTable: r.GetString(4), RefColumn: r.GetString(5),
            OnDelete: ReferentialActions.Parse(r.GetString(6)) ?? ReferentialAction.NoAction,
            OnUpdate: ReferentialActions.Parse(r.GetString(7)) ?? ReferentialAction.NoAction), cancellationToken))
            .GroupBy(f => (f.Table, f.Name, f.RefSchema, f.RefTable, f.OnDelete, f.OnUpdate))
            .ToLookup(g => g.Key.Table, g => new ForeignKeyInfo(g.Key.Name, g.Select(f => f.Column).ToList(), g.Key.RefSchema, g.Key.RefTable, g.Select(f => f.RefColumn).ToList(), g.Key.OnDelete, g.Key.OnUpdate));

        var tables = columns
            .Select(g => new TableInfo(database!, g.Key.Table, g.Key.Kind,
                g.Select(c => c.Column).ToList(), indexes[g.Key.Table].ToList(), foreignKeys[g.Key.Table].ToList(), null, g.Key.Definition))
            .ToList();
        var parameters = (await ReadAsync(connection, ParametersSql, r => (
            Routine: r.GetString(0), Mode: r.IsDBNull(1) ? null : r.GetString(1), Name: r.GetString(2), Type: r.GetString(3)), cancellationToken))
            .ToLookup(p => p.Routine, p => (p.Mode is "OUT" or "INOUT" ? p.Mode + " " : "") + p.Name + " " + p.Type);
        var routines = await ReadAsync(connection, RoutinesSql, r => new RoutineInfo(
            database!, r.GetString(0), r.GetString(2) == "PROCEDURE" ? RoutineKind.Procedure : RoutineKind.Function,
            $"{r.GetString(0)}({string.Join(", ", parameters[r.GetString(1)])})",
            r.IsDBNull(3) ? null : r.GetString(3), r.IsDBNull(4) ? null : r.GetString(4)), cancellationToken);
        var triggers = await ReadAsync(connection, TriggersSql, r => new TriggerInfo(
            database!, r.GetString(1), r.GetString(0), r.GetString(2), r.GetString(3), r.GetString(4)), cancellationToken);
        return new SchemaSnapshot(database, tables) { Routines = routines, Triggers = triggers };
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
