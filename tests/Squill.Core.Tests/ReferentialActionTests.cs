using Xunit;

namespace Squill.Core.Tests;

public sealed class ReferentialActionTests
{
    [Theory]
    [InlineData(ReferentialAction.NoAction, "NO ACTION")]
    [InlineData(ReferentialAction.Restrict, "RESTRICT")]
    [InlineData(ReferentialAction.Cascade, "CASCADE")]
    [InlineData(ReferentialAction.SetNull, "SET NULL")]
    [InlineData(ReferentialAction.SetDefault, "SET DEFAULT")]
    public void ToSql_And_Parse_RoundTrip(ReferentialAction action, string sql)
    {
        Assert.Equal(sql, action.ToSql());
        Assert.Equal(action, ReferentialActions.Parse(sql));
    }

    [Theory]
    [InlineData("no   action", ReferentialAction.NoAction)]
    [InlineData("  Set\tnull ", ReferentialAction.SetNull)]
    [InlineData("cascade", ReferentialAction.Cascade)]
    public void Parse_NormalizesCaseAndWhitespace(string sql, ReferentialAction expected) =>
        Assert.Equal(expected, ReferentialActions.Parse(sql));

    [Theory]
    [InlineData("bogus")]
    [InlineData("")]
    [InlineData(null)]
    public void Parse_UnknownOrNull_ReturnsNull(string? sql) => Assert.Null(ReferentialActions.Parse(sql));

    [Fact]
    public void NewModelParameters_HaveDefaults()
    {
        var column = new ColumnDefinition("id", "int", false, true, null);
        var fk = new ForeignKeyInfo("fk", ["a"], null, "t", ["b"]);
        var table = new TableInfo(null, "t", TableKind.Table, [column], [], [fk]);

        Assert.False(column.IsAutoIncrement);
        Assert.Equal(ReferentialAction.NoAction, fk.OnDelete);
        Assert.Equal(ReferentialAction.NoAction, fk.OnUpdate);
        Assert.Null(table.PrimaryKeyName);
    }
}
