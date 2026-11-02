using Squill.Core;
using Squill.Core.Design;

namespace Squill.Sql.Ddl;

/// <summary>DDL generator for PostgreSQL.</summary>
internal sealed class PostgresDdlGenerator : IDdlGenerator
{
    private const DatabaseDialect D = DatabaseDialect.PostgreSql;

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
            switch (op)
            {
                case RenameTable r:
                    statements.Add($"ALTER TABLE {t} RENAME TO {Q(r.To)}");
                    break;
                case AddColumn a:
                    statements.Add($"ALTER TABLE {t} ADD COLUMN {DdlWriter.Column(a.Column, D, false)}");
                    break;
                case DropColumn c:
                    statements.Add($"ALTER TABLE {t} DROP COLUMN {Q(c.Name)}");
                    break;
                case RenameColumn r:
                    statements.Add($"ALTER TABLE {t} RENAME COLUMN {Q(r.From)} TO {Q(r.To)}");
                    break;
                case AlterColumn a:
                    AlterColumn(statements, t, a);
                    break;
                case DropPrimaryKey p:
                    statements.Add($"ALTER TABLE {t} DROP CONSTRAINT {Q(p.Name ?? original.Name + "_pkey")}");
                    break;
                case AddPrimaryKey p:
                    statements.Add($"ALTER TABLE {t} ADD PRIMARY KEY ({DdlWriter.Columns(p.Columns, D)})");
                    break;
                case CreateIndex i:
                    statements.Add(DdlWriter.CreateIndex(original.Schema, t, i.Index, D));
                    break;
                case DropIndex i:
                    statements.Add($"DROP INDEX {SqlQuoting.QualifiedName(original.Schema, i.Name, D)}");
                    break;
                case AddForeignKey f:
                    statements.Add($"ALTER TABLE {t} ADD {DdlWriter.ForeignKey(f.ForeignKey, D)}");
                    break;
                case DropForeignKey f:
                    statements.Add($"ALTER TABLE {t} DROP CONSTRAINT {Q(f.Name)}");
                    break;
            }
        }

        return statements;
    }

    private static string Q(string identifier) => SqlQuoting.Quote(identifier, D);

    private static void AlterColumn(List<string> statements, string t, AlterColumn a)
    {
        var column = $"ALTER TABLE {t} ALTER COLUMN {Q(a.After.Name)}";
        if (a.Changes.HasFlag(ColumnChange.Type))
        {
            statements.Add($"{column} TYPE {a.After.DataType} USING {Q(a.After.Name)}::{a.After.DataType}");
        }

        if (a.Changes.HasFlag(ColumnChange.Nullability))
        {
            statements.Add($"{column} {(a.After.IsNullable ? "DROP" : "SET")} NOT NULL");
        }

        if (a.Changes.HasFlag(ColumnChange.Default))
        {
            statements.Add(string.IsNullOrWhiteSpace(a.After.DefaultExpression)
                ? $"{column} DROP DEFAULT"
                : $"{column} SET DEFAULT {a.After.DefaultExpression}");
        }
    }
}
