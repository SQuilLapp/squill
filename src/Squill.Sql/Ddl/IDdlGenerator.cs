using Squill.Core.Design;

namespace Squill.Sql.Ddl;

/// <summary>Generates DDL statements for one database engine. Each returned string is a single statement without a trailing semicolon.</summary>
public interface IDdlGenerator
{
    /// <summary>Generates <c>CREATE TABLE</c> followed by one <c>CREATE INDEX</c> per index.</summary>
    /// <param name="table">The table to create.</param>
    /// <returns>The statements, in execution order.</returns>
    /// <exception cref="ArgumentException">The design has no column.</exception>
    IReadOnlyList<string> CreateTable(TableDesign table);

    /// <summary>Generates <c>DROP TABLE</c>.</summary>
    /// <param name="schema">Schema of the table, if any.</param>
    /// <param name="name">Table name.</param>
    /// <returns>The statements, in execution order.</returns>
    IReadOnlyList<string> DropTable(string? schema, string name);

    /// <summary>Generates the statements that turn one design into another.</summary>
    /// <param name="original">The design as read from the database.</param>
    /// <param name="edited">The design after editing.</param>
    /// <returns>The statements, in execution order.</returns>
    /// <exception cref="NotSupportedException">ALTER is not implemented for this engine yet.</exception>
    IReadOnlyList<string> Alter(TableDesign original, TableDesign edited);
}
