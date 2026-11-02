using Squill.Core;
using Squill.Core.Design;

namespace Squill.Sql.Ddl;

/// <summary>DDL generator for SQLite.</summary>
internal sealed class SqliteDdlGenerator : IDdlGenerator
{
    /// <inheritdoc/>
    public IReadOnlyList<string> CreateTable(TableDesign table) => DdlWriter.CreateTable(table, DatabaseDialect.Sqlite);

    /// <inheritdoc/>
    public IReadOnlyList<string> DropTable(string? schema, string name) => DdlWriter.DropTable(schema, name, DatabaseDialect.Sqlite);

    /// <inheritdoc/>
    public IReadOnlyList<string> Alter(TableDesign original, TableDesign edited) =>
        throw new NotSupportedException("ALTER TABLE for SQLite is implemented by card P7-14");
}
