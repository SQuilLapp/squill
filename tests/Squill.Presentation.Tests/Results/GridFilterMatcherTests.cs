using Squill.Core;
using Squill.Presentation.Results;
using Xunit;

namespace Squill.Presentation.Tests.Results;

public sealed class GridFilterMatcherTests
{
    private static ColumnInfo Col(Type t, string dataType = "x") => new("c", dataType, t, true, 0);

    private static GridFilter Make(ColumnInfo column, GridFilterOperator op, string? text)
    {
        Assert.True(GridFilterMatcher.TryCreate(column, op, text, out var filter, out var error), error);
        return filter;
    }

    private static bool Match(ColumnInfo column, GridFilterOperator op, string? text, object? cell) =>
        GridFilterMatcher.Matches(Make(column, op, text), column, cell);

    [Fact]
    public void Operators_TextColumn_OffersNoComparison()
    {
        var ops = GridFilterMatcher.Operators(Col(typeof(string)));
        Assert.Contains(GridFilterOperator.Contains, ops);
        Assert.Contains(GridFilterOperator.In, ops);
        Assert.DoesNotContain(GridFilterOperator.GreaterThan, ops);
        Assert.DoesNotContain(GridFilterOperator.LessOrEqual, ops);
    }

    [Theory]
    [InlineData(typeof(int))]
    [InlineData(typeof(decimal))]
    [InlineData(typeof(DateTime))]
    public void Operators_NumberAndMoment_OfferComparisonsButNoTextSearch(Type type)
    {
        var ops = GridFilterMatcher.Operators(Col(type));
        Assert.Contains(GridFilterOperator.GreaterOrEqual, ops);
        Assert.Contains(GridFilterOperator.In, ops);
        Assert.DoesNotContain(GridFilterOperator.Contains, ops);
    }

    [Fact]
    public void Operators_BooleanColumn_IsEqualityAndNull() =>
        Assert.Equal(
            [GridFilterOperator.Equals, GridFilterOperator.NotEquals, GridFilterOperator.IsNull, GridFilterOperator.IsNotNull],
            GridFilterMatcher.Operators(Col(typeof(bool))));

    [Fact]
    public void Operators_ByteArrayColumn_IsNullAndContains() =>
        Assert.Equal(
            [GridFilterOperator.IsNull, GridFilterOperator.IsNotNull, GridFilterOperator.Contains],
            GridFilterMatcher.Operators(Col(typeof(byte[]))));

    [Theory]
    [InlineData(GridFilterOperator.Equals, "lyon", "Lyon", true)]
    [InlineData(GridFilterOperator.Equals, "lyon", "Lyons", false)]
    [InlineData(GridFilterOperator.NotEquals, "lyon", "Paris", true)]
    [InlineData(GridFilterOperator.NotEquals, "lyon", "LYON", false)]
    [InlineData(GridFilterOperator.Contains, "YO", "Lyon", true)]
    [InlineData(GridFilterOperator.Contains, "xx", "Lyon", false)]
    [InlineData(GridFilterOperator.StartsWith, "ly", "Lyon", true)]
    [InlineData(GridFilterOperator.StartsWith, "on", "Lyon", false)]
    [InlineData(GridFilterOperator.EndsWith, "ON", "Lyon", true)]
    [InlineData(GridFilterOperator.EndsWith, "ly", "Lyon", false)]
    public void Matches_TextColumn_IsCaseInsensitive(GridFilterOperator op, string operand, string cell, bool expected) =>
        Assert.Equal(expected, Match(Col(typeof(string)), op, operand, cell));

    [Fact]
    public void Matches_AccentedText_KeepsTheAccentSignificant()
    {
        var c = Col(typeof(string));
        Assert.True(Match(c, GridFilterOperator.Contains, "ÉTÉ", "un été chaud"));
        Assert.False(Match(c, GridFilterOperator.Contains, "ete", "un été chaud"));
    }

    [Theory]
    [InlineData(GridFilterOperator.Equals, "5", 5, true)]
    [InlineData(GridFilterOperator.Equals, "5", 6, false)]
    [InlineData(GridFilterOperator.NotEquals, "5", 6, true)]
    [InlineData(GridFilterOperator.GreaterThan, "5", 6, true)]
    [InlineData(GridFilterOperator.GreaterThan, "5", 5, false)]
    [InlineData(GridFilterOperator.GreaterOrEqual, "5", 5, true)]
    [InlineData(GridFilterOperator.LessThan, "5", 4, true)]
    [InlineData(GridFilterOperator.LessThan, "5", 5, false)]
    [InlineData(GridFilterOperator.LessOrEqual, "5", 5, true)]
    [InlineData(GridFilterOperator.LessOrEqual, "5", 6, false)]
    public void Matches_IntegerColumn_ComparesByValue(GridFilterOperator op, string operand, int cell, bool expected) =>
        Assert.Equal(expected, Match(Col(typeof(int)), op, operand, cell));

    [Fact]
    public void Matches_IntegerColumn_ComparesNumbersNotText() =>
        Assert.True(Match(Col(typeof(int)), GridFilterOperator.GreaterThan, "9", 10));

    [Theory]
    [InlineData(GridFilterOperator.GreaterThan, "100.5", "100.51", true)]
    [InlineData(GridFilterOperator.GreaterThan, "100.5", "100.50", false)]
    [InlineData(GridFilterOperator.Equals, "100.5", "100.50", true)]
    [InlineData(GridFilterOperator.LessOrEqual, "100.5", "99", true)]
    public void Matches_DecimalColumn_ComparesByValue(GridFilterOperator op, string operand, string cell, bool expected) =>
        Assert.Equal(expected, Match(Col(typeof(decimal)), op, operand, decimal.Parse(cell, System.Globalization.CultureInfo.InvariantCulture)));

    [Fact]
    public void Matches_IntegerColumnWithLongCell_ComparesAcrossNumericTypes() =>
        Assert.True(Match(Col(typeof(int)), GridFilterOperator.Equals, "5", 5L));

    [Theory]
    [InlineData(GridFilterOperator.GreaterThan, "2024-05-06", "2024-05-07 00:00:00", true)]
    [InlineData(GridFilterOperator.GreaterThan, "2024-05-06", "2024-05-06 00:00:00", false)]
    [InlineData(GridFilterOperator.LessThan, "2024-05-06 12:00:00", "2024-05-06 11:59:59", true)]
    [InlineData(GridFilterOperator.Equals, "2024-05-06 08:09:10", "2024-05-06 08:09:10", true)]
    public void Matches_DateTimeColumn_ComparesChronologically(GridFilterOperator op, string operand, string cell, bool expected) =>
        Assert.Equal(expected, Match(Col(typeof(DateTime)), op, operand, DateTime.Parse(cell, System.Globalization.CultureInfo.InvariantCulture)));

    [Theory]
    [InlineData(GridFilterOperator.Equals, "true", true, true)]
    [InlineData(GridFilterOperator.Equals, "true", false, false)]
    [InlineData(GridFilterOperator.NotEquals, "true", false, true)]
    [InlineData(GridFilterOperator.NotEquals, "true", true, false)]
    public void Matches_BooleanColumn_ComparesTheValue(GridFilterOperator op, string operand, bool cell, bool expected) =>
        Assert.Equal(expected, Match(Col(typeof(bool)), op, operand, cell));

    [Fact]
    public void Matches_ByteArrayColumn_ContainsSearchesTheHexText()
    {
        var c = Col(typeof(byte[]));
        Assert.True(Match(c, GridFilterOperator.Contains, "ab", new byte[] { 0x01, 0xAB }));
        Assert.False(Match(c, GridFilterOperator.Contains, "cd", new byte[] { 0x01, 0xAB }));
        Assert.True(Match(c, GridFilterOperator.IsNotNull, null, new byte[] { 1 }));
    }

    [Theory]
    [InlineData(typeof(string), GridFilterOperator.Equals, "a")]
    [InlineData(typeof(string), GridFilterOperator.NotEquals, "a")]
    [InlineData(typeof(string), GridFilterOperator.Contains, "a")]
    [InlineData(typeof(int), GridFilterOperator.NotEquals, "1")]
    [InlineData(typeof(int), GridFilterOperator.LessThan, "1")]
    [InlineData(typeof(bool), GridFilterOperator.NotEquals, "true")]
    public void Matches_NullCell_OnlyIsNullMatches(Type type, GridFilterOperator op, string operand) =>
        Assert.False(Match(Col(type), op, operand, null));

    [Fact]
    public void Matches_IsNullAndIsNotNull_SplitNullFromValues()
    {
        var c = Col(typeof(int));
        Assert.True(Match(c, GridFilterOperator.IsNull, null, null));
        Assert.False(Match(c, GridFilterOperator.IsNull, null, 1));
        Assert.True(Match(c, GridFilterOperator.IsNotNull, null, 1));
        Assert.False(Match(c, GridFilterOperator.IsNotNull, null, null));
    }

    [Fact]
    public void Matches_InSet_TextIsCaseInsensitive()
    {
        var c = Col(typeof(string));
        var f = GridFilterMatcher.CreateIn(c, ["Lyon", "Paris"]);
        Assert.True(GridFilterMatcher.Matches(f, c, "paris"));
        Assert.False(GridFilterMatcher.Matches(f, c, "Nice"));
        Assert.False(GridFilterMatcher.Matches(f, c, null));
    }

    [Fact]
    public void Matches_InSetWithNullEntry_MatchesNull()
    {
        var c = Col(typeof(int));
        var f = GridFilterMatcher.CreateIn(c, [1, null]);
        Assert.True(GridFilterMatcher.Matches(f, c, null));
        Assert.True(GridFilterMatcher.Matches(f, c, 1L));
        Assert.False(GridFilterMatcher.Matches(f, c, 2));
    }

    [Fact]
    public void Matches_EmptyInSet_MatchesNothing()
    {
        var c = Col(typeof(int));
        var f = GridFilterMatcher.CreateIn(c, []);
        Assert.False(GridFilterMatcher.Matches(f, c, 1));
        Assert.False(GridFilterMatcher.Matches(f, c, null));
    }

    [Fact]
    public void TryCreate_InvalidTypedOperand_ReturnsErrorAndNoOperand()
    {
        Assert.False(GridFilterMatcher.TryCreate(Col(typeof(int)), GridFilterOperator.Equals, "abc", out var filter, out var error));
        Assert.False(string.IsNullOrEmpty(error));
        Assert.Null(filter.Text);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void TryCreate_MissingOperand_IsAnError(string? text)
    {
        Assert.False(GridFilterMatcher.TryCreate(Col(typeof(string)), GridFilterOperator.Contains, text, out _, out var error));
        Assert.False(string.IsNullOrEmpty(error));
    }

    [Fact]
    public void TryCreate_IsNull_NeedsNoOperand()
    {
        Assert.True(GridFilterMatcher.TryCreate(Col(typeof(int)), GridFilterOperator.IsNull, null, out var filter, out var error));
        Assert.Null(error);
        Assert.Null(filter.Text);
    }

    [Fact]
    public void TryCreate_TypedOperand_IsNormalized()
    {
        Assert.True(GridFilterMatcher.TryCreate(Col(typeof(int)), GridFilterOperator.Equals, " 007 ", out var filter, out _));
        Assert.Equal("7", filter.Text);
    }

    [Fact]
    public void TryCreate_OperatorNotOfferedForTheColumn_Throws() =>
        Assert.Throws<ArgumentException>(() => GridFilterMatcher.TryCreate(Col(typeof(string)), GridFilterOperator.GreaterThan, "a", out _, out _));

    [Fact]
    public void Compile_ManyRows_MatchesLikeTheFilter()
    {
        var c = Col(typeof(int));
        var compiled = GridFilterMatcher.Compile(Make(c, GridFilterOperator.GreaterOrEqual, "50"), c);
        var count = 0;
        for (var i = 0; i < 100; i++)
            count += compiled.Matches(i) ? 1 : 0;
        Assert.Equal(50, count);
    }
}
