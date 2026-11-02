using Squill.Core;
using Squill.Core.Design;

namespace Squill.Sql.Ddl;

/// <summary>
/// DDL generator for MySQL. Known limit: MySQL commits DDL implicitly, so a failure halfway through
/// <see cref="Alter"/> leaves the earlier statements applied (<c>UsesTransactionalDdl</c> is false).
/// </summary>
internal sealed class MySqlDdlGenerator : IDdlGenerator
{
    private const DatabaseDialect D = DatabaseDialect.MySql;

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
                RenameTable r => $"RENAME TABLE {t} TO {SqlQuoting.QualifiedName(original.Schema, r.To, D)}",
                AddColumn a => $"ALTER TABLE {t} ADD COLUMN {DdlWriter.Column(a.Column, D, false)}",
                DropColumn c => $"ALTER TABLE {t} DROP COLUMN {Q(c.Name)}",
                RenameColumn r => $"ALTER TABLE {t} RENAME COLUMN {Q(r.From)} TO {Q(r.To)}",
                // MySQL cannot change one aspect of a column: restate the whole definition.
                AlterColumn a => $"ALTER TABLE {t} MODIFY COLUMN {DdlWriter.Column(a.After, D, false)}",
                DropPrimaryKey => $"ALTER TABLE {t} DROP PRIMARY KEY",
                AddPrimaryKey p => $"ALTER TABLE {t} ADD PRIMARY KEY ({DdlWriter.Columns(p.Columns, D)})",
                CreateIndex i => DdlWriter.CreateIndex(original.Schema, t, i.Index, D),
                DropIndex i => $"DROP INDEX {Q(i.Name)} ON {t}",
                AddForeignKey f => $"ALTER TABLE {t} ADD {DdlWriter.ForeignKey(f.ForeignKey, D)}",
                DropForeignKey f => $"ALTER TABLE {t} DROP FOREIGN KEY {Q(f.Name)}",
                _ => throw new NotSupportedException($"Unsupported schema operation {op.GetType().Name}"),
            });
        }

        return statements;
    }

    private static string Q(string identifier) => SqlQuoting.Quote(identifier, D);
}
