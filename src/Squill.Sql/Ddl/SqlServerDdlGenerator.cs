using Squill.Core;
using Squill.Core.Design;

namespace Squill.Sql.Ddl;

/// <summary>DDL generator for SQL Server.</summary>
internal sealed class SqlServerDdlGenerator : IDdlGenerator
{
    /// <inheritdoc/>
    public IReadOnlyList<string> CreateTable(TableDesign table) => DdlWriter.CreateTable(table, DatabaseDialect.SqlServer);

    /// <inheritdoc/>
    public IReadOnlyList<string> DropTable(string? schema, string name) => DdlWriter.DropTable(schema, name, DatabaseDialect.SqlServer);

    /// <inheritdoc/>
    public IReadOnlyList<string> Alter(TableDesign original, TableDesign edited) =>
        throw new NotSupportedException("ALTER TABLE for SQL Server is implemented by card P7-13");
}
