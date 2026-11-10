using System.Globalization;
using System.Text;
using Squill.Core;
using Squill.Sql.Analysis;
using Squill.Sql.Functions;

namespace Squill.Sql.Completion;

/// <summary>Builds the Markdown shown beside a completion item, so every wording lives here.</summary>
internal static class CompletionDocumentation
{
    private const int MaxLength = 2000;
    private const int MaxColumns = 12;

    /// <summary>Wraps an identifier or SQL fragment in a code span, with a longer fence when it contains a backtick.</summary>
    /// <param name="text">The text.</param>
    /// <returns>The code span.</returns>
    public static string Code(string text)
    {
        int longest = 0, run = 0;
        foreach (var c in text)
        {
            run = c == '`' ? run + 1 : 0;
            longest = Math.Max(longest, run);
        }

        var fence = new string('`', longest + 1);
        return longest == 0 ? fence + text + fence : fence + " " + text + " " + fence;
    }

    /// <summary>Documents a table or view: its columns, foreign keys and, for a view, the start of its definition.</summary>
    /// <param name="table">The table or view.</param>
    /// <returns>The Markdown.</returns>
    public static string Table(TableInfo table)
    {
        var sb = new StringBuilder();
        sb.Append("**").Append(table.Name).Append("** - ").Append(table.Kind.IsViewLike() ? "view" : "table")
            .Append(", ").Append(table.Columns.Count.ToString(CultureInfo.InvariantCulture)).Append(" columns");

        if (table.Columns.Count > 0)
        {
            sb.Append("\n\n```\n");
            foreach (var c in table.Columns.Take(MaxColumns))
            {
                sb.Append(c.Name).Append(' ').Append(c.DataType);
                if (c.IsPrimaryKey)
                {
                    sb.Append(" PK");
                }

                if (!c.IsNullable)
                {
                    sb.Append(" NOT NULL");
                }

                sb.Append('\n');
            }

            if (table.Columns.Count > MaxColumns)
            {
                sb.Append("...and ").Append((table.Columns.Count - MaxColumns).ToString(CultureInfo.InvariantCulture)).Append(" more\n");
            }

            sb.Append("```");
        }

        if (table.ForeignKeys.Count > 0)
        {
            sb.Append("\n\nForeign keys:");
            foreach (var fk in table.ForeignKeys.Take(5))
            {
                sb.Append("\n- ").Append(Code($"{fk.Name}: {string.Join(", ", fk.Columns)} -> {fk.ReferencedTable}({string.Join(", ", fk.ReferencedColumns)})"));
            }
        }

        if (table.Kind.IsViewLike() && table.Definition is { } definition)
        {
            var lines = definition.Split('\n').Take(MaxColumns).Select(l => l.TrimEnd('\r'));
            sb.Append("\n\n````\n").Append(string.Join('\n', lines)).Append("\n````");
        }

        return Cap(sb.ToString());
    }

    /// <summary>Documents a column of a table: type, nullability, key roles, default and foreign keys in both directions.</summary>
    /// <param name="schema">The schema snapshot, searched for the tables that reference the column.</param>
    /// <param name="table">The owner.</param>
    /// <param name="column">The column.</param>
    /// <param name="owner">The name the column is reached through (alias or table name).</param>
    /// <returns>The Markdown.</returns>
    public static string Column(SchemaSnapshot schema, TableInfo table, ColumnDefinition column, string owner)
    {
        var facts = new List<string> { Code(column.DataType), column.IsNullable ? "nullable" : "not null" };
        if (column.IsPrimaryKey)
        {
            facts.Add("primary key");
        }

        if (column.IsAutoIncrement)
        {
            facts.Add("auto increment");
        }

        if (column.IsGenerated)
        {
            facts.Add("generated");
        }

        if (column.DefaultExpression is { } expression)
        {
            facts.Add("default " + Code(expression));
        }

        var sb = new StringBuilder();
        sb.Append("**").Append(owner).Append('.').Append(column.Name).Append("**\n\n").Append(string.Join(", ", facts));

        var references = new List<string>();
        foreach (var fk in table.ForeignKeys)
        {
            var i = IndexOf(fk.Columns, column.Name);
            if (i >= 0 && i < fk.ReferencedColumns.Count)
            {
                references.Add(Code($"{fk.ReferencedTable}({fk.ReferencedColumns[i]})"));
            }
        }

        if (references.Count > 0)
        {
            sb.Append("\n\nreferences ").Append(string.Join(", ", references.Take(3)));
        }

        var referencedBy = new List<string>();
        foreach (var other in schema.Tables)
        {
            foreach (var fk in other.ForeignKeys)
            {
                var i = IndexOf(fk.ReferencedColumns, column.Name);
                if (i >= 0 && i < fk.Columns.Count && string.Equals(fk.ReferencedTable, table.Name, StringComparison.OrdinalIgnoreCase)
                    && (fk.ReferencedSchema is null || string.Equals(fk.ReferencedSchema, table.Schema, StringComparison.OrdinalIgnoreCase)))
                {
                    referencedBy.Add(Code($"{other.Name}({fk.Columns[i]})"));
                }
            }
        }

        if (referencedBy.Count > 0)
        {
            sb.Append("\n\nreferenced by ").Append(string.Join(", ", referencedBy.Take(3)));
        }

        return Cap(sb.ToString());
    }

    /// <summary>Documents a column of a derived table or CTE.</summary>
    /// <param name="owner">The derived table name or alias.</param>
    /// <param name="column">The column name.</param>
    /// <returns>The Markdown.</returns>
    public static string DerivedColumn(string owner, string column) => $"**{owner}.{column}**\n\nderived column";

    /// <summary>Documents the alias or table name offered as a qualifier.</summary>
    /// <param name="source">The visible source.</param>
    /// <returns>The Markdown.</returns>
    public static string Source(VisibleSource source)
    {
        if (source.DerivedColumns is { } columns)
        {
            var shown = string.Join(", ", columns.Take(MaxColumns)) + (columns.Count > MaxColumns ? ", ..." : string.Empty);
            return Cap("derived table, columns: " + shown);
        }

        var name = source.Schema is null ? source.Name : source.Schema + "." + source.Name;
        return (source.Alias is null ? "table " : "alias of ") + Code(name);
    }

    /// <summary>Documents a built-in function.</summary>
    /// <param name="function">The signature.</param>
    /// <returns>The Markdown.</returns>
    public static string Function(FunctionSignature function) => Cap(Code(function.Display) + "\n\n" + function.Description);

    /// <summary>Documents a stored function of the schema.</summary>
    /// <param name="routine">The routine.</param>
    /// <returns>The Markdown.</returns>
    public static string Function(RoutineInfo routine) => Cap(Code(routine.Signature + (routine.ReturnType is null ? string.Empty : " -> " + routine.ReturnType)));

    /// <summary>Documents a join row or an <c>ON</c> condition: why it is proposed and what the foreign key does on delete and update.</summary>
    /// <param name="schema">The schema snapshot.</param>
    /// <param name="relation">The relation.</param>
    /// <returns>The Markdown.</returns>
    public static string Relation(SchemaSnapshot schema, TableRelation relation)
    {
        var text = JoinConditionText.Describe(relation);
        if (relation.Origin == RelationOrigin.ForeignKey)
        {
            var owner = relation.OtherReferencesExisting ? relation.Other : schema.FindTable(relation.Existing.Schema, relation.Existing.Name);
            if (owner?.ForeignKeys.FirstOrDefault(f => f.Name == relation.ForeignKeyName) is { } fk)
            {
                text += " ON DELETE " + fk.OnDelete.ToSql() + ", ON UPDATE " + fk.OnUpdate.ToSql();
            }
        }

        return Cap(text);
    }

    private static int IndexOf(IReadOnlyList<string> names, string name)
    {
        for (var i = 0; i < names.Count; i++)
        {
            if (string.Equals(names[i], name, StringComparison.OrdinalIgnoreCase))
            {
                return i;
            }
        }

        return -1;
    }

    private static string Cap(string text) => text.Length <= MaxLength ? text : text[..(MaxLength - 3)] + "...";
}
