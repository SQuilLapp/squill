using Squill.Core;

namespace Squill.Presentation.Import;

/// <summary>One CSV column and the table column it feeds.</summary>
/// <param name="SourceIndex">Zero-based index of the CSV field.</param>
/// <param name="SourceName">CSV header text (or <c>colN</c>).</param>
/// <param name="Target">The table column, or <c>null</c> to ignore the CSV column.</param>
/// <param name="Enabled">Whether the column is imported; a column without target is never imported.</param>
public sealed record ColumnMap(int SourceIndex, string SourceName, ColumnDefinition? Target, bool Enabled);

/// <summary>The user's choice of which CSV column goes into which table column.</summary>
/// <param name="Columns">One entry per CSV column, in file order.</param>
public sealed record ImportMapping(IReadOnlyList<ColumnMap> Columns)
{
    /// <summary>Gets the maps that are imported, in file order.</summary>
    public IEnumerable<ColumnMap> Active => Columns.Where(c => c.Enabled && c.Target is not null);
}

/// <summary>Proposes a first mapping that the user then edits.</summary>
public static class ImportMapper
{
    /// <summary>Maps by name (case-insensitive, ignoring <c>_</c> and <c>-</c>), then by position when both sides have the same column count.</summary>
    /// <param name="csvColumns">CSV column names.</param>
    /// <param name="tableColumns">Target table columns.</param>
    /// <returns>The proposed mapping; unmatched CSV columns are left ignored (no synonym guessing).</returns>
    public static ImportMapping AutoMap(IReadOnlyList<string> csvColumns, IReadOnlyList<ColumnDefinition> tableColumns)
    {
        ArgumentNullException.ThrowIfNull(csvColumns);
        ArgumentNullException.ThrowIfNull(tableColumns);
        var targets = new ColumnDefinition?[csvColumns.Count];
        var used = new HashSet<int>();
        for (var i = 0; i < csvColumns.Count; i++)
        {
            var key = Normalize(csvColumns[i]);
            for (var t = 0; t < tableColumns.Count; t++)
            {
                if (!used.Contains(t) && Normalize(tableColumns[t].Name) == key)
                {
                    targets[i] = tableColumns[t];
                    used.Add(t);
                    break;
                }
            }
        }

        if (csvColumns.Count == tableColumns.Count)
        {
            for (var i = 0; i < targets.Length; i++)
            {
                if (targets[i] is null && used.Add(i))
                {
                    targets[i] = tableColumns[i];
                }
            }
        }

        return new ImportMapping([.. csvColumns.Select((name, i) => new ColumnMap(i, name, targets[i], targets[i] is not null))]);
    }

    private static string Normalize(string name) => name.Replace("_", string.Empty, StringComparison.Ordinal).Replace("-", string.Empty, StringComparison.Ordinal).ToLowerInvariant();
}
