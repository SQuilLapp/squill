using Squill.Core;
using Squill.Core.Design;

namespace Squill.Sql.Ddl;

/// <summary>
/// DDL generator for Oracle. Known limit: Oracle commits DDL implicitly, so a failure halfway through <see cref="Alter"/> leaves
/// the earlier statements applied (<c>UsesTransactionalDdl</c> is false), and a type change the server refuses on a non-empty
/// column is surfaced as the engine error rather than emulated by a rebuild.
/// </summary>
internal sealed class OracleDdlGenerator : IDdlGenerator
{
    private const DatabaseDialect D = DatabaseDialect.Oracle;

    /// <inheritdoc/>
    public IReadOnlyList<string> CreateTable(TableDesign table) => DdlWriter.CreateTable(table, D);

    /// <inheritdoc/>
    public IReadOnlyList<string> DropTable(string? schema, string name) => DdlWriter.DropTable(schema, name, D);

    /// <inheritdoc/>
    public IReadOnlyList<string> Alter(TableDesign original, TableDesign edited)
    {
        var t = SqlQuoting.QualifiedName(original.Schema, original.Name, D);
        var statements = new List<string>();
        foreach (var op in TableDiff.Compute(original, edited))
        {
            statements.Add(op switch
            {
                RenameTable r => $"ALTER TABLE {t} RENAME TO {Q(r.To)}",
                AddColumn a => $"ALTER TABLE {t} ADD {DdlWriter.Column(a.Column, D, false)}",
                DropColumn c => $"ALTER TABLE {t} DROP COLUMN {Q(c.Name)}",
                RenameColumn r => $"ALTER TABLE {t} RENAME COLUMN {Q(r.From)} TO {Q(r.To)}",
                AlterColumn a => $"ALTER TABLE {t} MODIFY {ModifyColumn(a)}",
                DropPrimaryKey => $"ALTER TABLE {t} DROP PRIMARY KEY",
                AddPrimaryKey p => $"ALTER TABLE {t} ADD PRIMARY KEY ({DdlWriter.Columns(p.Columns, D)})",
                CreateIndex i => DdlWriter.CreateIndex(original.Schema, t, i.Index, D),
                DropIndex i => $"DROP INDEX {SqlQuoting.QualifiedName(original.Schema, i.Name, D)}",
                AddForeignKey f => $"ALTER TABLE {t} ADD {DdlWriter.ForeignKey(f.ForeignKey, D)}",
                DropForeignKey f => $"ALTER TABLE {t} DROP CONSTRAINT {Q(f.Name)}",
                _ => throw new NotSupportedException($"Unsupported schema operation {op.GetType().Name}"),
            });
        }

        return statements;
    }

    private static string Q(string identifier) => SqlQuoting.Quote(identifier, D);

    // Only the changed aspects are restated: Oracle raises ORA-01442/ORA-01451 when NOT NULL / NULL is repeated.
    private static string ModifyColumn(AlterColumn a)
    {
        var sql = Q(a.After.Name);
        if (a.Changes.HasFlag(ColumnChange.Type))
            sql += " " + a.After.DataType;
        if (a.Changes.HasFlag(ColumnChange.Default))
            sql += $" DEFAULT {(string.IsNullOrWhiteSpace(a.After.DefaultExpression) ? "NULL" : a.After.DefaultExpression)}";
        if (a.Changes.HasFlag(ColumnChange.Nullability))
            sql += a.After.IsNullable ? " NULL" : " NOT NULL";
        return sql;
    }
}
