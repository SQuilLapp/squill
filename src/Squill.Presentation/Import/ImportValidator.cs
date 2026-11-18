using Squill.Core;
using Squill.Presentation.Localization;

namespace Squill.Presentation.Import;

/// <summary>What is wrong with the mapping as a whole.</summary>
public enum TableIssueKind
{
    /// <summary>A NOT NULL column without default is not fed by any CSV column.</summary>
    RequiredUnmapped,

    /// <summary>Two CSV columns feed the same table column.</summary>
    DuplicateTarget,

    /// <summary>A mapped column has a type the text cannot be converted to.</summary>
    UnsupportedType,
}

/// <summary>A problem with the mapping that blocks the import.</summary>
/// <param name="Kind">The problem class.</param>
/// <param name="ColumnName">The table column concerned.</param>
/// <param name="Message">A localized message.</param>
public sealed record TableIssue(TableIssueKind Kind, string ColumnName, string Message);

/// <summary>The sample check of one mapped column.</summary>
/// <param name="SourceIndex">Index of the CSV column.</param>
/// <param name="Checked">Rows examined.</param>
/// <param name="Failed">Rows that failed to convert.</param>
/// <param name="FirstExample">The raw text of the first failure, or <c>null</c>.</param>
/// <param name="Kind">The first failure's class, <see cref="ColumnIssueKind.None"/> when all converted.</param>
public sealed record ColumnReport(int SourceIndex, int Checked, int Failed, string? FirstExample, ColumnIssueKind Kind);

/// <summary>The result of checking a mapping against a sample.</summary>
/// <param name="Columns">One report per imported column.</param>
/// <param name="TableIssues">Problems of the mapping itself.</param>
public sealed record ImportValidation(IReadOnlyList<ColumnReport> Columns, IReadOnlyList<TableIssue> TableIssues)
{
    /// <summary>Gets a value indicating whether the mapping itself is sound (CHECK constraints are not predicted).</summary>
    public bool CanImport => TableIssues.Count == 0;
}

/// <summary>Checks a mapping over the preview sample.</summary>
public static class ImportValidator
{
    /// <summary>Validates the mapping.</summary>
    /// <param name="mapping">The user's mapping.</param>
    /// <param name="tableColumns">Every column of the target table.</param>
    /// <param name="sampleRows">The preview rows.</param>
    /// <param name="format">The reading options.</param>
    /// <returns>Per-column counts and table-level issues.</returns>
    public static ImportValidation Validate(ImportMapping mapping, IReadOnlyList<ColumnDefinition> tableColumns, IReadOnlyList<string?[]> sampleRows, ImportFormat format)
    {
        ArgumentNullException.ThrowIfNull(mapping);
        ArgumentNullException.ThrowIfNull(tableColumns);
        ArgumentNullException.ThrowIfNull(sampleRows);
        var active = mapping.Active.ToList();
        List<TableIssue> issues = [];
        foreach (var dup in active.GroupBy(m => m.Target!.Name, StringComparer.OrdinalIgnoreCase).Where(g => g.Count() > 1))
        {
            issues.Add(new(TableIssueKind.DuplicateTarget, dup.Key, UiText.Format("import.issue.duplicateTarget", dup.Key)));
        }

        foreach (var column in tableColumns.Where(c => !c.IsNullable && c.DefaultExpression is null && !c.IsAutoIncrement && !c.IsGenerated))
        {
            if (!active.Any(m => string.Equals(m.Target!.Name, column.Name, StringComparison.OrdinalIgnoreCase)))
            {
                issues.Add(new(TableIssueKind.RequiredUnmapped, column.Name, UiText.Format("import.issue.requiredUnmapped", column.Name)));
            }
        }

        foreach (var m in active.Where(m => ValueConverter.ClrTypeFor(m.Target!.DataType) is null))
        {
            issues.Add(new(TableIssueKind.UnsupportedType, m.Target!.Name, UiText.Format("import.issue.unsupportedType", m.Target.DataType)));
        }

        var reports = new List<ColumnReport>();
        foreach (var m in active)
        {
            var failed = 0;
            string? example = null;
            var kind = ColumnIssueKind.None;
            foreach (var row in sampleRows)
            {
                var raw = m.SourceIndex < row.Length ? row[m.SourceIndex] : null;
                if (ValueConverter.Convert(raw, m.Target!, format).Issue is { } issue)
                {
                    failed++;
                    if (example is null)
                    {
                        example = raw ?? string.Empty;
                        kind = issue.Kind;
                    }
                }
            }

            reports.Add(new(m.SourceIndex, sampleRows.Count, failed, example, kind));
        }

        return new(reports, issues);
    }
}
