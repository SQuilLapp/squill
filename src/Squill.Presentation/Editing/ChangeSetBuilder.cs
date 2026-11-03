using Squill.Core;
using Squill.Core.Editing;
using Squill.Presentation.Results;

namespace Squill.Presentation.Editing;

/// <summary>Turns the pending state of an <see cref="EditableGridDataSource"/> into a validated <see cref="ChangeSet"/>.</summary>
public static class ChangeSetBuilder
{
    /// <summary>Builds the change set; it only reads the grid.</summary>
    /// <param name="grid">The grid holding the pending changes.</param>
    /// <param name="target">The table the grid edits.</param>
    /// <returns>The edits in execution order (deletes, updates, inserts) and every validation issue found.</returns>
    public static ChangeSet Build(EditableGridDataSource grid, EditTarget target)
    {
        List<PendingEdit> deletes = [], updates = [], inserts = [];
        List<EditIssue> issues = [];
        List<object?[]> insertedKeys = [];

        for (long row = 0; row < grid.RowCount; row++)
        {
            switch (grid.GetRowState(row))
            {
                case RowState.Deleted:
                    deletes.Add(new(row, new DeleteRow(Key(grid, target, row))));
                    break;
                case RowState.Modified:
                    var changes = new List<ColumnValue>();
                    foreach (var c in target.Columns.Where(c => grid.IsCellModified(row, c.ResultOrdinal)))
                    {
                        var value = grid.GetCell(row, c.ResultOrdinal);
                        CheckNull(issues, row, c, value);
                        changes.Add(new(c.Definition.Name, value));
                    }

                    updates.Add(new(row, new UpdateRow(Key(grid, target, row), changes)));
                    break;
                case RowState.Inserted:
                    inserts.Add(new(row, new InsertRow(BuildInsert(grid, target, row, issues, insertedKeys))));
                    break;
            }
        }

        return new([.. deletes, .. updates, .. inserts], issues);
    }

    private static List<ColumnValue> Key(EditableGridDataSource grid, EditTarget target, long row) =>
        [.. target.KeyColumns.Select(k => new ColumnValue(k, grid.GetOriginal(row, KeyColumn(target, k).ResultOrdinal)))];

    private static EditColumn KeyColumn(EditTarget target, string name) =>
        target.Columns.First(c => string.Equals(c.Definition.Name, name, StringComparison.OrdinalIgnoreCase));

    private static void CheckNull(List<EditIssue> issues, long row, EditColumn c, object? value)
    {
        if (value is null && !c.Definition.IsNullable)
        {
            issues.Add(new(row, c.Definition.Name, $"{c.Definition.Name} cannot be NULL", CoreMessageCodes.EditColumnNull, [c.Definition.Name]));
        }
    }

    private static List<ColumnValue> BuildInsert(
        EditableGridDataSource grid, EditTarget target, long row, List<EditIssue> issues, List<object?[]> insertedKeys)
    {
        var values = new List<ColumnValue>();
        foreach (var c in target.Columns)
        {
            var d = c.Definition;
            if (grid.IsCellSet(row, c.ResultOrdinal))
            {
                var value = grid.GetCell(row, c.ResultOrdinal);
                CheckNull(issues, row, c, value);
                values.Add(new(d.Name, value));
            }
            else if (!d.IsNullable && d.DefaultExpression is null && !d.IsAutoIncrement)
            {
                issues.Add(new(row, d.Name, $"{d.Name} is required", CoreMessageCodes.EditColumnRequired, [d.Name]));
            }
        }

        if (target.KeyColumns.Count > 0 && target.KeyColumns.All(k => grid.IsCellSet(row, KeyColumn(target, k).ResultOrdinal)))
        {
            var key = target.KeyColumns.Select(k => grid.GetCell(row, KeyColumn(target, k).ResultOrdinal)).ToArray();
            if (insertedKeys.Any(p => p.Zip(key).All(t => ValueEquals(t.First, t.Second))))
            {
                issues.Add(new(row, null, "duplicate key in pending rows", CoreMessageCodes.EditDuplicateKey));
            }

            insertedKeys.Add(key);
        }

        return values;
    }

    private static bool ValueEquals(object? a, object? b) =>
        a is byte[] x && b is byte[] y ? x.AsSpan().SequenceEqual(y) : Equals(a, b);
}
