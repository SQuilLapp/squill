using System.Globalization;
using Squill.Core;
using Squill.Presentation.Editing;
using Squill.Presentation.Localization;

namespace Squill.Presentation.Results;

/// <summary>Knows which operators a column offers, validates a typed operand and decides whether a cell matches a <see cref="GridFilter"/>.</summary>
public static class GridFilterMatcher
{
    private static readonly GridFilterOperator[] TextOperators =
    [
        GridFilterOperator.Equals, GridFilterOperator.NotEquals, GridFilterOperator.Contains, GridFilterOperator.StartsWith,
        GridFilterOperator.EndsWith, GridFilterOperator.IsNull, GridFilterOperator.IsNotNull, GridFilterOperator.In,
    ];

    private static readonly GridFilterOperator[] OrderedOperators =
    [
        GridFilterOperator.Equals, GridFilterOperator.NotEquals, GridFilterOperator.GreaterThan, GridFilterOperator.GreaterOrEqual,
        GridFilterOperator.LessThan, GridFilterOperator.LessOrEqual, GridFilterOperator.IsNull, GridFilterOperator.IsNotNull, GridFilterOperator.In,
    ];

    private static readonly GridFilterOperator[] BooleanOperators =
        [GridFilterOperator.Equals, GridFilterOperator.NotEquals, GridFilterOperator.IsNull, GridFilterOperator.IsNotNull];

    private static readonly GridFilterOperator[] OtherOperators =
        [GridFilterOperator.IsNull, GridFilterOperator.IsNotNull, GridFilterOperator.Contains];

    /// <summary>The type category of a column, which decides its operators and how cells are compared.</summary>
    internal enum Category
    {
        /// <summary>Strings and guids, compared as text.</summary>
        Text,

        /// <summary>Integers and fractional numbers, compared by value.</summary>
        Number,

        /// <summary>Dates, times and moments, compared chronologically.</summary>
        Moment,

        /// <summary>True or false.</summary>
        Boolean,

        /// <summary>Anything else (binary, arrays, documents), searched as text only.</summary>
        Other,
    }

    /// <summary>Lists the operators the filter editor offers for a column.</summary>
    /// <param name="column">The column to filter.</param>
    /// <returns>The operators of the column's type category, in display order.</returns>
    public static IReadOnlyList<GridFilterOperator> Operators(ColumnInfo column) => CategoryOf(column) switch
    {
        Category.Text => TextOperators,
        Category.Number or Category.Moment => OrderedOperators,
        Category.Boolean => BooleanOperators,
        _ => OtherOperators,
    };

    /// <summary>Validates a typed operand and builds the filter.</summary>
    /// <param name="column">The filtered column.</param>
    /// <param name="op">The comparison; <see cref="GridFilterOperator.In"/> is built with <see cref="CreateIn"/>.</param>
    /// <param name="text">The typed operand; ignored for <see cref="GridFilterOperator.IsNull"/> and <see cref="GridFilterOperator.IsNotNull"/>.</param>
    /// <param name="filter">The filter, with the operand normalized to the invariant cell text; a placeholder when the result is <see langword="false"/>.</param>
    /// <param name="error">The user-facing message when the operand is missing or invalid, otherwise <see langword="null"/>.</param>
    /// <returns><see langword="true"/> when the filter is valid.</returns>
    /// <exception cref="ArgumentException">The column does not offer <paramref name="op"/>, or it is <see cref="GridFilterOperator.In"/>.</exception>
    public static bool TryCreate(ColumnInfo column, GridFilterOperator op, string? text, out GridFilter filter, out string? error)
    {
        ArgumentNullException.ThrowIfNull(column);
        if (op == GridFilterOperator.In || !Operators(column).Contains(op))
            throw new ArgumentException($"Operator {op} cannot be created from text for column {column.Name}.", nameof(op));
        error = null;
        filter = new GridFilter(column.Ordinal, op, null, null);
        if (op is GridFilterOperator.IsNull or GridFilterOperator.IsNotNull)
            return true;
        if (string.IsNullOrEmpty(text))
        {
            error = UiText.Get("editor.valueRequired");
            return false;
        }

        // The text operators (and every operator of a text or other column) compare formatted text; only the comparisons are typed.
        if (CategoryOf(column) is Category.Number or Category.Moment or Category.Boolean
            && op is not (GridFilterOperator.Contains or GridFilterOperator.StartsWith or GridFilterOperator.EndsWith))
        {
            if (!CellValueParser.TryParse(text, column, out var value, out error))
                return false;
            if (value is null)
            {
                error = UiText.Get("editor.valueRequired");
                return false;
            }

            text = CellFormatter.Format(value, column);
        }

        filter = filter with { Text = text };
        return true;
    }

    /// <summary>Builds the filter of a set of values, as the value list of the column menu produces it.</summary>
    /// <param name="column">The filtered column.</param>
    /// <param name="values">The accepted cell values; <see langword="null"/> stands for the NULL row.</param>
    /// <returns>An <see cref="GridFilterOperator.In"/> filter.</returns>
    public static GridFilter CreateIn(ColumnInfo column, IReadOnlyList<object?> values)
    {
        ArgumentNullException.ThrowIfNull(column);
        ArgumentNullException.ThrowIfNull(values);
        return new GridFilter(column.Ordinal, GridFilterOperator.In, null, values);
    }

    /// <summary>Tells whether a cell passes a filter.</summary>
    /// <param name="filter">The filter.</param>
    /// <param name="column">The filtered column.</param>
    /// <param name="cell">The cell value, <see langword="null"/> for SQL NULL.</param>
    /// <returns><see langword="true"/> when the cell matches; NULL matches only <see cref="GridFilterOperator.IsNull"/> and an <see cref="GridFilterOperator.In"/> set holding NULL.</returns>
    public static bool Matches(GridFilter filter, ColumnInfo column, object? cell) => Compile(filter, column).Matches(cell);

    internal static CompiledGridFilter Compile(GridFilter filter, ColumnInfo column)
    {
        ArgumentNullException.ThrowIfNull(filter);
        ArgumentNullException.ThrowIfNull(column);
        return new CompiledGridFilter(filter, column, CategoryOf(column));
    }

    internal static Category CategoryOf(ColumnInfo column) => CellEditorKinds.For(column) switch
    {
        CellEditorKind.Whole or CellEditorKind.Fractional => Category.Number,
        CellEditorKind.Date or CellEditorKind.Time or CellEditorKind.DateTime => Category.Moment,
        CellEditorKind.Boolean => Category.Boolean,
        _ => column.ClrType == typeof(string) || column.ClrType == typeof(Guid) ? Category.Text : Category.Other,
    };

    /// <summary>A filter with its operand parsed once, so a view can test 100,000 rows without parsing or allocating per row.</summary>
    internal sealed class CompiledGridFilter
    {
        private readonly GridFilterOperator _op;
        private readonly ColumnInfo _column;
        private readonly Category _category;
        private readonly string? _text;
        private readonly object? _operand;
        private readonly IReadOnlyList<object?>? _values;

        internal CompiledGridFilter(GridFilter filter, ColumnInfo column, Category category)
        {
            _op = filter.Operator;
            _column = column;
            _category = category;
            _text = filter.Text;
            _values = filter.Values;
            if (category is Category.Number or Category.Moment or Category.Boolean && filter.Text is not null && IsTyped(_op))
                CellValueParser.TryParse(filter.Text, column, out _operand, out _);
        }

        /// <summary>Tests one cell.</summary>
        /// <param name="cell">The cell value, <see langword="null"/> for SQL NULL.</param>
        /// <returns><see langword="true"/> when the cell matches.</returns>
        internal bool Matches(object? cell)
        {
            if (_op == GridFilterOperator.IsNull)
                return cell is null;
            if (_op == GridFilterOperator.IsNotNull)
                return cell is not null;
            if (_op == GridFilterOperator.In)
                return Any(cell);
            if (cell is null || _text is null)
                return false;
            if (!IsTyped(_op) || _category is Category.Text or Category.Other)
                return TextMatch(CellFormatter.Format(cell, _column) ?? "");
            if (_operand is null || !TryCompare(cell, _operand, out var order))
                return false;
            return _op switch
            {
                GridFilterOperator.Equals => order == 0,
                GridFilterOperator.NotEquals => order != 0,
                GridFilterOperator.GreaterThan => order > 0,
                GridFilterOperator.GreaterOrEqual => order >= 0,
                GridFilterOperator.LessThan => order < 0,
                _ => order <= 0,
            };
        }

        private static bool IsTyped(GridFilterOperator op) =>
            op is not (GridFilterOperator.Contains or GridFilterOperator.StartsWith or GridFilterOperator.EndsWith);

        private bool TextMatch(string cellText) => _op switch
        {
            GridFilterOperator.Equals => cellText.Equals(_text, StringComparison.OrdinalIgnoreCase),
            GridFilterOperator.NotEquals => !cellText.Equals(_text, StringComparison.OrdinalIgnoreCase),
            GridFilterOperator.Contains => cellText.Contains(_text!, StringComparison.OrdinalIgnoreCase),
            GridFilterOperator.StartsWith => cellText.StartsWith(_text!, StringComparison.OrdinalIgnoreCase),
            _ => cellText.EndsWith(_text!, StringComparison.OrdinalIgnoreCase),
        };

        private bool Any(object? cell)
        {
            if (_values is null)
                return false;
            foreach (var value in _values)
            {
                if (cell is null || value is null)
                {
                    if (cell is null && value is null)
                        return true;
                    continue;
                }

                if (_category is Category.Number or Category.Moment or Category.Boolean && TryCompare(cell, value, out var order))
                {
                    if (order == 0)
                        return true;
                }
                else if ((CellFormatter.Format(cell, _column) ?? "").Equals(CellFormatter.Format(value, _column), StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        internal static bool TryCompare(object a, object b, out int order)
        {
            order = 0;
            if (a.GetType() == b.GetType())
            {
                if (a is not IComparable comparable)
                    return false;
                order = comparable.CompareTo(b);
                return true;
            }

            if (IsNumber(a) && IsNumber(b))
            {
                order = a is double or float || b is double or float
                    ? Convert.ToDouble(a, CultureInfo.InvariantCulture).CompareTo(Convert.ToDouble(b, CultureInfo.InvariantCulture))
                    : Convert.ToDecimal(a, CultureInfo.InvariantCulture).CompareTo(Convert.ToDecimal(b, CultureInfo.InvariantCulture));
                return true;
            }

            if (a is DateTime or DateTimeOffset && b is DateTime or DateTimeOffset)
            {
                order = ToOffset(a).CompareTo(ToOffset(b));
                return true;
            }

            return false;
        }

        private static bool IsNumber(object o) => o is byte or short or int or long or sbyte or ushort or uint or ulong or float or double or decimal;

        private static DateTimeOffset ToOffset(object o) => o is DateTimeOffset d ? d : new DateTimeOffset(DateTime.SpecifyKind((DateTime)o, DateTimeKind.Utc));
    }
}
