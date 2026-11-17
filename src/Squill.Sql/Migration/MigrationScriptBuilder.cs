using System.Globalization;
using System.Text.RegularExpressions;
using Squill.Core;
using Squill.Core.Design;
using Squill.Core.Schema.Compare;
using Squill.Sql.Ddl;

namespace Squill.Sql.Migration;

/// <summary>
/// Turns a <see cref="SchemaComparison"/> into the SQL that makes the target look like the source. Statements come in this order: drops of
/// views, routines and tables, new tables (referenced before referencing), table alterations, views and routines, standalone indexes.
/// Shortcuts: the SQL Server <c>CREATE OR ALTER</c> statements are not separated by <c>GO</c> (run them statement by statement); a
/// PostgreSQL function whose return type changes needs a manual <c>DROP</c>; foreign-key cycles among dropped tables are not broken.
/// </summary>
public static class MigrationScriptBuilder
{
    private static readonly Regex CreateKeyword = new(@"^\s*CREATE\s+(?!OR\s+ALTER\b)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    /// <summary>Builds the script for the included differences.</summary>
    /// <param name="dialect">The engine of both snapshots.</param>
    /// <param name="source">The desired state.</param>
    /// <param name="target">The state the script changes.</param>
    /// <param name="comparison">The result of <see cref="SchemaComparer.Compare"/> on the same two snapshots.</param>
    /// <param name="included">Keys of the differences to apply; an excluded removal is written as a comment.</param>
    /// <param name="options">Header names, clock and transaction choice.</param>
    /// <returns>The script, its change count and its notes.</returns>
    /// <exception cref="NotSupportedException">The engine is Oracle.</exception>
    public static MigrationScript Build(
        DatabaseDialect dialect,
        SchemaSnapshot source,
        SchemaSnapshot target,
        SchemaComparison comparison,
        IReadOnlySet<ObjectKey> included,
        MigrationOptions options)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(comparison);
        ArgumentNullException.ThrowIfNull(included);
        ArgumentNullException.ThrowIfNull(options);
        if (dialect is not (DatabaseDialect.PostgreSql or DatabaseDialect.MySql or DatabaseDialect.SqlServer or DatabaseDialect.Sqlite))
            throw new NotSupportedException($"Migration scripts are not supported for {dialect}.");
        return new Run(dialect, source, target, comparison, included, options).Build();
    }

    /// <summary>One build: collects the statements of each phase and the notes.</summary>
    private sealed class Run(
        DatabaseDialect d, SchemaSnapshot source, SchemaSnapshot target, SchemaComparison comparison, IReadOnlySet<ObjectKey> included, MigrationOptions options)
    {
        private readonly IDdlGenerator _ddl = DdlGenerators.For(d);
        private readonly List<string> _drops = [], _creates = [], _alters = [], _objects = [], _indexes = [];
        private readonly List<MigrationNote> _notes = [];
        private bool _sqliteRebuild;

        /// <summary>Builds the script.</summary>
        /// <returns>The script.</returns>
        public MigrationScript Build()
        {
            var changes = comparison.Differences.Where(x => included.Contains(ObjectKey.Of(x))).ToList();
            var handledTables = changes.Where(x => x.Category == ObjectCategory.Table && x.Kind != DifferenceKind.Removed)
                .Select(x => ObjectKey.Of(x)).ToHashSet();

            DropPhase(comparison.Differences);
            CreateTables(changes.Where(x => x is { Category: ObjectCategory.Table, Kind: DifferenceKind.Added }));
            foreach (var x in changes.Where(x => x is { Category: ObjectCategory.Table, Kind: DifferenceKind.Modified }))
                AlterTable(x);
            foreach (var x in changes.Where(x => x.Category == ObjectCategory.View && x.Kind != DifferenceKind.Removed))
                CreateView(x);
            foreach (var x in changes.Where(x => x.Category == ObjectCategory.Routine && x.Kind != DifferenceKind.Removed))
                CreateRoutine(x);
            foreach (var x in comparison.Differences.Where(x => x.Category == ObjectCategory.Index))
                StandaloneIndex(x, handledTables);

            if (d == DatabaseDialect.MySql)
                _notes.Add(new(MigrationNoteKind.MySqlImplicitCommit, ""));

            return new(Assemble(changes.Count), changes.Count, [.. _notes.Distinct()]);
        }

        private string Assemble(int changeCount)
        {
            var blocks = new List<string>
            {
                $"-- {options.SourceName} -> {options.TargetName} . generated {options.GeneratedAt.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture)}",
            };
            var body = new List<string>([.. _drops, .. _creates, .. _alters, .. _objects, .. _indexes]);
            if (changeCount == 0 && body.Count == 0)
            {
                blocks.Add("-- no change selected");
                return string.Join("\n\n", blocks);
            }

            if (d == DatabaseDialect.MySql)
                blocks.Add("-- MySQL commits DDL implicitly: this script cannot be rolled back.");
            var wrap = options.WrapInTransaction && d != DatabaseDialect.MySql;
            if (_sqliteRebuild)
                blocks.Add("PRAGMA foreign_keys = OFF;");
            if (wrap)
                blocks.Add(d == DatabaseDialect.SqlServer ? "BEGIN TRANSACTION;" : "BEGIN;");
            blocks.AddRange(body);
            if (wrap)
                blocks.Add("COMMIT;");
            if (_sqliteRebuild)
                blocks.Add("PRAGMA foreign_key_check;\n\nPRAGMA foreign_keys = ON;");
            return string.Join("\n\n", blocks);
        }

        private static void Emit(List<string> to, IEnumerable<string> statements)
        {
            var list = statements.ToList();
            if (list.Count > 0)
                to.Add(DdlGenerators.ToScript(list));
        }

        private string Q(string? schema, string name) => SqlQuoting.QualifiedName(d == DatabaseDialect.Sqlite && schema == "main" ? null : schema, name, d);

        private static bool Eq(string? a, string? b) => string.Equals(a ?? "", b ?? "", StringComparison.OrdinalIgnoreCase);

        private static TableInfo? FindRelation(SchemaSnapshot s, ObjectDifference x) =>
            s.Tables.FirstOrDefault(t => Eq(t.Schema, x.Schema) && Eq(t.Name, x.Name) && t.Kind.IsViewLike() == (x.Category == ObjectCategory.View));

        private static RoutineInfo? FindRoutine(SchemaSnapshot s, ObjectDifference x) =>
            s.Routines.FirstOrDefault(r => Eq(r.Schema, x.Schema) && Eq(r.Signature, x.Name));

        private bool Included(ObjectDifference x) => included.Contains(ObjectKey.Of(x));

        private static TableDesign Design(SchemaSnapshot s, TableInfo t) => TableDesign.FromTable(t, s.Triggers);

        // ---- drops ----

        private void DropPhase(IReadOnlyList<ObjectDifference> all)
        {
            foreach (var x in all.Where(x => x is { Kind: DifferenceKind.Removed, Category: ObjectCategory.View }))
            {
                var v = FindRelation(target, x);
                var kw = v?.Kind == TableKind.MaterializedView ? "MATERIALIZED VIEW" : "VIEW";
                DropOrComment(x, $"DROP {kw} {Q(x.Schema, x.Name)}");
            }

            foreach (var x in all.Where(x => x is { Kind: DifferenceKind.Removed, Category: ObjectCategory.Routine }))
            {
                var r = FindRoutine(target, x);
                var kw = r?.Kind == RoutineKind.Procedure ? "PROCEDURE" : "FUNCTION";
                // PostgreSQL identifies a routine by its signature, the others by name.
                var name = d == DatabaseDialect.PostgreSql
                    ? (string.IsNullOrEmpty(x.Schema) ? "" : SqlQuoting.Quote(x.Schema, d) + ".") + x.Name
                    : Q(x.Schema, r?.Name ?? x.Name);
                DropOrComment(x, $"DROP {kw} {name}");
            }

            var removedTables = all.Where(x => x is { Kind: DifferenceKind.Removed, Category: ObjectCategory.Table }).ToList();
            var byKey = removedTables.ToDictionary(ObjectKey.Of);
            var tables = removedTables.Select(x => FindRelation(target, x)).OfType<TableInfo>().ToList();
            foreach (var (t, _) in Order(tables).AsEnumerable().Reverse())
            {
                var x = byKey[new ObjectKey(ObjectCategory.Table, t.Schema, t.Name)];
                if (Included(x))
                    _notes.Add(new(MigrationNoteKind.DropsData, t.Name));
                DropOrComment(x, _ddl.DropTable(t.Schema, t.Name)[0]);
            }

            foreach (var x in all.Where(x => x is { Kind: DifferenceKind.Removed, Category: ObjectCategory.Index }))
            {
                if (ResolveIndex(x, target) is not var (table, index))
                    continue;
                var sql = d is DatabaseDialect.MySql or DatabaseDialect.SqlServer
                    ? $"DROP INDEX {SqlQuoting.Quote(index.Name, d)} ON {Q(table.Schema, table.Name)}"
                    : $"DROP INDEX {Q(table.Schema, index.Name)}";
                // A removed index of a table that is altered here is dropped by the alteration itself.
                if (!Included(x))
                    _indexes.Add($"-- {sql};  (excluded)");
                else if (!TableIsAltered(table))
                    Emit(_indexes, [sql]);
            }
        }

        private bool TableIsAltered(TableInfo t) =>
            comparison.Differences.Any(x => x is { Category: ObjectCategory.Table, Kind: DifferenceKind.Modified } && Eq(x.Schema, t.Schema) && Eq(x.Name, t.Name) && Included(x));

        private void DropOrComment(ObjectDifference x, string sql)
        {
            if (Included(x))
                Emit(_drops, [sql]);
            else
                _drops.Add($"-- {sql};  (excluded)");
        }

        // ---- tables ----

        private void CreateTables(IEnumerable<ObjectDifference> added)
        {
            var tables = added.Select(x => FindRelation(source, x)).OfType<TableInfo>().ToList();
            var deferred = new List<string>();
            foreach (var (t, deferredFks) in Order(tables))
            {
                var design = Design(source, t);
                if (deferredFks.Count > 0)
                    design = design with { ForeignKeys = [.. design.ForeignKeys.Where(f => !deferredFks.Contains(f.Name))] };
                Emit(_creates, _ddl.CreateTable(design));
                foreach (var f in deferredFks)
                {
                    var fk = t.ForeignKeys.First(k => k.Name == f);
                    var fd = new ForeignKeyDesign(fk.Name, fk.Name, fk.Columns, fk.ReferencedSchema, fk.ReferencedTable, fk.ReferencedColumns, fk.OnDelete, fk.OnUpdate);
                    deferred.Add($"ALTER TABLE {Q(t.Schema, t.Name)} ADD {DdlWriter.ForeignKey(fd, d)}");
                }
            }

            foreach (var sql in deferred)
                Emit(_creates, [sql]);
        }

        // Dependency order (referenced first). A table caught in a cycle is placed anyway and its foreign keys to tables not yet created are returned
        // so the caller can add them afterwards; SQLite checks foreign keys lazily, so nothing is deferred there.
        private List<(TableInfo Table, HashSet<string> DeferredForeignKeys)> Order(List<TableInfo> tables)
        {
            var result = new List<(TableInfo, HashSet<string>)>();
            var remaining = new List<TableInfo>(tables);
            bool Pending(TableInfo t, TableInfo other) => !ReferenceEquals(t, other) && remaining.Contains(other);
            TableInfo? Referenced(ForeignKeyInfo f) =>
                remaining.FirstOrDefault(o => Eq(o.Name, f.ReferencedTable) && Eq(o.Schema, f.ReferencedSchema ?? o.Schema));
            while (remaining.Count > 0)
            {
                var next = remaining.FirstOrDefault(t => !t.ForeignKeys.Any(f => Referenced(f) is { } o && Pending(t, o)));
                var deferred = new HashSet<string>();
                if (next is null)
                {
                    next = remaining[0];
                    if (d != DatabaseDialect.Sqlite)
                        deferred = [.. next.ForeignKeys.Where(f => Referenced(f) is { } o && Pending(next, o)).Select(f => f.Name)];
                }

                remaining.Remove(next);
                result.Add((next, deferred));
            }

            return result;
        }

        private void AlterTable(ObjectDifference x)
        {
            var s = FindRelation(source, x);
            var t = FindRelation(target, x);
            if (s is null || t is null)
                return;
            foreach (var detail in x.Details)
            {
                if (detail.Kind == DetailKind.ColumnRemoved)
                    _notes.Add(new(MigrationNoteKind.DropsData, $"{x.Name}.{detail.Subject}"));
                else if (detail.Kind == DetailKind.ColumnChanged
                    && s.Columns.FirstOrDefault(c => Eq(c.Name, detail.Subject)) is { } sc
                    && t.Columns.FirstOrDefault(c => Eq(c.Name, detail.Subject)) is { } tc
                    && MayLoseData(sc.DataType, tc.DataType))
                    _notes.Add(new(MigrationNoteKind.NarrowsColumn, $"{x.Name}.{detail.Subject}"));
            }

            var original = Design(target, t);
            var statements = _ddl.Alter(original, Edited(Design(source, s), original)).ToList();
            if (d == DatabaseDialect.Sqlite && statements.Contains("PRAGMA foreign_keys = OFF"))
            {
                // The PRAGMAs only work outside a transaction: Assemble writes them around BEGIN / COMMIT.
                _sqliteRebuild = true;
                _notes.Add(new(MigrationNoteKind.SqliteRebuild, x.Name));
                statements.RemoveAll(sql => sql.StartsWith("PRAGMA ", StringComparison.Ordinal));
            }

            Emit(_alters, statements);
        }

        // The designer marks what exists in the database with OriginalName; here "existing" means present on the target, anything else is new.
        private static TableDesign Edited(TableDesign desired, TableDesign original) => desired with
        {
            Columns = [.. desired.Columns.Select(c => c with { OriginalName = original.Columns.FirstOrDefault(o => Eq(o.Name, c.Name))?.Name })],
            Indexes = [.. desired.Indexes.Select(i => i with { OriginalName = original.Indexes.FirstOrDefault(o => Eq(o.Name, i.Name))?.Name })],
            ForeignKeys = [.. desired.ForeignKeys.Select(f => f with { OriginalName = original.ForeignKeys.FirstOrDefault(o => Eq(o.Name, f.Name))?.Name })],
        };

        // The type text is the engine's own (for example "varchar(20)", "numeric(10,2)"); another base type counts as lossy.
        private static bool MayLoseData(string sourceType, string targetType)
        {
            static (string Base, string[] Args) Parse(string type)
            {
                var t = type.Trim().ToLowerInvariant();
                var open = t.IndexOf('(', StringComparison.Ordinal);
                var close = t.IndexOf(')', StringComparison.Ordinal);
                return open < 0 || close < open ? (t, []) : (t[..open].Trim(), t[(open + 1)..close].Split(',', StringSplitOptions.TrimEntries));
            }

            var (sb, sa) = Parse(sourceType);
            var (tb, ta) = Parse(targetType);
            if (sb != tb)
                return true;
            for (var i = 0; i < Math.Min(sa.Length, ta.Length); i++)
            {
                if (sa[i] == "max")
                    continue;
                if (ta[i] == "max" || (int.TryParse(sa[i], CultureInfo.InvariantCulture, out var a) && int.TryParse(ta[i], CultureInfo.InvariantCulture, out var b) && a < b))
                    return true;
            }

            return false;
        }

        // ---- views and routines ----

        private void CreateView(ObjectDifference x)
        {
            var v = FindRelation(source, x);
            var def = Clean(v?.Definition);
            if (v is null || def is null)
            {
                _objects.Add($"-- definition not readable: {x.Name}");
                return;
            }

            var q = Q(v.Schema, v.Name);
            var drop = x.Kind == DifferenceKind.Modified && (d == DatabaseDialect.Sqlite || v.Kind == TableKind.MaterializedView);
            var statements = new List<string>();
            if (drop)
                statements.Add($"DROP {(v.Kind == TableKind.MaterializedView ? "MATERIALIZED VIEW" : "VIEW")} {q}");
            statements.Add(d switch
            {
                DatabaseDialect.SqlServer => CreateOrAlter(def),
                DatabaseDialect.Sqlite => $"CREATE VIEW {q} AS\n{def}",
                _ when v.Kind == TableKind.MaterializedView => $"CREATE MATERIALIZED VIEW {q} AS\n{def}",
                _ => $"CREATE OR REPLACE VIEW {q} AS\n{def}",
            });
            Emit(_objects, statements);
        }

        private void CreateRoutine(ObjectDifference x)
        {
            var r = FindRoutine(source, x);
            var def = Clean(r?.Definition);
            if (r is null || def is null)
            {
                _objects.Add($"-- definition not readable: {x.Name}");
                return;
            }

            var statements = new List<string>();
            switch (d)
            {
                case DatabaseDialect.PostgreSql:
                    statements.Add(def);
                    break;
                case DatabaseDialect.SqlServer:
                    statements.Add(CreateOrAlter(def));
                    break;
                default:
                    // MySQL stores only the body, so the header is rebuilt and the routine is dropped first.
                    var kw = r.Kind == RoutineKind.Procedure ? "PROCEDURE" : "FUNCTION";
                    if (x.Kind == DifferenceKind.Modified)
                        statements.Add($"DROP {kw} {Q(r.Schema, r.Name)}");
                    var returns = r.ReturnType is { Length: > 0 } type ? $"\nRETURNS {type}" : "";
                    statements.Add($"CREATE {kw} {Q(null, r.Name)}{Parameters(r.Signature)}{returns}\n{def}");
                    break;
            }

            Emit(_objects, statements);
        }

        // MySQL signatures read "name(IN a int, ...)"; the parameter list is kept as the engine reports it.
        private static string Parameters(string signature)
        {
            var open = signature.IndexOf('(', StringComparison.Ordinal);
            return open < 0 ? "()" : signature[open..];
        }

        private static string? Clean(string? definition)
        {
            if (string.IsNullOrWhiteSpace(definition))
                return null;
            // PostgreSQL's pg_get_viewdef ends with a semicolon, which the script adds itself.
            return definition.Trim().TrimEnd(';').TrimEnd();
        }

        private static string CreateOrAlter(string definition) => CreateKeyword.Replace(definition, "CREATE OR ALTER ", 1);

        // ---- standalone indexes ----

        private void StandaloneIndex(ObjectDifference x, HashSet<ObjectKey> handledTables)
        {
            if (x.Kind == DifferenceKind.Removed || !Included(x))
                return;
            if (ResolveIndex(x, source) is not var (table, index) || handledTables.Contains(new ObjectKey(ObjectCategory.Table, table.Schema, table.Name)))
                return;
            var schema = d == DatabaseDialect.Sqlite && table.Schema == "main" ? null : table.Schema;
            var statements = new List<string>();
            if (x.Kind == DifferenceKind.Modified)
                statements.Add(d is DatabaseDialect.MySql or DatabaseDialect.SqlServer
                    ? $"DROP INDEX {SqlQuoting.Quote(index.Name, d)} ON {Q(table.Schema, table.Name)}"
                    : $"DROP INDEX {Q(table.Schema, index.Name)}");
            statements.Add(DdlWriter.CreateIndex(schema, Q(table.Schema, table.Name), new IndexDesign(index.Name, index.Name, index.IsUnique, index.Columns), d));
            Emit(_indexes, statements);
        }

        // The name of an index difference is "table.index"; the table is found by trying every table of the schema.
        private static (TableInfo Table, IndexInfo Index)? ResolveIndex(ObjectDifference x, SchemaSnapshot snapshot)
        {
            foreach (var t in snapshot.Tables.Where(t => t.Kind == TableKind.Table && Eq(t.Schema, x.Schema)))
                foreach (var i in t.Indexes)
                    if (Eq($"{t.Name}.{i.Name}", x.Name))
                        return (t, i);
            return null;
        }
    }
}
