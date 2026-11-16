namespace Squill.Presentation.Results;

/// <summary>The comparison a grid filter applies to the cells of one column.</summary>
public enum GridFilterOperator
{
    /// <summary>The cell equals the operand.</summary>
    Equals,

    /// <summary>The cell is not NULL and differs from the operand.</summary>
    NotEquals,

    /// <summary>The cell text contains the operand.</summary>
    Contains,

    /// <summary>The cell text starts with the operand.</summary>
    StartsWith,

    /// <summary>The cell text ends with the operand.</summary>
    EndsWith,

    /// <summary>The cell is greater than the operand.</summary>
    GreaterThan,

    /// <summary>The cell is greater than or equal to the operand.</summary>
    GreaterOrEqual,

    /// <summary>The cell is less than the operand.</summary>
    LessThan,

    /// <summary>The cell is less than or equal to the operand.</summary>
    LessOrEqual,

    /// <summary>The cell is SQL NULL.</summary>
    IsNull,

    /// <summary>The cell is not SQL NULL.</summary>
    IsNotNull,

    /// <summary>The cell is one of a set of values (the set may hold NULL).</summary>
    In,
}
