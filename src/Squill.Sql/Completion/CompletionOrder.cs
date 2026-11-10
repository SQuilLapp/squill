using Squill.Core;
using Squill.Sql.Analysis;
using Squill.Sql.Lexing;

namespace Squill.Sql.Completion;

/// <summary>The sort groups of the popup (a lower prefix sorts first) and the rules that place an item in one.</summary>
internal static class CompletionOrder
{
    /// <summary>Join rows after <c>JOIN</c> and the conditions inside <c>ON</c>.</summary>
    public const string Relation = "00";

    /// <summary>Key columns of the table nearest to the caret.</summary>
    public const string KeyColumn = "01";

    /// <summary>Other columns.</summary>
    public const string Column = "02";

    /// <summary>Aliases and table names usable as qualifiers in a column list.</summary>
    public const string Qualifier = "03";

    /// <summary>Tables already used in the statement, then tables related to the FROM list.</summary>
    public const string UsedTable = "04";

    /// <summary>Other tables and views.</summary>
    public const string Table = "05";

    /// <summary>Schemas.</summary>
    public const string Schema = "06";

    /// <summary>Functions.</summary>
    public const string Function = "07";

    /// <summary>Keywords.</summary>
    public const string Keyword = "08";

    /// <summary>Counts how many times each name is written in the text, by identifier token, ignoring the word being typed.</summary>
    /// <param name="request">The request; its text is the statement under the caret, so the count means "used in this statement".</param>
    /// <param name="typed">The word being typed.</param>
    /// <returns>Occurrences by lower-case name.</returns>
    public static Dictionary<string, int> Usage(SqlRequest request, TextRange typed)
    {
        var counts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var text = request.Text;
        foreach (var t in SqlTokenizers.For(request.Dialect).Tokenize(text))
        {
            if (t.Kind is not (TokenKind.Identifier or TokenKind.QuotedIdentifier) || (typed.Length > 0 && t.Start == typed.Start))
            {
                continue;
            }

            var word = t.Text(text);
            word = t.Kind == TokenKind.QuotedIdentifier && word.Length >= 2 ? word[1..^1] : word;
            counts[word] = counts.GetValueOrDefault(word) + 1;
        }

        return counts;
    }

    /// <summary>Finds the tables that have a foreign key with one of the given sources, in either direction.</summary>
    /// <param name="schema">The schema snapshot.</param>
    /// <param name="sources">The sources already in the FROM list.</param>
    /// <returns>The related tables.</returns>
    public static HashSet<TableInfo> Related(SchemaSnapshot schema, IReadOnlyList<VisibleSource> sources)
    {
        var left = sources.Select(s => schema.FindTable(s.Schema, s.Name)).OfType<TableInfo>().ToList();
        var related = new HashSet<TableInfo>();
        if (left.Count == 0)
        {
            return related;
        }

        foreach (var t in schema.Tables)
        {
            foreach (var fk in t.ForeignKeys)
            {
                if (left.Any(l => References(fk, l)))
                {
                    related.Add(t);
                }

                if (left.Contains(t) && schema.FindTable(fk.ReferencedSchema, fk.ReferencedTable) is { } target)
                {
                    related.Add(target);
                }
            }
        }

        return related;
    }

    /// <summary>Sort text of a table in a table position.</summary>
    /// <param name="table">The table or view.</param>
    /// <param name="usage">Occurrences in the statement.</param>
    /// <param name="related">Tables related to the FROM list.</param>
    /// <returns>The sort text.</returns>
    public static string TableSort(TableInfo table, Dictionary<string, int> usage, HashSet<TableInfo> related)
    {
        if (usage.GetValueOrDefault(table.Name) is var n and > 0)
        {
            return UsedTable + "0" + (999 - Math.Min(n, 999)).ToString("D3", System.Globalization.CultureInfo.InvariantCulture) + table.Name;
        }

        return related.Contains(table) ? UsedTable + "1" + table.Name : Table + table.Name;
    }

    /// <summary>Finds the columns of a table that take part in a foreign key with another table, in either direction.</summary>
    /// <param name="table">The table whose columns are returned.</param>
    /// <param name="other">The other table.</param>
    /// <returns>The column names of <paramref name="table"/>.</returns>
    public static IEnumerable<string> KeyColumnsWith(TableInfo table, TableInfo other)
    {
        foreach (var fk in table.ForeignKeys.Where(fk => References(fk, other)))
        {
            foreach (var c in fk.Columns)
            {
                yield return c;
            }
        }

        foreach (var fk in other.ForeignKeys.Where(fk => References(fk, table)))
        {
            foreach (var c in fk.ReferencedColumns)
            {
                yield return c;
            }
        }
    }

    private static bool References(ForeignKeyInfo fk, TableInfo table) =>
        string.Equals(fk.ReferencedTable, table.Name, StringComparison.OrdinalIgnoreCase)
        && (fk.ReferencedSchema is null || string.Equals(fk.ReferencedSchema, table.Schema, StringComparison.OrdinalIgnoreCase));
}
