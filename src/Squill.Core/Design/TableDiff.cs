namespace Squill.Core.Design;

/// <summary>Computes the ordered <see cref="SchemaOperation"/> list that turns one <see cref="TableDesign"/> into another.</summary>
public static class TableDiff
{
    /// <summary>Diffs two designs of the same table.</summary>
    /// <param name="original">The design built by <see cref="TableDesign.FromTable"/>.</param>
    /// <param name="edited">The design after the user's edits.</param>
    /// <returns>Operations ordered so that replaying them in sequence is valid (drops first, table rename last).</returns>
    public static IReadOnlyList<SchemaOperation> Compute(TableDesign original, TableDesign edited)
    {
        ArgumentNullException.ThrowIfNull(original);
        ArgumentNullException.ThrowIfNull(edited);

        var keptNames = edited.Columns.Where(c => c.OriginalName is not null).Select(c => c.OriginalName!).ToHashSet(StringComparer.Ordinal);
        var originalColumns = original.Columns.ToDictionary(c => c.Name, StringComparer.Ordinal);
        // Maps an edited column name to the name it has in the database.
        var toOriginal = edited.Columns.GroupBy(c => c.Name, StringComparer.Ordinal).ToDictionary(g => g.Key, g => g.First().OriginalName ?? g.Key, StringComparer.Ordinal);
        List<string> Map(IReadOnlyList<string> names) => [.. names.Select(n => toOriginal.GetValueOrDefault(n, n))];

        var dropFks = new List<SchemaOperation>();
        var dropIndexes = new List<SchemaOperation>();
        var dropPk = new List<SchemaOperation>();
        var dropColumns = new List<SchemaOperation>();
        var renames = new List<SchemaOperation>();
        var alters = new List<SchemaOperation>();
        var addColumns = new List<SchemaOperation>();
        var addPk = new List<SchemaOperation>();
        var createIndexes = new List<SchemaOperation>();
        var addFks = new List<SchemaOperation>();

        foreach (var c in original.Columns.Where(c => !keptNames.Contains(c.Name)))
        {
            dropColumns.Add(new DropColumn(c.Name));
        }

        foreach (var c in edited.Columns)
        {
            if (c.OriginalName is null || !originalColumns.TryGetValue(c.OriginalName, out var before))
            {
                addColumns.Add(new AddColumn(c));
                continue;
            }

            if (before.Name != c.Name)
            {
                renames.Add(new RenameColumn(before.Name, c.Name));
            }

            var changes = ColumnChange.None;
            if (!string.Equals(before.DataType.Trim(), c.DataType.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                changes |= ColumnChange.Type;
            }

            if (before.IsNullable != c.IsNullable)
            {
                changes |= ColumnChange.Nullability;
            }

            if (NormalizeDefault(before.DefaultExpression) != NormalizeDefault(c.DefaultExpression))
            {
                changes |= ColumnChange.Default;
            }

            if (changes != ColumnChange.None)
            {
                alters.Add(new AlterColumn(before with { Name = c.Name }, c, changes));
            }
        }

        if (!original.PrimaryKey.SequenceEqual(Map(edited.PrimaryKey), StringComparer.Ordinal))
        {
            if (original.PrimaryKey.Count > 0)
            {
                dropPk.Add(new DropPrimaryKey(original.PrimaryKeyName));
            }

            if (edited.PrimaryKey.Count > 0)
            {
                addPk.Add(new AddPrimaryKey(edited.PrimaryKey));
            }
        }

        foreach (var o in original.Indexes)
        {
            var e = edited.Indexes.FirstOrDefault(i => i.OriginalName == o.OriginalName);
            if (e is null || e.IsUnique != o.IsUnique || !o.Columns.SequenceEqual(Map(e.Columns), StringComparer.Ordinal))
            {
                dropIndexes.Add(new DropIndex(o.Name));
                if (e is not null)
                {
                    createIndexes.Add(new CreateIndex(e));
                }
            }
        }

        createIndexes.AddRange(edited.Indexes.Where(i => i.OriginalName is null).Select(i => new CreateIndex(i)));

        foreach (var o in original.ForeignKeys)
        {
            var e = edited.ForeignKeys.FirstOrDefault(f => f.OriginalName == o.OriginalName);
            if (e is null || !SameForeignKey(o, e, Map(e.Columns)))
            {
                dropFks.Add(new DropForeignKey(o.Name));
                if (e is not null)
                {
                    addFks.Add(new AddForeignKey(e));
                }
            }
        }

        addFks.AddRange(edited.ForeignKeys.Where(f => f.OriginalName is null).Select(f => new AddForeignKey(f)));

        List<SchemaOperation> result = [.. dropFks, .. dropIndexes, .. dropPk, .. dropColumns, .. renames, .. alters, .. addColumns, .. addPk, .. createIndexes, .. addFks];
        if (original.Name != edited.Name)
        {
            result.Add(new RenameTable(original.Name, edited.Name));
        }

        return result;
    }

    private static string NormalizeDefault(string? expression) => expression?.Trim() ?? "";

    private static bool SameForeignKey(ForeignKeyDesign o, ForeignKeyDesign e, List<string> editedColumns) =>
        o.Columns.SequenceEqual(editedColumns, StringComparer.Ordinal)
        && o.ReferencedSchema == e.ReferencedSchema
        && o.ReferencedTable == e.ReferencedTable
        && o.ReferencedColumns.SequenceEqual(e.ReferencedColumns, StringComparer.Ordinal)
        && o.OnDelete == e.OnDelete
        && o.OnUpdate == e.OnUpdate;
}
