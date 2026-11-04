using System.Globalization;

namespace Squill.Core.Design;

/// <summary>How serious a <see cref="DesignIssue"/> is.</summary>
public enum DesignIssueSeverity
{
    /// <summary>The design cannot be applied.</summary>
    Error,

    /// <summary>The design can be applied but may fail or lose data.</summary>
    Warning,
}

/// <summary>A problem found in a <see cref="TableDesign"/> by <see cref="DesignValidator"/>.</summary>
/// <param name="Severity">Whether the issue blocks the change.</param>
/// <param name="Message">Human-readable description.</param>
/// <param name="Subject">Column, index or key name the issue is about; null for a table-level issue.</param>
/// <param name="Code">Stable identifier from <see cref="CoreMessageCodes"/> that the UI maps to a translated text; null when there is none.</param>
/// <param name="Args">Values for the placeholders of the translated text, <c>Args[i]</c> filling <c>{i}</c>.</param>
public sealed record DesignIssue(
    DesignIssueSeverity Severity, string Message, string? Subject, string? Code = null, IReadOnlyList<string>? Args = null);

/// <summary>Checks an edited <see cref="TableDesign"/> before any SQL is generated.</summary>
public static class DesignValidator
{
    private static bool IsLongType(string type) =>
        type.Trim().EndsWith("text", StringComparison.OrdinalIgnoreCase) || type.Trim().EndsWith("blob", StringComparison.OrdinalIgnoreCase);

    /// <summary>Validates a design.</summary>
    /// <param name="edited">The design to check.</param>
    /// <param name="original">The design the table had when the editor opened; null for a new table.</param>
    /// <param name="dialect">Target engine, which adds dialect-specific rules.</param>
    /// <returns>The errors, then (for an existing table) the data-loss warnings; empty when the design is clean.</returns>
    public static IReadOnlyList<DesignIssue> Validate(TableDesign edited, TableDesign? original, DatabaseDialect dialect)
    {
        ArgumentNullException.ThrowIfNull(edited);

        var issues = new List<DesignIssue>();
        void Error(string code, string message, string? subject = null, params string[] args) =>
            issues.Add(new DesignIssue(DesignIssueSeverity.Error, message, subject, code, args));

        if (string.IsNullOrWhiteSpace(edited.Name))
        {
            Error(CoreMessageCodes.DesignTableNameEmpty, "The table name is empty.");
        }

        if (edited.Columns.Count == 0)
        {
            Error(CoreMessageCodes.DesignNoColumn, "The table has no column.");
        }

        var columns = new Dictionary<string, ColumnDesign>(StringComparer.OrdinalIgnoreCase);
        foreach (var c in edited.Columns)
        {
            if (string.IsNullOrWhiteSpace(c.Name))
            {
                Error(CoreMessageCodes.DesignColumnNameEmpty, "A column name is empty.");
            }
            else if (!columns.TryAdd(c.Name, c))
            {
                Error(CoreMessageCodes.DesignColumnNameDuplicate, $"The column name '{c.Name}' is used twice.", c.Name, c.Name);
            }

            if (string.IsNullOrWhiteSpace(c.DataType))
            {
                Error(CoreMessageCodes.DesignColumnTypeMissing, $"The column '{c.Name}' has no data type.", c.Name, c.Name);
            }
        }

        var pk = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var name in edited.PrimaryKey)
        {
            if (!columns.TryGetValue(name, out var column))
            {
                Error(CoreMessageCodes.DesignPkColumnMissing, $"The primary key column '{name}' does not exist.", name, name);
            }
            else if (!pk.Add(name))
            {
                Error(CoreMessageCodes.DesignPkColumnDuplicate, $"The primary key column '{name}' is listed twice.", name, name);
            }
            else if (column.IsNullable)
            {
                Error(CoreMessageCodes.DesignPkColumnNullable, $"The primary key column '{name}' is nullable.", name, name);
            }
        }

        var autoIncrement = edited.Columns.Where(c => c.IsAutoIncrement).ToList();
        if (autoIncrement.Count > 1)
        {
            Error(CoreMessageCodes.DesignAutoIncrementMultiple, "Only one column can be auto-increment.");
        }

        foreach (var c in autoIncrement)
        {
            if (dialect == DatabaseDialect.Sqlite && !(pk.Count == 1 && pk.Contains(c.Name)))
            {
                Error(CoreMessageCodes.DesignAutoIncrementNotOnlyPk, $"The auto-increment column '{c.Name}' must be the only primary key column.", c.Name, c.Name);
            }
            else if (dialect == DatabaseDialect.MySql && !pk.Contains(c.Name))
            {
                Error(CoreMessageCodes.DesignAutoIncrementNotInPk, $"The auto-increment column '{c.Name}' must be part of the primary key.", c.Name, c.Name);
            }
        }

        if (original is not null)
        {
            foreach (var c in edited.Columns)
            {
                var before = c.OriginalName is null ? null : original.Columns.FirstOrDefault(o => o.Name == c.OriginalName);
                if (before is not null && before.IsAutoIncrement != c.IsAutoIncrement)
                {
                    Error(CoreMessageCodes.DesignAutoIncrementChange, $"Auto-increment cannot be changed on the existing column '{c.Name}'.", c.Name, c.Name);
                }
            }
        }

        var indexNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var i in edited.Indexes)
        {
            if (string.IsNullOrWhiteSpace(i.Name))
            {
                Error(CoreMessageCodes.DesignIndexNameEmpty, "An index name is empty.");
            }
            else if (!indexNames.Add(i.Name))
            {
                Error(CoreMessageCodes.DesignIndexNameDuplicate, $"The index name '{i.Name}' is used twice.", i.Name, i.Name);
            }

            CheckColumns("index", i.Name, i.Columns);
            if (dialect == DatabaseDialect.MySql)
            {
                foreach (var n in i.Columns.Where(n => columns.TryGetValue(n, out var col) && IsLongType(col.DataType)))
                {
                    Error(CoreMessageCodes.DesignIndexNeedsLength, $"The index '{i.Name}' cannot use the text or blob column '{n}'.", i.Name, i.Name, n);
                }
            }
        }

        var fkNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var f in edited.ForeignKeys)
        {
            if (string.IsNullOrWhiteSpace(f.Name))
            {
                Error(CoreMessageCodes.DesignForeignKeyNameEmpty, "A foreign key name is empty.");
            }
            else if (!fkNames.Add(f.Name))
            {
                Error(CoreMessageCodes.DesignForeignKeyNameDuplicate, $"The foreign key name '{f.Name}' is used twice.", f.Name, f.Name);
            }

            CheckColumns("foreign key", f.Name, f.Columns);
            if (f.Columns.Count != f.ReferencedColumns.Count)
            {
                Error(
                    CoreMessageCodes.DesignForeignKeyColumnCount, $"The foreign key '{f.Name}' has {f.Columns.Count} local and {f.ReferencedColumns.Count} referenced columns.", f.Name,
                    f.Name, f.Columns.Count.ToString(CultureInfo.InvariantCulture), f.ReferencedColumns.Count.ToString(CultureInfo.InvariantCulture));
            }

            if (string.IsNullOrWhiteSpace(f.ReferencedTable))
            {
                Error(CoreMessageCodes.DesignForeignKeyNoTable, $"The foreign key '{f.Name}' has no referenced table.", f.Name, f.Name);
            }
        }

        if (original is not null && edited.Schema != original.Schema)
        {
            Error(CoreMessageCodes.DesignSchemaMove, "Moving a table to another schema is not supported.");
        }

        if (dialect == DatabaseDialect.Sqlite && edited.Schema is not null && edited.Schema != "main")
        {
            Error(CoreMessageCodes.DesignSqliteSchema, $"SQLite only supports the schema 'main', not '{edited.Schema}'.", null, edited.Schema);
        }

        if (original is not null)
        {
            foreach (var op in TableDiff.Compute(original, edited))
            {
                switch (op)
                {
                    case DropColumn d:
                        issues.Add(new DesignIssue(DesignIssueSeverity.Warning, $"Data in column '{d.Name}' is lost.", d.Name, CoreMessageCodes.DesignDropColumnDataLoss, [d.Name]));
                        break;
                    case AlterColumn a:
                        if (a.Changes.HasFlag(ColumnChange.Type))
                        {
                            issues.Add(new DesignIssue(DesignIssueSeverity.Warning, $"Changing the type of '{a.After.Name}' may fail or lose data.", a.After.Name, CoreMessageCodes.DesignTypeChangeRisk, [a.After.Name]));
                        }

                        if (a.Changes.HasFlag(ColumnChange.Nullability) && a.Before.IsNullable && !a.After.IsNullable)
                        {
                            issues.Add(new DesignIssue(DesignIssueSeverity.Warning, $"Existing NULLs make '{a.After.Name}' fail to become non-nullable.", a.After.Name, CoreMessageCodes.DesignNullsBlockNotNull, [a.After.Name]));
                        }

                        break;
                }
            }
        }

        return issues;

        void CheckColumns(string kind, string name, IReadOnlyList<string> names)
        {
            var isIndex = kind == "index";
            if (names.Count == 0)
            {
                Error(isIndex ? CoreMessageCodes.DesignIndexNoColumn : CoreMessageCodes.DesignForeignKeyNoColumn, $"The {kind} '{name}' has no column.", name, name);
            }

            foreach (var n in names.Where(n => !columns.ContainsKey(n)))
            {
                Error(
                    isIndex ? CoreMessageCodes.DesignIndexColumnMissing : CoreMessageCodes.DesignForeignKeyColumnMissing,
                    $"The {kind} '{name}' uses the missing column '{n}'.", name, name, n);
            }
        }
    }
}
