using Squill.Core;
using Squill.Core.Design;

namespace Squill.Sql.Ddl;

/// <summary>DDL generator for SQLite.</summary>
internal sealed class SqliteDdlGenerator : IDdlGenerator
{
    private const DatabaseDialect D = DatabaseDialect.Sqlite;

    /// <inheritdoc/>
    public IReadOnlyList<string> CreateTable(TableDesign table) => DdlWriter.CreateTable(table, D);

    /// <inheritdoc/>
    public IReadOnlyList<string> DropTable(string? schema, string name) => DdlWriter.DropTable(schema, name, D);

    /// <summary>
    /// Generates native <c>ALTER TABLE</c> statements when SQLite supports every change, otherwise one table rebuild for the whole edit.
    /// A rebuild starts with <c>PRAGMA foreign_keys = OFF</c> and ends with <c>PRAGMA foreign_key_check</c> and <c>PRAGMA foreign_keys = ON</c>:
    /// the executor must run those leading and trailing <c>PRAGMA</c> statements outside the transaction and everything between them inside it.
    /// </summary>
    /// <param name="original">The design as read from the database.</param>
    /// <param name="edited">The design after editing.</param>
    /// <returns>The statements, in execution order; empty when nothing changed.</returns>
    public IReadOnlyList<string> Alter(TableDesign original, TableDesign edited)
    {
        ArgumentNullException.ThrowIfNull(original);
        ArgumentNullException.ThrowIfNull(edited);

        var ops = TableDiff.Compute(original, edited);
        if (ops.Count == 0)
        {
            return [];
        }

        var schema = original.Schema == "main" ? null : original.Schema;
        var t = SqlQuoting.QualifiedName(schema, original.Name, D);
        var statements = new List<string>();
        foreach (var op in ops)
        {
            switch (op)
            {
                case RenameTable r:
                    statements.Add($"ALTER TABLE {t} RENAME TO {Q(r.To)}");
                    break;
                case RenameColumn r:
                    statements.Add($"ALTER TABLE {t} RENAME COLUMN {Q(r.From)} TO {Q(r.To)}");
                    break;
                case CreateIndex i:
                    statements.Add(DdlWriter.CreateIndex(schema, t, i.Index, D));
                    break;
                case DropIndex i:
                    statements.Add($"DROP INDEX {SqlQuoting.QualifiedName(schema, i.Name, D)}");
                    break;
                case AddColumn a when CanAddNatively(a.Column, edited):
                    statements.Add($"ALTER TABLE {t} ADD COLUMN {DdlWriter.Column(a.Column, D, false)}");
                    break;
                case DropColumn c when CanDropNatively(c.Name, original):
                    statements.Add($"ALTER TABLE {t} DROP COLUMN {Q(c.Name)}");
                    break;
                default:
                    return Rebuild(original, edited, schema, t);
            }
        }

        return statements;
    }

    private static string Q(string identifier) => SqlQuoting.Quote(identifier, D);

    private static bool CanAddNatively(ColumnDesign c, TableDesign edited) =>
        (c.IsNullable || !string.IsNullOrWhiteSpace(c.DefaultExpression)) && !c.IsAutoIncrement && !edited.PrimaryKey.Contains(c.Name);

    private static bool CanDropNatively(string column, TableDesign original) =>
        !original.PrimaryKey.Contains(column)
        && !original.Indexes.Any(i => i.Columns.Contains(column))
        && !original.ForeignKeys.Any(f => f.Columns.Contains(column));

    // Views and triggers that depend on the table are not recreated; the designer warns about them (P7-21).
    private static List<string> Rebuild(TableDesign original, TableDesign edited, string? schema, string t)
    {
        var temp = edited.Name + "__squill_new";
        var tempTable = SqlQuoting.QualifiedName(schema, temp, D);
        var statements = new List<string> { "PRAGMA foreign_keys = OFF" };
        statements.AddRange(DdlWriter.CreateTable(edited with { Schema = schema, Name = temp, Indexes = [] }, D));

        var kept = edited.Columns.Where(c => c.OriginalName is not null && original.Columns.Any(o => o.Name == c.OriginalName)).ToList();
        if (kept.Count > 0)
        {
            statements.Add($"INSERT INTO {tempTable} ({DdlWriter.Columns(kept.Select(c => c.Name), D)}) "
                + $"SELECT {DdlWriter.Columns(kept.Select(c => c.OriginalName!), D)} FROM {t}");
        }

        statements.Add($"DROP TABLE {t}");
        // Back to the original name first: a rename after the triggers exist lets SQLite rewrite their text.
        statements.Add($"ALTER TABLE {tempTable} RENAME TO {Q(original.Name)}");
        statements.AddRange(edited.Indexes.Select(i => DdlWriter.CreateIndex(schema, t, i, D)));
        // shortcut: trigger bodies keep their old text, so one that names a renamed or dropped column fails when it fires; upgrade by rewriting the body.
        statements.AddRange(original.Triggers);
        if (original.Name != edited.Name)
        {
            statements.Add($"ALTER TABLE {t} RENAME TO {Q(edited.Name)}");
        }

        statements.Add("PRAGMA foreign_key_check");
        statements.Add("PRAGMA foreign_keys = ON");
        return statements;
    }
}
