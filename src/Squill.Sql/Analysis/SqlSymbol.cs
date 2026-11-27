using Squill.Core;

namespace Squill.Sql.Analysis;

/// <summary>What a name in a script stands for.</summary>
public enum SqlSymbolKind
{
    /// <summary>A table.</summary>
    Table,

    /// <summary>A view or materialized view.</summary>
    View,

    /// <summary>A column of a table or view.</summary>
    Column,

    /// <summary>An alias given to a table in <c>FROM</c>.</summary>
    Alias,

    /// <summary>A common table expression.</summary>
    Cte,

    /// <summary>A sub-query in <c>FROM</c>, named by its alias.</summary>
    DerivedTable,

    /// <summary>An output column of a common table expression or of a sub-query in <c>FROM</c>.</summary>
    DerivedColumn,

    /// <summary>An output name given in a select list, usable in <c>ORDER BY</c>.</summary>
    SelectAlias,
}

/// <summary>A resolved name: what it is and where it is defined.</summary>
/// <param name="Kind">What the name stands for.</param>
/// <param name="Name">The name, spelled as the schema or the declaration spells it.</param>
/// <param name="Schema">The schema of a table, view or column owner, or <see langword="null"/>.</param>
/// <param name="Table">The table, view, CTE or derived table that owns a column, or <see langword="null"/>.</param>
/// <param name="DefinitionInScript">The declaration inside the script (alias, CTE name, derived column), or <see langword="null"/> for a real table or column.</param>
/// <param name="DefinitionInSchema">Whether the table, view or column exists in the schema snapshot.</param>
public sealed record SqlSymbol(
    SqlSymbolKind Kind,
    string Name,
    string? Schema,
    string? Table,
    TextRange? DefinitionInScript,
    bool DefinitionInSchema);
