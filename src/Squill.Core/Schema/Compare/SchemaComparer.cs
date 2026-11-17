using System.Text;

namespace Squill.Core.Schema.Compare;

/// <summary>
/// Compares two schema snapshots of the same dialect. The source is the desired state and the target is what a script would change.
/// Names match case-insensitively. Columns match by name, so a rename shows as a removed plus an added column; column order and
/// definition whitespace never count as differences. Triggers are not compared.
/// </summary>
public static class SchemaComparer
{
    /// <summary>Lists what differs between two snapshots.</summary>
    /// <param name="source">The desired state.</param>
    /// <param name="target">The state a script would change.</param>
    /// <returns>The differences in a deterministic order.</returns>
    public static SchemaComparison Compare(SchemaSnapshot source, SchemaSnapshot target)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(target);

        var result = new List<ObjectDifference>();
        CompareRelations(source, target, result);
        CompareRoutines(source, target, result);
        result.Sort(static (a, b) =>
        {
            var c = a.Category.CompareTo(b.Category);
            if (c == 0) c = StringComparer.OrdinalIgnoreCase.Compare(a.Schema ?? "", b.Schema ?? "");
            if (c == 0) c = StringComparer.OrdinalIgnoreCase.Compare(a.Name, b.Name);
            return c;
        });
        return new SchemaComparison(result);
    }

    private static void CompareRelations(SchemaSnapshot source, SchemaSnapshot target, List<ObjectDifference> result)
    {
        var targets = target.Tables.ToDictionary(Key, StringComparer.OrdinalIgnoreCase);
        var matched = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var s in source.Tables)
        {
            var key = Key(s);
            if (!targets.TryGetValue(key, out var t))
            {
                result.Add(new(CategoryOf(s), s.Schema, s.Name, DifferenceKind.Added, []));
                continue;
            }

            matched.Add(key);
            if (s.Kind.IsViewLike())
            {
                if (DefinitionDetail(s.Definition, t.Definition) is { } d)
                    result.Add(new(ObjectCategory.View, s.Schema, s.Name, DifferenceKind.Modified, [d]));
                continue;
            }

            var details = TableDetails(s, t);
            if (details.Count > 0)
                result.Add(new(ObjectCategory.Table, s.Schema, s.Name, DifferenceKind.Modified, details));
            CompareIndexes(s, t, result);
        }

        foreach (var t in target.Tables.Where(t => !matched.Contains(Key(t))))
            result.Add(new(CategoryOf(t), t.Schema, t.Name, DifferenceKind.Removed, []));
    }

    private static string Key(TableInfo t) => $"{(t.Kind.IsViewLike() ? 'V' : 'T')}|{t.Schema}|{t.Name}";

    private static ObjectCategory CategoryOf(TableInfo t) => t.Kind.IsViewLike() ? ObjectCategory.View : ObjectCategory.Table;

    private static List<DifferenceDetail> TableDetails(TableInfo s, TableInfo t)
    {
        var details = new List<DifferenceDetail>();
        var tc = t.Columns.ToDictionary(c => c.Name, StringComparer.OrdinalIgnoreCase);
        var sc = s.Columns.ToDictionary(c => c.Name, StringComparer.OrdinalIgnoreCase);
        foreach (var c in s.Columns)
        {
            if (!tc.TryGetValue(c.Name, out var o))
                details.Add(new(DetailKind.ColumnAdded, c.Name, Describe(c), null));
            else if (ColumnChanged(c, o))
                details.Add(new(DetailKind.ColumnChanged, c.Name, Describe(c), Describe(o)));
        }

        details.AddRange(t.Columns.Where(c => !sc.ContainsKey(c.Name)).Select(c => new DifferenceDetail(DetailKind.ColumnRemoved, c.Name, null, Describe(c))));

        var spk = string.Join(", ", s.Columns.Where(c => c.IsPrimaryKey).Select(c => c.Name));
        var tpk = string.Join(", ", t.Columns.Where(c => c.IsPrimaryKey).Select(c => c.Name));
        if (!string.Equals(spk, tpk, StringComparison.OrdinalIgnoreCase))
            details.Add(new(DetailKind.PrimaryKeyChanged, "", spk, tpk));

        Diff(s.ForeignKeys, t.ForeignKeys, f => f.Name, SameForeignKey, DescribeFk,
            DetailKind.ForeignKeyAdded, DetailKind.ForeignKeyRemoved, DetailKind.ForeignKeyChanged, details);
        Diff(s.Indexes, t.Indexes, i => i.Name, SameIndex, DescribeIndex,
            DetailKind.IndexAdded, DetailKind.IndexRemoved, DetailKind.IndexChanged, details);
        return details;
    }

    // Indexes of tables present on both sides are also listed as objects of their own (the INDEX group).
    private static void CompareIndexes(TableInfo s, TableInfo t, List<ObjectDifference> result)
    {
        var details = new List<DifferenceDetail>();
        Diff(s.Indexes, t.Indexes, i => i.Name, SameIndex, DescribeIndex,
            DetailKind.IndexAdded, DetailKind.IndexRemoved, DetailKind.IndexChanged, details);
        foreach (var d in details)
        {
            var kind = d.Kind switch
            {
                DetailKind.IndexAdded => DifferenceKind.Added,
                DetailKind.IndexRemoved => DifferenceKind.Removed,
                _ => DifferenceKind.Modified,
            };
            result.Add(new(ObjectCategory.Index, s.Schema, $"{s.Name}.{d.Subject}", kind, kind == DifferenceKind.Modified ? [d] : []));
        }
    }

    // Matches items by name (case-insensitive) and appends added / changed (source order), then removed (target order) details.
    private static void Diff<T>(IReadOnlyList<T> source, IReadOnlyList<T> target, Func<T, string> name, Func<T, T, bool> same, Func<T, string> describe,
        DetailKind added, DetailKind removed, DetailKind changed, List<DifferenceDetail> details)
    {
        var tm = target.ToDictionary(name, StringComparer.OrdinalIgnoreCase);
        var sm = source.ToDictionary(name, StringComparer.OrdinalIgnoreCase);
        foreach (var s in source)
        {
            if (!tm.TryGetValue(name(s), out var t))
                details.Add(new(added, name(s), describe(s), null));
            else if (!same(s, t))
                details.Add(new(changed, name(s), describe(s), describe(t)));
        }

        details.AddRange(target.Where(t => !sm.ContainsKey(name(t))).Select(t => new DifferenceDetail(removed, name(t), null, describe(t))));
    }

    // Same notion of "changed" as TableDiff (type, nullability, default) plus identity.
    private static bool ColumnChanged(ColumnDefinition a, ColumnDefinition b) =>
        !string.Equals(a.DataType.Trim(), b.DataType.Trim(), StringComparison.OrdinalIgnoreCase)
        || a.IsNullable != b.IsNullable
        || (a.DefaultExpression?.Trim() ?? "") != (b.DefaultExpression?.Trim() ?? "")
        || a.IsAutoIncrement != b.IsAutoIncrement;

    private static string Describe(ColumnDefinition c) =>
        $"{c.DataType.Trim()} {(c.IsNullable ? "NULL" : "NOT NULL")}{(c.IsAutoIncrement ? " IDENTITY" : "")}{(c.DefaultExpression is { } d ? " DEFAULT " + d.Trim() : "")}";

    private static bool SameIndex(IndexInfo a, IndexInfo b) => a.IsUnique == b.IsUnique && SameList(a.Columns, b.Columns);

    private static string DescribeIndex(IndexInfo i) => $"{(i.IsUnique ? "UNIQUE " : "")}({string.Join(", ", i.Columns)})";

    private static bool SameForeignKey(ForeignKeyInfo a, ForeignKeyInfo b) =>
        SameList(a.Columns, b.Columns) && SameList(a.ReferencedColumns, b.ReferencedColumns)
        && string.Equals(a.ReferencedSchema ?? "", b.ReferencedSchema ?? "", StringComparison.OrdinalIgnoreCase)
        && string.Equals(a.ReferencedTable, b.ReferencedTable, StringComparison.OrdinalIgnoreCase)
        && a.OnDelete == b.OnDelete && a.OnUpdate == b.OnUpdate;

    private static string DescribeFk(ForeignKeyInfo f) =>
        $"({string.Join(", ", f.Columns)}) -> {(f.ReferencedSchema is null ? "" : f.ReferencedSchema + ".")}{f.ReferencedTable}({string.Join(", ", f.ReferencedColumns)}) ON DELETE {f.OnDelete} ON UPDATE {f.OnUpdate}";

    private static bool SameList(IReadOnlyList<string> a, IReadOnlyList<string> b) => a.SequenceEqual(b, StringComparer.OrdinalIgnoreCase);

    private static void CompareRoutines(SchemaSnapshot source, SchemaSnapshot target, List<ObjectDifference> result)
    {
        static string Key(RoutineInfo r) => $"{r.Schema}|{r.Signature}";
        var targets = target.Routines.ToDictionary(Key, StringComparer.OrdinalIgnoreCase);
        var matched = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var s in source.Routines)
        {
            if (!targets.TryGetValue(Key(s), out var t))
            {
                result.Add(new(ObjectCategory.Routine, s.Schema, s.Signature, DifferenceKind.Added, []));
                continue;
            }

            matched.Add(Key(s));
            if (DefinitionDetail(s.Definition, t.Definition) is { } d)
                result.Add(new(ObjectCategory.Routine, s.Schema, s.Signature, DifferenceKind.Modified, [d]));
        }

        foreach (var t in target.Routines.Where(t => !matched.Contains(Key(t))))
            result.Add(new(ObjectCategory.Routine, t.Schema, t.Signature, DifferenceKind.Removed, []));
    }

    private static DifferenceDetail? DefinitionDetail(string? source, string? target)
    {
        if (source is null && target is null) return null;
        if (source is null || target is null) return new(DetailKind.DefinitionUnknown, "", source, target);
        return Normalize(source) == Normalize(target) ? null : new(DetailKind.DefinitionChanged, "", source, target);
    }

    // Collapses whitespace runs to one space and trims.
    private static string Normalize(string text)
    {
        var sb = new StringBuilder(text.Length);
        var space = false;
        foreach (var ch in text)
        {
            if (char.IsWhiteSpace(ch)) { space = sb.Length > 0; continue; }
            if (space) { sb.Append(' '); space = false; }
            sb.Append(ch);
        }

        return sb.ToString();
    }
}
