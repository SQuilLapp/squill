using Squill.Core;
using Squill.Sql.Highlighting;
using Xunit;

namespace Squill.Sql.Tests;

public sealed class HighlighterTests
{
    private static string[] Kinds(string sql, DatabaseDialect d) =>
        [.. SqlHighlighter.Classify(sql, d).Select(s => $"{s.Kind}:{sql.Substring(s.Start, s.Length)}")];

    [Fact]
    public void Classifies_basic_statement() =>
        Assert.Equal(
            ["Keyword:SELECT", "Function:now", "Keyword:FROM", "Identifier:t", "Keyword:WHERE", "Identifier:a", "Operator:=", "Number:1", "Comment:-- c"],
            Kinds("SELECT now() FROM t WHERE a = 1 -- c", DatabaseDialect.PostgreSql));

    [Fact]
    public void Function_needs_call_parenthesis_and_catalog_entry()
    {
        Assert.Equal(["Function:now"], Kinds("now()", DatabaseDialect.PostgreSql));
        Assert.Equal(["Identifier:now"], Kinds("now", DatabaseDialect.PostgreSql));
        Assert.Equal(["Identifier:foo"], Kinds("foo()", DatabaseDialect.PostgreSql));
    }

    [Theory]
    [InlineData(DatabaseDialect.PostgreSql, "$1", "Parameter:$1")]
    [InlineData(DatabaseDialect.MySql, "`a`", "QuotedIdentifier:`a`")]
    [InlineData(DatabaseDialect.SqlServer, "[a b]", "QuotedIdentifier:[a b]")]
    [InlineData(DatabaseDialect.SqlServer, "@p", "Parameter:@p")]
    [InlineData(DatabaseDialect.Sqlite, ":p", "Parameter::p")]
    [InlineData(DatabaseDialect.PostgreSql, "'x''y'", "StringLiteral:'x''y'")]
    [InlineData(DatabaseDialect.MySql, "# note", "Comment:# note")]
    public void Dialect_forms(DatabaseDialect d, string sql, string expected) => Assert.Equal([expected], Kinds(sql, d));

    [Fact]
    public void Whitespace_and_punctuation_are_omitted() =>
        Assert.Empty(SqlHighlighter.Classify(" ( , ; ) ", DatabaseDialect.Sqlite));
}
