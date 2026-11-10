using Squill.Core;
using Squill.Sql.Analysis;
using Squill.Sql.Completion;
using Xunit;

namespace Squill.Sql.Tests;

public class JoinConditionTextTests
{
    private static TableInfo T(string name) => new(null, name, TableKind.Table, [], [], []);

    private static TableRelation Rel(string existing, string other, string[] existingColumns, string[] otherColumns,
        bool otherReferencesExisting = true, RelationOrigin origin = RelationOrigin.ForeignKey, string? fk = "fk")
        => new(new VisibleSource(null, existing, null, null), T(other), existingColumns, otherColumns, origin, fk, otherReferencesExisting);

    [Fact]
    public void ExistingReferencedByNew()
        => Assert.Equal("c.id = o.customer_id",
            JoinConditionText.Build(Rel("customers", "orders", ["id"], ["customer_id"]), "c", "o", DatabaseDialect.PostgreSql));

    [Fact]
    public void NewReferencedByExisting()
        => Assert.Equal("o.customer_id = c.id",
            JoinConditionText.Build(Rel("orders", "customers", ["customer_id"], ["id"], otherReferencesExisting: false), "o", "c", DatabaseDialect.PostgreSql));

    [Fact]
    public void CompositeKeyKeepsOrder()
        => Assert.Equal("a.x = b.x AND a.y = b.y",
            JoinConditionText.Build(Rel("a", "b", ["x", "y"], ["x", "y"]), "a", "b", DatabaseDialect.PostgreSql));

    [Fact]
    public void TableNamesWhenNoAlias()
        => Assert.Equal("customers.id = orders.customer_id",
            JoinConditionText.Build(Rel("customers", "orders", ["id"], ["customer_id"]), "customers", "orders", DatabaseDialect.Sqlite));

    [Theory]
    [InlineData(DatabaseDialect.MySql, "c.`Order Id` = o.x")]
    [InlineData(DatabaseDialect.SqlServer, "c.[Order Id] = o.x")]
    [InlineData(DatabaseDialect.PostgreSql, "c.\"Order Id\" = o.x")]
    public void QuotesPerDialect(DatabaseDialect dialect, string expected)
        => Assert.Equal(expected, JoinConditionText.Build(Rel("customers", "orders", ["Order Id"], ["x"]), "c", "o", dialect));

    [Fact]
    public void PostgresQuotesCapitals()
        => Assert.Equal("c.\"Id\" = o.customer_id",
            JoinConditionText.Build(Rel("customers", "orders", ["Id"], ["customer_id"]), "c", "o", DatabaseDialect.PostgreSql));

    [Fact]
    public void ClosingQuoteCharacterIsDoubled()
    {
        Assert.Equal("c.[a]]b] = o.x", JoinConditionText.Build(Rel("t", "u", ["a]b"], ["x"]), "c", "o", DatabaseDialect.SqlServer));
        Assert.Equal("c.`a``b` = o.x", JoinConditionText.Build(Rel("t", "u", ["a`b"], ["x"]), "c", "o", DatabaseDialect.MySql));
    }

    [Fact]
    public void DescribeForeignKeyFollowsTheKey()
    {
        Assert.Equal("orders.customer_id -> customers.id (fk_orders_customer)",
            JoinConditionText.Describe(Rel("customers", "orders", ["id"], ["customer_id"], true, fk: "fk_orders_customer")));
        Assert.Equal("orders.customer_id -> customers.id (fk_orders_customer)",
            JoinConditionText.Describe(Rel("orders", "customers", ["customer_id"], ["id"], false, fk: "fk_orders_customer")));
    }

    [Fact]
    public void DescribeSameColumnName()
        => Assert.Equal("same column name: customer_no",
            JoinConditionText.Describe(Rel("a", "b", ["customer_no"], ["customer_no"], true, RelationOrigin.SameColumnName, null)));
}
