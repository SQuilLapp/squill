using System.Globalization;
using System.Text;
using Squill.Core;

namespace Squill.Data.Oracle;

/// <summary>Reads tables, views, materialized views, columns, defaults, identities, indexes, foreign keys, routines and triggers of the non-system schemas into a <see cref="SchemaSnapshot"/>.</summary>
internal static class OracleSchemaReader
{
    private const string SystemUsers = "('SYS', 'SYSTEM', 'SYSMAN', 'DBSNMP', 'OUTLN', 'XDB', 'CTXSYS', 'MDSYS', 'ORDSYS', 'ORDDATA', 'ORDPLUGINS', 'SI_INFORMTN_SCHEMA', 'WMSYS', 'EXFSYS', 'LBACSYS', 'DVSYS', 'OLAPSYS', 'APPQOSSYS', 'AUDSYS', 'GSMADMIN_INTERNAL', 'OJVMSYS', 'DIP', 'ANONYMOUS', 'XS$NULL', 'MDDATA', 'SPATIAL_CSW_ADMIN_USR', 'SPATIAL_WFS_ADMIN_USR', 'FLOWS_FILES', 'DBSFWUSER', 'REMOTE_SCHEDULER_AGENT', 'SYS$UMF', 'GGSYS', 'GSMCATUSER', 'GSMUSER', 'PUBLIC')";

    /// <summary>
    /// A predicate that keeps only the user schemas in <paramref name="column"/>: the connected user's own schema, plus everything that is not
    /// a known Oracle system schema. <c>all_users.oracle_maintained</c> is not used: it is absent before 12c and was found unset (no row
    /// with 'N') on a 19c server, which hid every table.
    /// </summary>
    internal static string UserFilter(string column) =>
        $"({column} = sys_context('USERENV', 'CURRENT_SCHEMA') or ({column} not in {SystemUsers} and {column} not like 'APEX%' and {column} not like 'FLOWS%'))";

    private static string RelationsSql() => $"""
        select owner, object_name, object_type from all_objects
        where object_type in ('TABLE', 'VIEW') and object_name not like 'BIN$%' and {UserFilter("owner")}
        order by owner, object_name
        """;

    private static string ColumnsSql() => $"""
        select owner, table_name, column_name, data_type, char_length, data_precision, data_scale, nullable
        from all_tab_columns where {UserFilter("owner")}
        order by owner, table_name, column_id
        """;

    private static string PrimaryKeysSql() => $"""
        select c.owner, c.table_name, c.constraint_name, cc.column_name
        from all_constraints c
        join all_cons_columns cc on cc.owner = c.owner and cc.constraint_name = c.constraint_name
        where c.constraint_type = 'P' and {UserFilter("c.owner")}
        """;

    // Indexes that back the primary key are skipped (the key is already on the columns), as are the hidden columns of function-based indexes.
    private static string IndexesSql() => $"""
        select i.table_owner, i.table_name, i.index_name, i.uniqueness, ic.column_name
        from all_indexes i
        join all_ind_columns ic on ic.index_owner = i.owner and ic.index_name = i.index_name
        where i.table_type = 'TABLE' and i.index_type <> 'LOB' and i.index_name not like 'BIN$%' and {UserFilter("i.table_owner")}
          and ic.column_name not like 'SYS\_NC%' escape '\'
          and not exists (select 1 from all_constraints x where x.owner = i.owner and x.index_name = i.index_name and x.constraint_type = 'P')
        order by i.table_owner, i.table_name, i.index_name, ic.column_position
        """;

    private static string ForeignKeysSql() => $"""
        select c.owner, c.table_name, c.constraint_name, cc.column_name, r.owner, r.table_name, rc.column_name, c.delete_rule
        from all_constraints c
        join all_cons_columns cc on cc.owner = c.owner and cc.constraint_name = c.constraint_name
        join all_constraints r on r.owner = c.r_owner and r.constraint_name = c.r_constraint_name
        join all_cons_columns rc on rc.owner = r.owner and rc.constraint_name = r.constraint_name and rc.position = cc.position
        where c.constraint_type = 'R' and {UserFilter("c.owner")}
        order by c.owner, c.table_name, c.constraint_name, cc.position
        """;

    // data_default is a LONG: it is read in its own query, and filtered in code because a LONG cannot be tested in the where clause.
    private static string DefaultsSql() => $"select owner, table_name, column_name, data_default from all_tab_columns where {UserFilter("owner")}";

    private static string IdentitiesSql() => $"select owner, table_name, column_name from all_tab_identity_cols where {UserFilter("owner")}";

    // text / query are LONGs: each is the last (and only LONG) column of its own query.
    private static string ViewsSql() => $"select owner, view_name, text from all_views where {UserFilter("owner")}";

    private static string MaterializedViewsSql() => $"select owner, mview_name, query from all_mviews where {UserFilter("owner")}";

    // Standalone functions and procedures, and package specifications (a package is listed as one routine).
    private static string RoutinesSql() => $"""
        select owner, object_name, object_type from all_procedures
        where procedure_name is null and object_type in ('FUNCTION', 'PROCEDURE', 'PACKAGE') and {UserFilter("owner")}
        order by owner, object_name
        """;

    // Position 0 is the return value of a function.
    private static string ArgumentsSql() => $"""
        select owner, object_name, argument_name, in_out, coalesce(data_type, type_name), position
        from all_arguments
        where package_name is null and data_level = 0 and (data_type is not null or type_name is not null) and {UserFilter("owner")}
        order by owner, object_name, position
        """;

    // The source of routines and triggers; all_source avoids the LONG body of all_triggers.
    private static string SourceSql() => $"""
        select owner, name, type, text from all_source
        where type in ('FUNCTION', 'PROCEDURE', 'PACKAGE', 'TRIGGER') and {UserFilter("owner")}
        order by owner, name, type, line
        """;

    private static string TriggersSql() => $"""
        select owner, table_owner, table_name, trigger_name, trigger_type, triggering_event from all_triggers
        where base_object_type in ('TABLE', 'VIEW') and {UserFilter("table_owner")}
        order by table_owner, table_name, trigger_name
        """;

    /// <summary>Reads the snapshot over an open connection.</summary>
    /// <remarks>Routine and trigger definitions are the <c>all_source</c> text prefixed with <c>CREATE OR REPLACE</c>; a package is listed as one routine (its specification).</remarks>
    public static async ValueTask<SchemaSnapshot> ReadAsync(IDatabaseConnection connection, CancellationToken cancellationToken)
    {
        var currentSchema = Text((await QueryAsync(connection, "select sys_context('USERENV', 'CURRENT_SCHEMA') from dual", cancellationToken)).FirstOrDefault()?[0]);

        var primaryKeys = new Dictionary<(string, string), (string Name, HashSet<string> Columns)>();
        foreach (var r in await QueryAsync(connection, PrimaryKeysSql(), cancellationToken))
        {
            var key = (Text(r[0])!, Text(r[1])!);
            if (!primaryKeys.TryGetValue(key, out var pk))
                primaryKeys[key] = pk = (Text(r[2])!, []);
            pk.Columns.Add(Text(r[3])!);
        }

        var defaults = new Dictionary<(string, string, string), string>();
        foreach (var r in await QueryAsync(connection, DefaultsSql(), cancellationToken))
        {
            if (Text(r[3])?.Trim() is { Length: > 0 } text && !text.Equals("NULL", StringComparison.OrdinalIgnoreCase))
                defaults[(Text(r[0])!, Text(r[1])!, Text(r[2])!)] = text;
        }
        var identities = (await QueryAsync(connection, IdentitiesSql(), cancellationToken)).Select(r => (Text(r[0])!, Text(r[1])!, Text(r[2])!)).ToHashSet();

        var columns = new Dictionary<(string, string), List<ColumnDefinition>>();
        foreach (var r in await QueryAsync(connection, ColumnsSql(), cancellationToken))
        {
            var key = (Text(r[0])!, Text(r[1])!);
            if (!columns.TryGetValue(key, out var list))
                columns[key] = list = [];
            var name = Text(r[2])!;
            var identity = identities.Contains((key.Item1, key.Item2, name));
            list.Add(new ColumnDefinition(name, DataType(Text(r[3])!, r[4], r[5], r[6]), Text(r[7]) == "Y",
                primaryKeys.TryGetValue(key, out var pk) && pk.Columns.Contains(name),
                identity ? null : defaults.GetValueOrDefault((key.Item1, key.Item2, name)), identity));
        }

        var indexes = (await QueryAsync(connection, IndexesSql(), cancellationToken))
            .GroupBy(r => (Owner: Text(r[0])!, Table: Text(r[1])!, Name: Text(r[2])!, Unique: Text(r[3]) == "UNIQUE"))
            .ToLookup(g => (g.Key.Owner, g.Key.Table), g => new IndexInfo(g.Key.Name, g.Key.Unique, g.Select(r => Text(r[4])!).ToList()));
        var foreignKeys = (await QueryAsync(connection, ForeignKeysSql(), cancellationToken))
            .GroupBy(r => (Owner: Text(r[0])!, Table: Text(r[1])!, Name: Text(r[2])!, RefOwner: Text(r[4])!, RefTable: Text(r[5])!, Delete: Text(r[7])))
            .ToLookup(g => (g.Key.Owner, g.Key.Table), g => new ForeignKeyInfo(g.Key.Name, g.Select(r => Text(r[3])!).ToList(), g.Key.RefOwner, g.Key.RefTable,
                g.Select(r => Text(r[6])!).ToList(), ReferentialActions.Parse(g.Key.Delete) ?? ReferentialAction.NoAction));

        var definitions = new Dictionary<(string, string), string>();
        foreach (var r in await QueryAsync(connection, ViewsSql(), cancellationToken))
            definitions[(Text(r[0])!, Text(r[1])!)] = Text(r[2])?.Trim() ?? "";
        var materialized = new HashSet<(string, string)>();
        foreach (var r in await QueryAsync(connection, MaterializedViewsSql(), cancellationToken))
        {
            materialized.Add((Text(r[0])!, Text(r[1])!));
            definitions[(Text(r[0])!, Text(r[1])!)] = Text(r[2])?.Trim() ?? "";
        }

        List<TableInfo> tables = [];
        foreach (var r in await QueryAsync(connection, RelationsSql(), cancellationToken))
        {
            var key = (Text(r[0])!, Text(r[1])!);
            // A materialized view is listed by all_objects as its container table.
            var kind = materialized.Contains(key) ? TableKind.MaterializedView : Text(r[2]) == "VIEW" ? TableKind.View : TableKind.Table;
            tables.Add(new TableInfo(key.Item1, key.Item2, kind, columns.GetValueOrDefault(key) ?? [], indexes[key].ToList(), foreignKeys[key].ToList(),
                primaryKeys.TryGetValue(key, out var pk) ? pk.Name : null, kind == TableKind.Table ? null : definitions.GetValueOrDefault(key)));
        }

        var sources = new Dictionary<(string, string, string), StringBuilder>();
        foreach (var r in await QueryAsync(connection, SourceSql(), cancellationToken))
        {
            var key = (Text(r[0])!, Text(r[1])!, Text(r[2])!);
            if (!sources.TryGetValue(key, out var text))
                sources[key] = text = new StringBuilder("CREATE OR REPLACE ");
            text.Append(Text(r[3]));
        }

        string? Source(string owner, string name, string type) => sources.TryGetValue((owner, name, type), out var text) ? text.ToString().TrimEnd() : null;

        var arguments = (await QueryAsync(connection, ArgumentsSql(), cancellationToken)).ToLookup(r => (Text(r[0])!, Text(r[1])!));
        var routines = (await QueryAsync(connection, RoutinesSql(), cancellationToken)).Select(r =>
        {
            string owner = Text(r[0])!, name = Text(r[1])!, type = Text(r[2])!;
            var args = arguments[(owner, name)].ToList();
            var parameters = args.Where(a => Int(a[5]) > 0).Select(a => (Text(a[3]) is "OUT" or "IN/OUT" ? Text(a[3]) + " " : "") + Text(a[2]) + " " + Text(a[4]));
            return new RoutineInfo(owner, name, type == "FUNCTION" ? RoutineKind.Function : RoutineKind.Procedure,
                type == "PACKAGE" ? name : $"{name}({string.Join(", ", parameters)})",
                args.FirstOrDefault(a => Int(a[5]) == 0) is { } ret ? Text(ret[4]) : null, Source(owner, name, type));
        }).ToList();
        var triggers = (await QueryAsync(connection, TriggersSql(), cancellationToken)).Select(r =>
            new TriggerInfo(Text(r[1])!, Text(r[2])!, Text(r[3])!, Timing(Text(r[4])!), Text(r[5])!, Source(Text(r[0])!, Text(r[3])!, "TRIGGER"))).ToList();
        return new SchemaSnapshot(currentSchema, tables) { Routines = routines, Triggers = triggers };
    }

    // "BEFORE EACH ROW", "AFTER STATEMENT", "INSTEAD OF", "COMPOUND": keep the timing part.
    private static string Timing(string type) => type.Split(" EACH", 2)[0].Split(" STATEMENT", 2)[0].Split(" EVENT", 2)[0];

    private static async Task<List<object?[]>> QueryAsync(IDatabaseConnection connection, string sql, CancellationToken cancellationToken)
    {
        await using var reader = await connection.ExecuteAsync(sql, [], cancellationToken);
        return await reader.ReadAllAsync(1000, cancellationToken);
    }

    private static string? Text(object? value) => Convert.ToString(value, CultureInfo.InvariantCulture);

    private static int? Int(object? value) => value is null ? null : Convert.ToInt32(value, CultureInfo.InvariantCulture);

    // NUMBER(p,s), VARCHAR2(n) and friends; other types keep Oracle's own name (DATE, CLOB, TIMESTAMP(6)...).
    private static string DataType(string type, object? charLength, object? precision, object? scale) => type switch
    {
        "VARCHAR2" or "NVARCHAR2" or "CHAR" or "NCHAR" or "RAW" when Int(charLength) is > 0 and var n => $"{type}({n})",
        "NUMBER" when Int(precision) is { } p => Int(scale) is { } s and not 0 ? $"NUMBER({p},{s})" : $"NUMBER({p})",
        _ => type,
    };
}
