using Squill.Core;
using Squill.Presentation.Results;
using Xunit;

namespace Squill.Presentation.Tests.Results;

public sealed class GridFilterTextTests
{
    private static ColumnInfo Col(string name, Type t) => new(name, "x", t, true, 0);

    private static string Chip(ColumnInfo column, GridFilterOperator op, string? text)
    {
        Assert.True(GridFilterMatcher.TryCreate(column, op, text, out var filter, out var error), error);
        return GridFilterText.Format(filter, column);
    }

    [Fact]
    public void Format_Number_IsBare() =>
        Assert.Equal("montant > 100", Chip(Col("montant", typeof(decimal)), GridFilterOperator.GreaterThan, "100"));

    [Fact]
    public void Format_Boolean_IsBare() =>
        Assert.Equal("actif = true", Chip(Col("actif", typeof(bool)), GridFilterOperator.Equals, "TRUE"));

    [Fact]
    public void Format_String_IsQuotedWithDoubledQuote() =>
        Assert.Equal("nom = 'l''ours'", Chip(Col("nom", typeof(string)), GridFilterOperator.Equals, "l'ours"));

    [Fact]
    public void Format_Date_IsQuotedInTheInvariantFormat() =>
        Assert.Equal("d >= '2024-05-06'", Chip(Col("d", typeof(DateOnly)), GridFilterOperator.GreaterOrEqual, "2024-05-06"));

    [Fact]
    public void Format_TextOperatorOnNumber_QuotesTheOperand() =>
        Assert.Equal("n CONTAINS '12'", Chip(Col("n", typeof(string)), GridFilterOperator.Contains, "12"));

    [Fact]
    public void Format_NullOperators_HaveNoOperand()
    {
        var c = Col("siret", typeof(string));
        Assert.Equal("siret IS NULL", Chip(c, GridFilterOperator.IsNull, null));
        Assert.Equal("siret IS NOT NULL", Chip(c, GridFilterOperator.IsNotNull, null));
    }

    [Theory]
    [InlineData(1, "ville IN ('Lyon')")]
    [InlineData(3, "ville IN ('Lyon', 'Paris', 'Nice')")]
    [InlineData(5, "ville IN ('Lyon', 'Paris', 'Nice', +2)")]
    public void Format_InSet_ListsThreeValuesThenTheRest(int count, string expected)
    {
        var c = Col("ville", typeof(string));
        var values = new object?[] { "Lyon", "Paris", "Nice", "Lille", "Metz" }.Take(count).ToArray();
        Assert.Equal(expected, GridFilterText.Format(GridFilterMatcher.CreateIn(c, values), c));
    }

    [Fact]
    public void Format_InSetWithNull_ShowsNullBare()
    {
        var c = Col("n", typeof(int));
        Assert.Equal("n IN (1, NULL)", GridFilterText.Format(GridFilterMatcher.CreateIn(c, [1, null]), c));
    }
}
