using Squill.Core;
using Squill.Sql.Analysis;
using Squill.Sql.Completion;
using Xunit;

namespace Squill.Sql.Tests;

public class AliasGeneratorTests
{
    private static string Gen(string name, DatabaseDialect d = DatabaseDialect.PostgreSql, params string[] taken)
        => AliasGenerator.Generate(name, taken, d);

    [Theory]
    [InlineData("customers", "c")]
    [InlineData("order_items", "oi")]
    [InlineData("OrderItems", "oi")]
    [InlineData("order-items", "oi")]
    [InlineData("order items", "oi")]
    [InlineData("HTTPLog", "hl")]
    [InlineData("orderLines", "ol")]
    [InlineData("1stTable", "t")]
    [InlineData("_x", "t")]
    public void Initials(string name, string expected) => Assert.Equal(expected, Gen(name));

    [Fact]
    public void ReservedWordsAreNeverProduced()
    {
        Assert.Equal("ord", Gen("orders", DatabaseDialect.PostgreSql, "o"));
        Assert.Equal("one", Gen("ones", DatabaseDialect.PostgreSql, "o"));
        Assert.Equal("ass", Gen("assets", DatabaseDialect.PostgreSql, "a"));
        Assert.Equal("byt", Gen("bytes", DatabaseDialect.PostgreSql, "b"));
        Assert.Equal("iss", Gen("issues", DatabaseDialect.PostgreSql, "i"));
        Assert.Equal("inv", Gen("invoices", DatabaseDialect.PostgreSql, "i"));
    }

    [Fact]
    public void FirstTwoLettersWhenTaken() => Assert.Equal("us", Gen("users", DatabaseDialect.PostgreSql, "u"));

    [Fact]
    public void FirstTwoLettersPlusOtherInitials() => Assert.Equal("ori", Gen("order_items", DatabaseDialect.PostgreSql, "oi"));

    [Fact]
    public void FirstThreeLettersPlusOtherInitials() => Assert.Equal("ordi", Gen("order_items", DatabaseDialect.PostgreSql, "oi", "ori"));

    [Fact]
    public void CollisionChainEndsInDigits()
        => Assert.Equal("oi2", Gen("order_items", DatabaseDialect.PostgreSql, "oi", "ori", "ordi"));

    [Fact]
    public void DigitsContinueWhenTwoIsTaken()
        => Assert.Equal("oi3", Gen("order_items", DatabaseDialect.PostgreSql, "oi", "ori", "ordi", "oi2"));

    [Fact]
    public void TakenIsCaseInsensitive() => Assert.Equal("cu", Gen("customers", DatabaseDialect.PostgreSql, "C"));

    [Fact]
    public void ReservedWordsDifferPerDialect()
    {
        // "do" is reserved in PostgreSQL only.
        Assert.Equal("dao", Gen("data_objects", DatabaseDialect.PostgreSql));
        Assert.Equal("do", Gen("data_objects", DatabaseDialect.Sqlite));
    }

    private static VisibleSource S(string? schema, string name, string? alias = null) => new(schema, name, alias, null);

    [Fact]
    public void AliasNeededBecauseASourceHasAlias()
        => Assert.True(AliasGenerator.IsAliasNeeded([S(null, "customers", "c")], null, "orders", false));

    [Fact]
    public void AliasNotNeededWithoutReason()
        => Assert.False(AliasGenerator.IsAliasNeeded([S(null, "customers")], null, "orders", false));

    [Fact]
    public void AliasNeededWhenTableAlreadyPresent()
        => Assert.True(AliasGenerator.IsAliasNeeded([S(null, "orders")], null, "orders", false));

    [Fact]
    public void AliasNeededForSelfReference()
        => Assert.True(AliasGenerator.IsAliasNeeded([S(null, "customers")], null, "orders", true));

    [Fact]
    public void SchemaComparedOnlyWhenBothGiven()
    {
        Assert.False(AliasGenerator.IsAliasNeeded([S("a", "orders")], "b", "orders", false));
        Assert.True(AliasGenerator.IsAliasNeeded([S("a", "orders")], "a", "orders", false));
        Assert.True(AliasGenerator.IsAliasNeeded([S(null, "orders")], "b", "orders", false));
        Assert.True(AliasGenerator.IsAliasNeeded([S("a", "orders")], null, "orders", false));
    }
}
