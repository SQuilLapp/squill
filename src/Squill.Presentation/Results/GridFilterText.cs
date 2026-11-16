using System.Text;
using Squill.Core;

namespace Squill.Presentation.Results;

/// <summary>Builds the text of a filter chip. It is for display only: it is never sent to a server and never parsed back.</summary>
public static class GridFilterText
{
    private const int MaxListed = 3;

    /// <summary>Formats a filter like <c>ville IN ('Lyon', 'Paris', 'Nice', +2)</c>, <c>montant &gt; 100</c> or <c>siret IS NULL</c>.</summary>
    /// <param name="filter">The filter.</param>
    /// <param name="column">The filtered column.</param>
    /// <returns>The chip text; strings and dates are quoted (a quote is doubled), numbers and booleans are bare, a set shows its first three values then <c>+N</c>.</returns>
    public static string Format(GridFilter filter, ColumnInfo column)
    {
        ArgumentNullException.ThrowIfNull(filter);
        ArgumentNullException.ThrowIfNull(column);
        var category = GridFilterMatcher.CategoryOf(column);
        var head = column.Name + " ";
        switch (filter.Operator)
        {
            case GridFilterOperator.IsNull:
                return head + "IS NULL";
            case GridFilterOperator.IsNotNull:
                return head + "IS NOT NULL";
            case GridFilterOperator.In:
                var values = filter.Values ?? [];
                var sb = new StringBuilder(head).Append("IN (");
                for (var i = 0; i < Math.Min(values.Count, MaxListed); i++)
                    sb.Append(i > 0 ? ", " : "").Append(values[i] is null ? "NULL" : Literal(CellFormatter.Format(values[i], column), category));
                if (values.Count > MaxListed)
                    sb.Append(", +").Append(values.Count - MaxListed);
                return sb.Append(')').ToString();
        }

        var symbol = filter.Operator switch
        {
            GridFilterOperator.Equals => "=",
            GridFilterOperator.NotEquals => "<>",
            GridFilterOperator.GreaterThan => ">",
            GridFilterOperator.GreaterOrEqual => ">=",
            GridFilterOperator.LessThan => "<",
            GridFilterOperator.LessOrEqual => "<=",
            GridFilterOperator.Contains => "CONTAINS",
            GridFilterOperator.StartsWith => "STARTS WITH",
            _ => "ENDS WITH",
        };
        // The text operators quote their operand whatever the column type.
        var bare = category is GridFilterMatcher.Category.Number or GridFilterMatcher.Category.Boolean
            && filter.Operator is not (GridFilterOperator.Contains or GridFilterOperator.StartsWith or GridFilterOperator.EndsWith);
        return head + symbol + " " + (bare ? filter.Text ?? "NULL" : Quote(filter.Text));
    }

    private static string Literal(string? text, GridFilterMatcher.Category category) =>
        category is GridFilterMatcher.Category.Number or GridFilterMatcher.Category.Boolean ? text ?? "NULL" : Quote(text);

    private static string Quote(string? text) => "'" + (text ?? "").Replace("'", "''", StringComparison.Ordinal) + "'";
}
