using Squill.Core;
using Squill.Core.Design;

namespace Squill.Sql.Ddl;

/// <summary>DDL generator for SQL Server.</summary>
internal sealed class SqlServerDdlGenerator : IDdlGenerator
{
    private const DatabaseDialect D = DatabaseDialect.SqlServer;

    /// <inheritdoc/>
    public IReadOnlyList<string> CreateTable(TableDesign table) => DdlWriter.CreateTable(table, D);

    /// <inheritdoc/>
    public IReadOnlyList<string> DropTable(string? schema, string name) => DdlWriter.DropTable(schema, name, D);

    /// <inheritdoc/>
    public IReadOnlyList<string> Alter(TableDesign original, TableDesign edited)
    {
        var t = SqlQuoting.QualifiedName(original.Schema, original.Name, D);
        var plain = Plain(original.Schema, original.Name);
        var statements = new List<string>();
        foreach (var op in TableDiff.Compute(original, edited))
        {
            switch (op)
            {
                case RenameTable r:
                    statements.Add($"EXEC sp_rename {NLiteral(plain)}, {NLiteral(r.To)}");
                    break;
                case AddColumn a:
                    statements.Add($"ALTER TABLE {t} ADD {DdlWriter.Column(a.Column, D, false)}");
                    break;
                case DropColumn c:
                    if (original.Columns.Any(o => o.Name == c.Name && !string.IsNullOrWhiteSpace(o.DefaultExpression)))
                    {
                        statements.Add(DropDefault(t, plain, c.Name));
                    }

                    statements.Add($"ALTER TABLE {t} DROP COLUMN {Q(c.Name)}");
                    break;
                case RenameColumn r:
                    statements.Add($"EXEC sp_rename {NLiteral($"{plain}.{r.From}")}, {NLiteral(r.To)}, N'COLUMN'");
                    break;
                case AlterColumn a:
                    AlterColumn(statements, t, plain, a);
                    break;
                case DropPrimaryKey p:
                    statements.Add($"ALTER TABLE {t} DROP CONSTRAINT {Q(p.Name ?? throw new InvalidOperationException("SQL Server primary keys are always named."))}");
                    break;
                case AddPrimaryKey p:
                    statements.Add($"ALTER TABLE {t} ADD PRIMARY KEY ({DdlWriter.Columns(p.Columns, D)})");
                    break;
                case CreateIndex i:
                    statements.Add(DdlWriter.CreateIndex(original.Schema, t, i.Index, D));
                    break;
                case DropIndex i:
                    statements.Add($"DROP INDEX {Q(i.Name)} ON {t}");
                    break;
                case AddForeignKey f:
                    statements.Add($"ALTER TABLE {t} ADD {DdlWriter.ForeignKey(f.ForeignKey, D)}");
                    break;
                case DropForeignKey f:
                    statements.Add($"ALTER TABLE {t} DROP CONSTRAINT {Q(f.Name)}");
                    break;
                default:
                    throw new NotSupportedException($"Unsupported schema operation {op.GetType().Name}");
            }
        }

        return statements;
    }

    private static string Q(string identifier) => SqlQuoting.Quote(identifier, D);

    private static string Plain(string? schema, string name) => string.IsNullOrEmpty(schema) ? name : $"{schema}.{name}";

    private static string NLiteral(string value) => "N" + SqlQuoting.Literal(value);

    private static bool HasDefault(ColumnDesign c) => !string.IsNullOrWhiteSpace(c.DefaultExpression);

    // Default constraints are auto-named, so the batch looks the name up before dropping it.
    private static string DropDefault(string t, string plain, string column) =>
        "DECLARE @n sysname = (SELECT dc.name FROM sys.default_constraints dc JOIN sys.columns c ON c.object_id = dc.parent_object_id AND c.column_id = dc.parent_column_id "
        + $"WHERE dc.parent_object_id = OBJECT_ID({NLiteral(plain)}) AND c.name = {NLiteral(column)}); "
        + $"IF @n IS NOT NULL EXEC({NLiteral($"ALTER TABLE {t} DROP CONSTRAINT ")} + QUOTENAME(@n))";

    private static void AlterColumn(List<string> statements, string t, string plain, AlterColumn a)
    {
        var typeChanged = a.Changes.HasFlag(ColumnChange.Type);
        var defaultChanged = a.Changes.HasFlag(ColumnChange.Default);
        var name = a.After.Name;

        if ((typeChanged || defaultChanged) && HasDefault(a.Before))
        {
            statements.Add(DropDefault(t, plain, name));
        }

        if (typeChanged || a.Changes.HasFlag(ColumnChange.Nullability))
        {
            statements.Add($"ALTER TABLE {t} ALTER COLUMN {Q(name)} {a.After.DataType} {(a.After.IsNullable ? "NULL" : "NOT NULL")}");
        }

        if ((typeChanged || defaultChanged) && HasDefault(a.After))
        {
            statements.Add($"ALTER TABLE {t} ADD DEFAULT {a.After.DefaultExpression} FOR {Q(name)}");
        }
    }
}
