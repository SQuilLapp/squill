using Squill.Core;

namespace Squill.Sql.Completion;

/// <summary>Builds the text of a generated join condition from a <see cref="TableRelation"/>.</summary>
internal static class JoinConditionText
{
    /// <summary>Builds <c>existing.c1 = new.d1 AND existing.c2 = new.d2</c> (no <c>ON</c> keyword).</summary>
    /// <param name="relation">The relation; the existing side is always written first.</param>
    /// <param name="existingName">Unquoted alias or table name of the table already in the FROM list.</param>
    /// <param name="newName">Unquoted alias or table name of the new table.</param>
    /// <param name="dialect">The dialect that decides the identifier quoting.</param>
    /// <returns>The condition text.</returns>
    public static string Build(TableRelation relation, string existingName, string newName, DatabaseDialect dialect)
    {
        var left = IdentifierQuoting.Quote(existingName, dialect);
        var right = IdentifierQuoting.Quote(newName, dialect);
        var parts = new string[relation.ExistingColumns.Count];
        for (var i = 0; i < parts.Length; i++)
        {
            parts[i] = left + "." + IdentifierQuoting.Quote(relation.ExistingColumns[i], dialect)
                + " = " + right + "." + IdentifierQuoting.Quote(relation.OtherColumns[i], dialect);
        }

        return string.Join(" AND ", parts);
    }

    /// <summary>Describes a relation in a short human text, for documentation and tests.</summary>
    /// <param name="relation">The relation.</param>
    /// <returns><c>orders.customer_id -&gt; customers.id (fk_name)</c> for a foreign key (referencing side first), <c>same column name: x</c> otherwise.</returns>
    public static string Describe(TableRelation relation)
    {
        if (relation.Origin == RelationOrigin.SameColumnName)
        {
            return "same column name: " + string.Join(", ", relation.ExistingColumns);
        }

        var (from, fromColumns, to, toColumns) = relation.OtherReferencesExisting
            ? (relation.Other.Name, relation.OtherColumns, relation.Existing.Name, relation.ExistingColumns)
            : (relation.Existing.Name, relation.ExistingColumns, relation.Other.Name, relation.OtherColumns);
        return $"{Side(from, fromColumns)} -> {Side(to, toColumns)} ({relation.ForeignKeyName})";
    }

    private static string Side(string table, IReadOnlyList<string> columns)
        => columns.Count == 1 ? table + "." + columns[0] : table + ".(" + string.Join(", ", columns) + ")";
}
