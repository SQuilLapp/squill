using Squill.Core;
using Squill.Core.Design;

namespace Squill.Sql.Ddl;

/// <summary>DDL generator for MySQL.</summary>
internal sealed class MySqlDdlGenerator : IDdlGenerator
{
    /// <inheritdoc/>
    public IReadOnlyList<string> CreateTable(TableDesign table) => DdlWriter.CreateTable(table, DatabaseDialect.MySql);

    /// <inheritdoc/>
    public IReadOnlyList<string> DropTable(string? schema, string name) => DdlWriter.DropTable(schema, name, DatabaseDialect.MySql);

    /// <inheritdoc/>
    public IReadOnlyList<string> Alter(TableDesign original, TableDesign edited) =>
        throw new NotSupportedException("ALTER TABLE for MySQL is implemented by card P7-12");
}
