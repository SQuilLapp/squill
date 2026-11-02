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
public sealed record DesignIssue(DesignIssueSeverity Severity, string Message, string? Subject);

/// <summary>Checks an edited <see cref="TableDesign"/> before any SQL is generated.</summary>
public static class DesignValidator
{
    /// <summary>Validates a design.</summary>
    /// <param name="edited">The design to check.</param>
    /// <param name="original">The design the table had when the editor opened; null for a new table.</param>
    /// <param name="dialect">Target engine, which adds dialect-specific rules.</param>
    /// <returns>The errors, then (for an existing table) the data-loss warnings; empty when the design is clean.</returns>
    public static IReadOnlyList<DesignIssue> Validate(TableDesign edited, TableDesign? original, DatabaseDialect dialect)
    {
        ArgumentNullException.ThrowIfNull(edited);

        var issues = new List<DesignIssue>();
        void Error(string message, string? subject = null) => issues.Add(new DesignIssue(DesignIssueSeverity.Error, message, subject));

        if (string.IsNullOrWhiteSpace(edited.Name))
        {
            Error("The table name is empty.");
        }

        if (edited.Columns.Count == 0)
        {
            Error("The table has no column.");
        }

        var columns = new Dictionary<string, ColumnDesign>(StringComparer.OrdinalIgnoreCase);
        foreach (var c in edited.Columns)
        {
            if (string.IsNullOrWhiteSpace(c.Name))
            {
                Error("A column name is empty.");
            }
            else if (!columns.TryAdd(c.Name, c))
            {
                Error($"The column name '{c.Name}' is used twice.", c.Name);
            }

            if (string.IsNullOrWhiteSpace(c.DataType))
            {
                Error($"The column '{c.Name}' has no data type.", c.Name);
            }
        }

        var pk = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var name in edited.PrimaryKey)
        {
            if (!columns.TryGetValue(name, out var column))
            {
                Error($"The primary key column '{name}' does not exist.", name);
            }
            else if (!pk.Add(name))
            {
                Error($"The primary key column '{name}' is listed twice.", name);
            }
            else if (column.IsNullable)
            {
                Error($"The primary key column '{name}' is nullable.", name);
            }
        }

        var autoIncrement = edited.Columns.Where(c => c.IsAutoIncrement).ToList();
        if (autoIncrement.Count > 1)
        {
            Error("Only one column can be auto-increment.");
        }

        foreach (var c in autoIncrement)
        {
            if (dialect == DatabaseDialect.Sqlite && !(pk.Count == 1 && pk.Contains(c.Name)))
            {
                Error($"The auto-increment column '{c.Name}' must be the only primary key column.", c.Name);
            }
            else if (dialect == DatabaseDialect.MySql && !pk.Contains(c.Name))
            {
                Error($"The auto-increment column '{c.Name}' must be part of the primary key.", c.Name);
            }
        }

        if (original is not null)
        {
            foreach (var c in edited.Columns)
            {
                var before = c.OriginalName is null ? null : original.Columns.FirstOrDefault(o => o.Name == c.OriginalName);
                if (before is not null && before.IsAutoIncrement != c.IsAutoIncrement)
                {
                    Error($"Auto-increment cannot be changed on the existing column '{c.Name}'.", c.Name);
                }
            }
        }

        var indexNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var i in edited.Indexes)
        {
            if (string.IsNullOrWhiteSpace(i.Name))
            {
                Error("An index name is empty.");
            }
            else if (!indexNames.Add(i.Name))
            {
                Error($"The index name '{i.Name}' is used twice.", i.Name);
            }

            CheckColumns("index", i.Name, i.Columns);
        }

        var fkNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var f in edited.ForeignKeys)
        {
            if (string.IsNullOrWhiteSpace(f.Name))
            {
                Error("A foreign key name is empty.");
            }
            else if (!fkNames.Add(f.Name))
            {
                Error($"The foreign key name '{f.Name}' is used twice.", f.Name);
            }

            CheckColumns("foreign key", f.Name, f.Columns);
            if (f.Columns.Count != f.ReferencedColumns.Count)
            {
                Error($"The foreign key '{f.Name}' has {f.Columns.Count} local and {f.ReferencedColumns.Count} referenced columns.", f.Name);
            }

            if (string.IsNullOrWhiteSpace(f.ReferencedTable))
            {
                Error($"The foreign key '{f.Name}' has no referenced table.", f.Name);
            }
        }

        if (original is not null && edited.Schema != original.Schema)
        {
            Error("Moving a table to another schema is not supported.");
        }

        if (dialect == DatabaseDialect.Sqlite && edited.Schema is not null && edited.Schema != "main")
        {
            Error($"SQLite only supports the schema 'main', not '{edited.Schema}'.");
        }

        if (original is not null)
        {
            foreach (var op in TableDiff.Compute(original, edited))
            {
                switch (op)
                {
                    case DropColumn d:
                        issues.Add(new DesignIssue(DesignIssueSeverity.Warning, $"Data in column '{d.Name}' is lost.", d.Name));
                        break;
                    case AlterColumn a:
                        if (a.Changes.HasFlag(ColumnChange.Type))
                        {
                            issues.Add(new DesignIssue(DesignIssueSeverity.Warning, $"Changing the type of '{a.After.Name}' may fail or lose data.", a.After.Name));
                        }

                        if (a.Changes.HasFlag(ColumnChange.Nullability) && a.Before.IsNullable && !a.After.IsNullable)
                        {
                            issues.Add(new DesignIssue(DesignIssueSeverity.Warning, $"Existing NULLs make '{a.After.Name}' fail to become non-nullable.", a.After.Name));
                        }

                        break;
                }
            }
        }

        return issues;

        void CheckColumns(string kind, string name, IReadOnlyList<string> names)
        {
            if (names.Count == 0)
            {
                Error($"The {kind} '{name}' has no column.", name);
            }

            foreach (var n in names.Where(n => !columns.ContainsKey(n)))
            {
                Error($"The {kind} '{name}' uses the missing column '{n}'.", name);
            }
        }
    }
}
