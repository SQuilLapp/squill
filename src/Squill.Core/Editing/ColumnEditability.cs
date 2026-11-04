namespace Squill.Core.Editing;

/// <summary>Why a column of a table cannot be edited in the grid.</summary>
public enum ReadOnlyReason
{
    /// <summary>The column can be edited.</summary>
    None,

    /// <summary>The result column is not a plain column of the target table.</summary>
    NotTableColumn,

    /// <summary>The engine computes the value (generated, computed, rowversion, identity ALWAYS).</summary>
    Generated,

    /// <summary>The column type has no safe text form to write back.</summary>
    UnsupportedType,
}

/// <summary>Decides from the declared column type whether a value can be written back.</summary>
public static class ColumnEditability
{
    private static readonly string[] UnsupportedTypes = ["sql_variant", "hierarchyid", "geography", "geometry"];

    /// <summary>Gets the reason a mapped table column is read-only.</summary>
    /// <param name="column">The table column.</param>
    /// <returns><see cref="ReadOnlyReason.None"/> when the column can be edited.</returns>
    public static ReadOnlyReason Of(ColumnDefinition column)
    {
        ArgumentNullException.ThrowIfNull(column);
        if (column.IsGenerated)
            return ReadOnlyReason.Generated;
        var type = column.DataType.Trim();
        if (UnsupportedTypes.Contains(type, StringComparer.OrdinalIgnoreCase)
            || type.EndsWith("range", StringComparison.OrdinalIgnoreCase)
            || type.EndsWith("multirange", StringComparison.OrdinalIgnoreCase)
            || (type.EndsWith("[]", StringComparison.Ordinal) && !type.Equals("integer[]", StringComparison.OrdinalIgnoreCase) && !type.Equals("text[]", StringComparison.OrdinalIgnoreCase)))
            return ReadOnlyReason.UnsupportedType;
        return ReadOnlyReason.None;
    }
}
