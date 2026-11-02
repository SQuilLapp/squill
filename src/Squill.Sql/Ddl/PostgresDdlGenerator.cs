using Squill.Core;
using Squill.Core.Design;

namespace Squill.Sql.Ddl;

/// <summary>DDL generator for PostgreSQL.</summary>
internal sealed class PostgresDdlGenerator : IDdlGenerator
{
    /// <inheritdoc/>
    public IReadOnlyList<string> CreateTable(TableDesign table) => DdlWriter.CreateTable(table, DatabaseDialect.PostgreSql);

    /// <inheritdoc/>
    public IReadOnlyList<string> DropTable(string? schema, string name) => DdlWriter.DropTable(schema, name, DatabaseDialect.PostgreSql);

    /// <inheritdoc/>
    public IReadOnlyList<string> Alter(TableDesign original, TableDesign edited) =>
        throw new NotSupportedException("ALTER TABLE for PostgreSQL is implemented by card P7-11");
}
