using Squill.Core;
using Squill.Sql.Completion;
using Squill.Sql.Functions;
using Xunit;

namespace Squill.Sql.Tests;

public sealed class FunctionTests
{
    private static SqlRequest Req(string marked, DatabaseDialect dialect = DatabaseDialect.PostgreSql)
    {
        var offset = marked.IndexOf('|');
        return new SqlRequest(marked.Remove(offset, 1), offset, dialect, null);
    }

    [Theory]
    [InlineData(DatabaseDialect.PostgreSql, "date_trunc")]
    [InlineData(DatabaseDialect.PostgreSql, "string_agg")]
    [InlineData(DatabaseDialect.MySql, "DATE_FORMAT")]
    [InlineData(DatabaseDialect.MySql, "IFNULL")]
    [InlineData(DatabaseDialect.SqlServer, "GETDATE")]
    [InlineData(DatabaseDialect.SqlServer, "ISNULL")]
    [InlineData(DatabaseDialect.SqlServer, "DATEADD")]
    [InlineData(DatabaseDialect.Sqlite, "strftime")]
    [InlineData(DatabaseDialect.Sqlite, "ifnull")]
    [InlineData(DatabaseDialect.Sqlite, "COUNT")]
    public void Catalog_ContainsCommonAndDialectFunctions(DatabaseDialect dialect, string name) =>
        Assert.Contains(FunctionCatalog.For(dialect), f => f.Name == name);

    [Fact]
    public void Catalog_DialectSpecificFunctionsStayInTheirDialect() =>
        Assert.DoesNotContain(FunctionCatalog.For(DatabaseDialect.Sqlite), f => f.Name == "date_trunc");

    [Fact]
    public void Completion_InWhere_OffersFunctionsInsertingTheBareName()
    {
        var items = new FunctionCompletionProvider().GetCompletions(Req("select a from t where co|"));
        var item = Assert.Single(items, i => i.Label == "COALESCE");
        Assert.Equal(CompletionKind.Function, item.Kind);
        Assert.Equal("COALESCE", item.InsertText);
        Assert.Equal("COALESCE(value, fallback...) -> any", item.Detail);
        Assert.Equal(new TextRange(22, 2), item.ReplaceRange);
    }

    [Theory]
    [InlineData("select | from t")]
    [InlineData("select a from t join u on |")]
    [InlineData("select a from t group by a having |")]
    [InlineData("select a from t order by |")]
    [InlineData("update t set a = |")]
    public void Completion_InExpressionContexts_IsNotEmpty(string marked) =>
        Assert.NotEmpty(new FunctionCompletionProvider().GetCompletions(Req(marked)));

    [Theory]
    [InlineData("|")]
    [InlineData("select a from |")]
    public void Completion_OutsideExpressions_IsEmpty(string marked) =>
        Assert.Empty(new FunctionCompletionProvider().GetCompletions(Req(marked)));

    [Theory]
    [InlineData("select round(|", 0)]
    [InlineData("select ROUND(a, |", 1)]
    [InlineData("select round(a, b|)", 1)]
    public void SignatureHelp_CountsCommas(string marked, int active)
    {
        var help = SignatureHelpProvider.GetSignatureHelp(Req(marked));
        Assert.NotNull(help);
        Assert.Equal("ROUND", help.Signature.Name);
        Assert.Equal(active, help.ActiveParameter);
    }

    [Fact]
    public void SignatureHelp_NestedCall_ReportsInnermost()
    {
        var help = SignatureHelpProvider.GetSignatureHelp(Req("select round(abs(x, |"));
        Assert.Equal("ABS", help!.Signature.Name);
        Assert.Equal(1, help.ActiveParameter);
    }

    [Fact]
    public void SignatureHelp_AfterClosedNestedCall_ReturnsToOuter()
    {
        var help = SignatureHelpProvider.GetSignatureHelp(Req("select round(abs(x), |"));
        Assert.Equal("ROUND", help!.Signature.Name);
        Assert.Equal(1, help.ActiveParameter);
    }

    [Fact]
    public void SignatureHelp_IgnoresCommasInNonFunctionParenthesesAndStrings()
    {
        var help = SignatureHelpProvider.GetSignatureHelp(Req("select replace('a,b', (1 + 2), |"));
        Assert.Equal("REPLACE", help!.Signature.Name);
        Assert.Equal(2, help.ActiveParameter);
    }

    [Theory]
    [InlineData("select myfunc(a, |")]
    [InlineData("select round(a) |")]
    [InlineData("select |")]
    public void SignatureHelp_UnknownOrClosed_IsNull(string marked) =>
        Assert.Null(SignatureHelpProvider.GetSignatureHelp(Req(marked)));
}
