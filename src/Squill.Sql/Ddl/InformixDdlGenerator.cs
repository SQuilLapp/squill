using Squill.Core;
using Squill.Core.Design;

namespace Squill.Sql.Ddl;

/// <summary>DDL generator for Informix: only <c>DROP TABLE</c> for now, the designer statements are a later card.</summary>
internal sealed class InformixDdlGenerator : IDdlGenerator
{
    /// <inheritdoc/>
    public IReadOnlyList<string> CreateTable(TableDesign table) => throw new NotSupportedException("The table designer is not available for Informix yet.");

    /// <inheritdoc/>
    public IReadOnlyList<string> DropTable(string? schema, string name) => DdlWriter.DropTable(schema, name, DatabaseDialect.Informix);

    /// <inheritdoc/>
    public IReadOnlyList<string> Alter(TableDesign original, TableDesign edited) => throw new NotSupportedException("The table designer is not available for Informix yet.");
}
