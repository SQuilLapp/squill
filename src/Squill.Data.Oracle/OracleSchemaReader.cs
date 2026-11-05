using System.Globalization;
using Squill.Core;

namespace Squill.Data.Oracle;

/// <summary>Reads tables, views, columns and primary keys of the non-system schemas into a <see cref="SchemaSnapshot"/> (indexes, foreign keys, routines and triggers are not read yet).</summary>
internal static class OracleSchemaReader
{
    /// <summary>Predicate on a user-name column that drops Oracle's own schemas; a fixed list because <c>oracle_maintained</c> only exists from Oracle 12c.</summary>
    internal const string SystemUsers = "('SYS', 'SYSTEM', 'SYSMAN', 'DBSNMP', 'OUTLN', 'XDB', 'CTXSYS', 'MDSYS', 'ORDSYS', 'ORDDATA', 'ORDPLUGINS', 'SI_INFORMTN_SCHEMA', 'WMSYS', 'EXFSYS', 'LBACSYS', 'DVSYS', 'OLAPSYS', 'APPQOSSYS', 'AUDSYS', 'GSMADMIN_INTERNAL', 'OJVMSYS', 'DIP', 'ANONYMOUS', 'XS$NULL', 'MDDATA', 'SPATIAL_CSW_ADMIN_USR', 'SPATIAL_WFS_ADMIN_USR', 'FLOWS_FILES', 'DBSFWUSER', 'REMOTE_SCHEDULER_AGENT', 'SYS$UMF', 'GGSYS', 'GSMCATUSER', 'GSMUSER', 'PUBLIC')";

    private const string UserSchemas = $"owner not in {SystemUsers} and owner not like 'APEX%' and owner not like 'FLOWS%'";

    private const string RelationsSql = $"""
        select owner, object_name, object_type from all_objects
        where object_type in ('TABLE', 'VIEW') and object_name not like 'BIN$%' and {UserSchemas}
        order by owner, object_name
        """;

    private const string ColumnsSql = $"""
        select owner, table_name, column_name, data_type, char_length, data_precision, data_scale, nullable
        from all_tab_columns where {UserSchemas}
        order by owner, table_name, column_id
        """;

    private const string PrimaryKeysSql = $"""
        select c.owner, c.table_name, c.constraint_name, cc.column_name
        from all_constraints c
        join all_cons_columns cc on cc.owner = c.owner and cc.constraint_name = c.constraint_name
        where c.constraint_type = 'P' and c.owner not in {SystemUsers} and c.owner not like 'APEX%' and c.owner not like 'FLOWS%'
        """;

    /// <summary>Reads the snapshot over an open connection.</summary>
    public static async ValueTask<SchemaSnapshot> ReadAsync(IDatabaseConnection connection, CancellationToken cancellationToken)
    {
        var currentSchema = Text((await QueryAsync(connection, "select sys_context('USERENV', 'CURRENT_SCHEMA') from dual", cancellationToken)).FirstOrDefault()?[0]);

        var primaryKeys = new Dictionary<(string, string), (string Name, HashSet<string> Columns)>();
        foreach (var r in await QueryAsync(connection, PrimaryKeysSql, cancellationToken))
        {
            var key = (Text(r[0])!, Text(r[1])!);
            if (!primaryKeys.TryGetValue(key, out var pk))
                primaryKeys[key] = pk = (Text(r[2])!, []);
            pk.Columns.Add(Text(r[3])!);
        }

        var columns = new Dictionary<(string, string), List<ColumnDefinition>>();
        foreach (var r in await QueryAsync(connection, ColumnsSql, cancellationToken))
        {
            var key = (Text(r[0])!, Text(r[1])!);
            if (!columns.TryGetValue(key, out var list))
                columns[key] = list = [];
            var name = Text(r[2])!;
            list.Add(new ColumnDefinition(name, DataType(Text(r[3])!, r[4], r[5], r[6]), Text(r[7]) == "Y",
                primaryKeys.TryGetValue(key, out var pk) && pk.Columns.Contains(name), null));
        }

        List<TableInfo> tables = [];
        foreach (var r in await QueryAsync(connection, RelationsSql, cancellationToken))
        {
            var key = (Text(r[0])!, Text(r[1])!);
            tables.Add(new TableInfo(key.Item1, key.Item2, Text(r[2]) == "VIEW" ? TableKind.View : TableKind.Table,
                columns.GetValueOrDefault(key) ?? [], [], [], primaryKeys.TryGetValue(key, out var pk) ? pk.Name : null));
        }
        return new SchemaSnapshot(currentSchema, tables);
    }

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
