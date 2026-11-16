namespace Squill.Presentation.Results;

/// <summary>One condition of the grid filter bar: a column, an operator and its operand.</summary>
/// <param name="Column">The ordinal of the filtered column.</param>
/// <param name="Operator">The comparison.</param>
/// <param name="Text">The typed operand of a single-value operator, <see langword="null"/> for <see cref="GridFilterOperator.IsNull"/>, <see cref="GridFilterOperator.IsNotNull"/> and <see cref="GridFilterOperator.In"/>.</param>
/// <param name="Values">The set of <see cref="GridFilterOperator.In"/> (it may hold <see langword="null"/> for the NULL row), otherwise <see langword="null"/>.</param>
public sealed record GridFilter(int Column, GridFilterOperator Operator, string? Text, IReadOnlyList<object?>? Values);
