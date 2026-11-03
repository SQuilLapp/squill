using Squill.Core;
using Squill.Sql.Analysis;
using Squill.Sql.Completion;
using Squill.Sql.Syntax;
using Xunit;

namespace Squill.Sql.Tests;

public sealed class MessageCodesTests
{
    private static readonly SchemaSnapshot Schema = new("public",
    [
        new TableInfo("public", "users", TableKind.Table, [new("id", "int", false, true, null), new("name", "text", true, false, null)], [], []),
        new TableInfo("public", "v", TableKind.View, [new("a", "int", true, false, null)], [], []),
    ]);

    [Theory]
    [InlineData("select 1 )", SqlMessageCodes.Unexpected, ")")]
    [InlineData("select t. from t", SqlMessageCodes.ExpectedIdentifierAfterDot, "")]
    [InlineData("select (1", SqlMessageCodes.ExpectedChar, ")")]
    [InlineData("with select 1", SqlMessageCodes.ExpectedCteName, "")]
    [InlineData("with c select 1", SqlMessageCodes.ExpectedKeyword, "AS")]
    [InlineData("insert into t (a)", SqlMessageCodes.ExpectedValuesOrQuery, "")]
    [InlineData("update t", SqlMessageCodes.ExpectedKeyword, "SET")]
    [InlineData("update t set ", SqlMessageCodes.ExpectedColumnName, "")]
    [InlineData("update t set a. = 1", SqlMessageCodes.ExpectedNameAfterDot, "")]
    [InlineData("update t set a 1", SqlMessageCodes.ExpectedChar, "=")]
    [InlineData("select from t", SqlMessageCodes.ExpectedSelectItem, "")]
    [InlineData("select a as from t", SqlMessageCodes.ExpectedAlias, "")]
    [InlineData("select * from t where", SqlMessageCodes.ExpectedExpression, "")]
    [InlineData("select cast(1 as)", SqlMessageCodes.ExpectedTypeName, "")]
    [InlineData("select * from", SqlMessageCodes.ExpectedTableName, "")]
    [InlineData("select * from t order", SqlMessageCodes.ExpectedKeyword, "BY")]
    [InlineData("select case when 1 from t", SqlMessageCodes.ExpectedKeyword, "THEN")]
    [InlineData("select case end", SqlMessageCodes.ExpectedKeyword, "WHEN")]
    [InlineData("select case when 1 then 2", SqlMessageCodes.ExpectedKeyword, "END")]
    [InlineData("select * from t where a between 1", SqlMessageCodes.ExpectedKeyword, "AND")]
    public void Parser_ReportsTheCodeAndArgsOfEachProblem(string sql, string code, string args)
    {
        var diagnostics = SqlParser.Parse(sql, DatabaseDialect.PostgreSql).Diagnostics;
        Assert.Contains(diagnostics, d => d.Code == code && string.Join(",", d.Args ?? []) == args);
        Assert.All(diagnostics, d => Assert.NotNull(d.Code));
    }

    [Fact]
    public void ParseText_TrailingTokenIsUnexpectedToken()
    {
        ExpressionParser.ParseText("1 2", DatabaseDialect.PostgreSql, out var diagnostics);
        Assert.Equal([SqlMessageCodes.UnexpectedToken], diagnostics.Select(d => d.Code));
    }

    [Fact]
    public void Diagnostics_CarryTheCodeOfTheirMessage()
    {
        var all = SqlDiagnosticsProvider.GetDiagnostics(new SqlRequest("select nope from missing", 0, DatabaseDialect.PostgreSql, Schema));
        Assert.Contains(all, d => d is { Code: "SQL1001", MessageCode: SqlMessageCodes.UnknownTable, Message: "Unknown table 'missing'" } && d.Args!.SequenceEqual(["missing"]));

        var column = SqlDiagnosticsProvider.GetDiagnostics(new SqlRequest("select nope from users", 0, DatabaseDialect.PostgreSql, Schema));
        Assert.Contains(column, d => d is { Code: "SQL1002", MessageCode: SqlMessageCodes.UnknownColumn } && d.Args!.SequenceEqual(["nope", "users"]));

        var syntax = SqlDiagnosticsProvider.GetDiagnostics(new SqlRequest("select 1 )", 0, DatabaseDialect.PostgreSql, null));
        Assert.Contains(syntax, d => d is { Code: "SQL0001", MessageCode: SqlMessageCodes.Unexpected });
    }

    [Theory]
    [InlineData("select * from us|ers", SqlMessageCodes.HoverTable, "2,public.users", "**table** public.users (2 columns)")]
    [InlineData("select * from |v", SqlMessageCodes.HoverView, "1,public.v", "**view** public.v (1 column)")]
    [InlineData("select na|me from users", SqlMessageCodes.HoverColumnNullable, "name,text", "`name`: text, nullable")]
    [InlineData("select i|d from users", SqlMessageCodes.HoverColumnNotNullPk, "id,int", "`id`: int, not null, PK")]
    public void Hover_CarriesTheCodeOfItsText(string marked, string code, string args, string markdown)
    {
        var offset = marked.IndexOf('|');
        var hover = SqlHoverProvider.GetHover(new SqlRequest(marked.Remove(offset, 1), offset, DatabaseDialect.PostgreSql, Schema));
        Assert.NotNull(hover);
        Assert.Equal((code, args, markdown), (hover.Code, string.Join(",", hover.Args!), hover.Markdown));
    }

    [Fact]
    public void Hover_NotNullAndNullablePrimaryKeyHaveTheirOwnCodes()
    {
        var schema = new SchemaSnapshot("public",
        [
            new TableInfo("public", "t", TableKind.Table, [new("a", "int", false, false, null), new("b", "int", true, true, null)], [], []),
        ]);
        Assert.Equal(SqlMessageCodes.HoverColumnNotNull, SqlHoverProvider.GetHover(new SqlRequest("select a from t", 7, DatabaseDialect.PostgreSql, schema))?.Code);
        Assert.Equal(SqlMessageCodes.HoverColumnNullablePk, SqlHoverProvider.GetHover(new SqlRequest("select b from t", 7, DatabaseDialect.PostgreSql, schema))?.Code);
    }

    [Fact]
    public void All_ListsEveryConstantOfTheSource()
    {
        var root = AppContext.BaseDirectory;
        while (root is not null && !Directory.Exists(Path.Combine(root, "src")))
            root = Path.GetDirectoryName(root);
        Assert.NotNull(root);

        var source = File.ReadAllText(Path.Combine(root, "src", "Squill.Sql", "SqlMessageCodes.cs"));
        var declared = System.Text.RegularExpressions.Regex.Matches(source, "public const string \\w+ = \"([^\"]+)\"").Select(m => m.Groups[1].Value);
        Assert.Equal(declared.Order(), SqlMessageCodes.All.Order());
    }
}
