using Squill.Core;
using Squill.Core.Editing;
using Squill.Sql.Syntax;

namespace Squill.Sql.Editing;

/// <summary>Decides from the SQL text whether a result grid maps to one editable table.</summary>
public static class EditableSourceAnalyzer
{
    /// <summary>Analyzes the query that produced a result.</summary>
    /// <param name="sql">The SQL text that produced the result.</param>
    /// <param name="dialect">The dialect of the text.</param>
    /// <param name="schema">The schema used to resolve the table.</param>
    /// <param name="resultColumns">The columns of the result, in result order.</param>
    /// <returns>The edit target, or the reason the result is read-only.</returns>
    public static EditableSourceResult Analyze(string sql, DatabaseDialect dialect, SchemaSnapshot schema, IReadOnlyList<ColumnInfo> resultColumns)
    {
        var script = SqlParser.Parse(sql, dialect);
        if (script.Diagnostics.Count > 0 || script.Statements.Count != 1)
            return ReadOnly("not a single statement");
        if (script.Statements[0] is not SelectStatement select)
            return ReadOnly("not a plain SELECT");
        if (select.Ctes.Count > 0 || select.Distinct || select.GroupBy.Count > 0 || select.Having is not null)
            return ReadOnly("query is aggregated or uses WITH/DISTINCT");
        if (select.From is not TableRef from)
            return ReadOnly("query does not read a single table");
        if (schema.FindTable(from.Schema, from.Name) is not { Kind: TableKind.Table } table)
            return ReadOnly("source is not a table");

        var keys = table.Columns.Where(c => c.IsPrimaryKey).ToList();
        if (keys.Count == 0)
            return ReadOnly("table has no primary key");
        if (resultColumns.GroupBy(c => c.Name, StringComparer.OrdinalIgnoreCase).Any(g => g.Count() > 1))
            return ReadOnly("duplicate column names");

        var plain = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in select.Items)
        {
            switch (item.Value)
            {
                case StarExpr star when star.Qualifier is null
                    || string.Equals(star.Qualifier, from.Alias, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(star.Qualifier, from.Name, StringComparison.OrdinalIgnoreCase):
                    plain.UnionWith(table.Columns.Select(c => c.Name));
                    break;
                case ColumnRef col when item.Alias is null || string.Equals(item.Alias, col.Name, StringComparison.OrdinalIgnoreCase):
                    plain.Add(col.Name);
                    break;
            }
        }

        var mapped = new List<EditColumn>();
        foreach (var result in resultColumns)
        {
            if (plain.Contains(result.Name)
                && table.Columns.FirstOrDefault(c => string.Equals(c.Name, result.Name, StringComparison.OrdinalIgnoreCase)) is { } def)
                mapped.Add(new EditColumn(result.Ordinal, def));
        }

        foreach (var key in keys)
        {
            if (!mapped.Any(m => string.Equals(m.Definition.Name, key.Name, StringComparison.OrdinalIgnoreCase)))
                return ReadOnly($"primary key column {key.Name} is not selected");
        }

        return new EditableSourceResult(new EditTarget(table.Schema, table.Name, mapped, [.. keys.Select(k => k.Name)], table), null);
    }

    private static EditableSourceResult ReadOnly(string reason) => new(null, reason);
}
