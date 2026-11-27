using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Squill.Core;

namespace Squill.Data.Informix;

/// <summary>Reads tables, views, columns, keys, indexes, routines and triggers of the current database into a <see cref="SchemaSnapshot"/> from the system catalogue (plain SQL, no <c>SQLTables</c>).</summary>
/// <remarks>
/// Informix has owners but no schemas: the schema of a table is its owner. Column defaults, index sort order and
/// <c>ON UPDATE</c> actions are not read (the catalogue has no update rule, and <see cref="IndexInfo"/> has no sort order).
/// </remarks>
internal static partial class InformixSchemaReader
{
    private const string UserTables = "tabid >= 100 and tabtype in ('T', 'V')";

    private const string PartColumns = "part1, part2, part3, part4, part5, part6, part7, part8, part9, part10, part11, part12, part13, part14, part15, part16";

    /// <summary>Reads the snapshot over an open connection.</summary>
    /// <param name="connection">An open connection to the database to describe.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>Every user table and view, and the user routines and triggers.</returns>
    public static async ValueTask<SchemaSnapshot> ReadAsync(IDatabaseConnection connection, CancellationToken cancellationToken)
    {
        var user = Text((await QueryAsync(connection, "select trim(user) from systables where tabid = 1", cancellationToken)).FirstOrDefault()?[0]);
        var relations = await QueryAsync(connection, $"select tabid, trim(owner), trim(tabname), tabtype from systables where {UserTables} order by owner, tabname", cancellationToken);
        var columnRows = await QueryAsync(connection,
            $"select tabid, colno, trim(colname), coltype, collength, extended_id from syscolumns where tabid in (select tabid from systables where {UserTables}) order by tabid, colno", cancellationToken);
        var constraints = (await QueryAsync(connection, "select constrid, tabid, trim(constrname), constrtype, trim(idxname) from sysconstraints where constrtype in ('P', 'U', 'R')", cancellationToken))
            .ToDictionary(r => Int(r[0]), r => (TabId: Int(r[1]), Name: Text(r[2])!, Type: Text(r[3])!.Trim(), Index: Text(r[4])));
        var indexRows = await QueryAsync(connection, $"select tabid, trim(idxname), idxtype, {PartColumns} from sysindexes where tabid in (select tabid from systables where {UserTables})", cancellationToken);
        var references = (await QueryAsync(connection, "select constrid, primary, ptabid, delrule from sysreferences", cancellationToken))
            .ToDictionary(r => Int(r[0]), r => (Parent: Int(r[1]), ParentTab: Int(r[2]), Delete: Text(r[3])?.Trim()));

        var columnNames = columnRows.ToDictionary(r => (Int(r[0]), Int(r[1])), r => Text(r[2])!);
        var indexes = new Dictionary<(int, string), (bool Unique, List<string> Columns)>();
        foreach (var r in indexRows)
        {
            var tab = Int(r[0]);
            var columns = new List<string>();
            for (var i = 3; i < 3 + 16 && r[i] is not null && Int(r[i]) != 0; i++)
                columns.Add(columnNames[(tab, Math.Abs(Int(r[i])))]);
            indexes[(tab, Text(r[1])!)] = (Text(r[2])!.Trim().Equals("U", StringComparison.OrdinalIgnoreCase), columns);
        }

        List<string> KeyColumns(int constraintId) =>
            constraints.TryGetValue(constraintId, out var c) && c.Index is { } idx && indexes.TryGetValue((c.TabId, idx), out var index) ? index.Columns : [];

        var owners = relations.ToDictionary(r => Int(r[0]), r => Text(r[1])!);
        var names = relations.ToDictionary(r => Int(r[0]), r => Text(r[2])!);
        var primaryKeys = constraints.Where(c => c.Value.Type == "P").ToDictionary(c => c.Value.TabId, c => (c.Value.Name, Columns: KeyColumns(c.Key)));
        // The index behind a primary or foreign key is part of the constraint, not a separate index.
        var backing = constraints.Values.Where(c => c.Type is "P" or "R" && c.Index is not null).Select(c => (c.TabId, c.Index!)).ToHashSet();
        var uniqueNames = constraints.Values.Where(c => c.Type == "U" && c.Index is not null).ToDictionary(c => (c.TabId, c.Index!), c => c.Name);

        var columnsByTable = columnRows.GroupBy(r => Int(r[0])).ToDictionary(g => g.Key, g => g.Select(r =>
        {
            var coltype = Int(r[3]);
            var name = Text(r[2])!;
            return new ColumnDefinition(name, InformixColumnType.Decode(coltype, Int(r[4]), Int(r[5])), !InformixColumnType.IsNotNull(coltype),
                primaryKeys.TryGetValue(g.Key, out var pk) && pk.Columns.Contains(name), null, InformixColumnType.IsSerial(coltype));
        }).ToList());

        var viewText = Chunks(await QueryAsync(connection, "select tabid, viewtext from sysviews order by tabid, seqno", cancellationToken));

        var foreignKeys = constraints.Where(c => c.Value.Type == "R" && references.ContainsKey(c.Key)).Select(c =>
        {
            var reference = references[c.Key];
            var own = c.Value.Index is { } idx && indexes.TryGetValue((c.Value.TabId, idx), out var index) ? index.Columns : [];
            return (c.Value.TabId, Key: new ForeignKeyInfo(c.Value.Name, own, owners.GetValueOrDefault(reference.ParentTab), names.GetValueOrDefault(reference.ParentTab, ""), KeyColumns(reference.Parent),
                reference.Delete is "C" or "c" ? ReferentialAction.Cascade : ReferentialAction.NoAction));
        }).ToLookup(f => f.TabId, f => f.Key);

        var tables = relations.Select(r =>
        {
            var id = Int(r[0]);
            var isView = Text(r[3])!.Trim() == "V";
            var tableIndexes = indexes.Where(i => i.Key.Item1 == id && !backing.Contains(i.Key))
                .Select(i => new IndexInfo(uniqueNames.GetValueOrDefault(i.Key, i.Key.Item2), i.Value.Unique, i.Value.Columns)).ToList();
            return new TableInfo(Text(r[1]), Text(r[2])!, isView ? TableKind.View : TableKind.Table, columnsByTable.GetValueOrDefault(id) ?? [], tableIndexes,
                foreignKeys[id].ToList(), primaryKeys.TryGetValue(id, out var pk) ? pk.Name : null, isView ? viewText.GetValueOrDefault(id)?.Trim() : null);
        }).ToList();

        return new SchemaSnapshot(user, tables)
        {
            Routines = await ReadRoutinesAsync(connection, cancellationToken),
            Triggers = await ReadTriggersAsync(connection, owners, names, cancellationToken),
        };
    }

    // Catalogue text is stored in fixed-size chunks that must be joined unchanged, in order (the rows arrive ordered by seqno).
    private static Dictionary<int, string> Chunks(List<object?[]> rows)
    {
        var text = new Dictionary<int, StringBuilder>();
        foreach (var r in rows)
        {
            if (!text.TryGetValue(Int(r[0]), out var builder))
                text[Int(r[0])] = builder = new StringBuilder();
            builder.Append(Text(r[1]));
        }

        return text.ToDictionary(t => t.Key, t => t.Value.ToString());
    }

    private static async Task<List<RoutineInfo>> ReadRoutinesAsync(IDatabaseConnection connection, CancellationToken cancellationToken)
    {
        var bodies = Chunks(await QueryAsync(connection, "select procid, data from sysprocbody where datakey = 'T' order by procid, seqno", cancellationToken));
        var rows = await QueryAsync(connection, "select procid, trim(owner), trim(procname), isproc, paramtypes from sysprocedures where mode in ('O', 'D', 'R', 'T') order by owner, procname", cancellationToken);
        return [.. rows.Select(r =>
        {
            var name = Text(r[2])!;
            var isFunction = Text(r[3])?.Trim() is "f" or "F";
            var definition = bodies.GetValueOrDefault(Int(r[0]))?.Trim();
            return new RoutineInfo(Text(r[1])!, name, isFunction ? RoutineKind.Function : RoutineKind.Procedure, $"{name}({Text(r[4])})", isFunction ? ReturnType(definition) : null, definition);
        })];
    }

    private static async Task<List<TriggerInfo>> ReadTriggersAsync(IDatabaseConnection connection, Dictionary<int, string> owners, Dictionary<int, string> names, CancellationToken cancellationToken)
    {
        // datakey 'D' is the CREATE TRIGGER header and 'A' the triggered actions (with the BEFORE / AFTER keyword); the other keys are compiled code.
        var headers = Chunks(await QueryAsync(connection, "select trigid, data from systrigbody where datakey = 'D' order by trigid, seqno", cancellationToken));
        var actions = Chunks(await QueryAsync(connection, "select trigid, data from systrigbody where datakey = 'A' order by trigid, seqno", cancellationToken));
        return [.. (await QueryAsync(connection, "select trigid, trim(trigname), tabid, event from systriggers order by tabid, trigname", cancellationToken))
            .Where(r => names.ContainsKey(Int(r[2])))
            .Select(r =>
            {
                var id = Int(r[0]);
                var definition = (headers.GetValueOrDefault(id)?.Trim() + "\n" + actions.GetValueOrDefault(id)?.Trim()).Trim();
                return new TriggerInfo(owners[Int(r[2])], names[Int(r[2])], Text(r[1])!, Timing(actions.GetValueOrDefault(id) ?? ""), Event(Text(r[3])), definition);
            })];
    }

    // The return type is the text after the RETURNING keyword of the CREATE FUNCTION header.
    private static string? ReturnType(string? definition) =>
        definition is not null && ReturningPattern().Match(definition) is { Success: true } m ? m.Groups[1].Value.Trim() : null;

    // Informix has no timing column: it is the first BEFORE / AFTER / INSTEAD OF of the definition.
    private static string Timing(string definition) =>
        TimingPattern().Match(definition) is { Success: true } m ? string.Join(' ', m.Value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)).ToUpperInvariant() : "";

    private static string Event(string? code) => code?.Trim() switch
    {
        "I" => "INSERT",
        "U" => "UPDATE",
        "D" => "DELETE",
        "S" => "SELECT",
        var other => $"unknown({other})",
    };

    [GeneratedRegex(@"\breturning\s+([^;]+?)\s*;", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex ReturningPattern();

    [GeneratedRegex(@"\b(before|after|instead\s+of)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex TimingPattern();

    private static async Task<List<object?[]>> QueryAsync(IDatabaseConnection connection, string sql, CancellationToken cancellationToken)
    {
        await using var reader = await connection.ExecuteAsync(sql, [], cancellationToken);
        return await reader.ReadAllAsync(1000, cancellationToken);
    }

    private static string? Text(object? value) => Convert.ToString(value, CultureInfo.InvariantCulture);

    private static int Int(object? value) => Convert.ToInt32(value, CultureInfo.InvariantCulture);
}
