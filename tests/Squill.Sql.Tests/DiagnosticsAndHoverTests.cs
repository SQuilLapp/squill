using Squill.Core;
using Squill.Sql.Analysis;
using Squill.Sql.Completion;
using Xunit;

namespace Squill.Sql.Tests;

public sealed class DiagnosticsAndHoverTests
{
    private static TableInfo Table(string name, TableKind kind, params ColumnDefinition[] columns) =>
        new("public", name, kind, columns, [], []);

    private static readonly SchemaSnapshot Schema = new("public",
    [
        Table("users", TableKind.Table, new("id", "int", false, true, null), new("name", "text", true, false, null)),
        Table("orders", TableKind.Table, new("id", "int", false, true, null), new("user_id", "int", true, false, null)),
    ]);

    private static IReadOnlyList<SqlDiagnostic> Diagnose(string text, SchemaSnapshot? schema = null) =>
        SqlDiagnosticsProvider.GetDiagnostics(new SqlRequest(text, 0, DatabaseDialect.PostgreSql, schema));

    private static HoverInfo? Hover(string marked, SchemaSnapshot? schema = null)
    {
        var offset = marked.IndexOf('|');
        return SqlHoverProvider.GetHover(new SqlRequest(marked.Remove(offset, 1), offset, DatabaseDialect.PostgreSql, schema ?? Schema));
    }

    [Fact]
    public void GetDiagnostics_SyntaxError_ReportsErrorInsideText()
    {
        const string text = "select from where";
        var diagnostics = Diagnose(text);
        Assert.NotEmpty(diagnostics);
        Assert.All(diagnostics, d => Assert.Equal(("SQL0001", DiagnosticSeverity.Error), (d.Code, d.Severity)));
        Assert.All(diagnostics, d => Assert.True(d.Range.Start >= 0 && d.Range.End <= text.Length));
    }

    [Fact]
    public void GetDiagnostics_NoSchema_OnlySyntaxDiagnostics()
    {
        Assert.Empty(Diagnose("select * from nowhere"));
        Assert.All(Diagnose("select from where"), d => Assert.Equal("SQL0001", d.Code));
    }

    [Fact]
    public void GetDiagnostics_UnknownTable_IsWarning()
    {
        const string text = "select * from nowhere";
        var d = Assert.Single(Diagnose(text, Schema));
        Assert.Equal("SQL1001", d.Code);
        Assert.Equal(DiagnosticSeverity.Warning, d.Severity);
        Assert.Equal("Unknown table 'nowhere'", d.Message);
        Assert.Equal("nowhere", text.Substring(d.Range.Start, d.Range.Length));
    }

    [Fact]
    public void GetDiagnostics_Cte_IsNotFlagged()
    {
        Assert.Empty(Diagnose("with recent as (select id from users) select * from recent", Schema));
    }

    [Fact]
    public void GetDiagnostics_KnownTablesAndColumns_NoDiagnostics()
    {
        Assert.Empty(Diagnose("select u.name, o.user_id from users u join orders o on o.user_id = u.id where u.name <> ''", Schema));
    }

    [Fact]
    public void GetDiagnostics_UnknownQualifiedColumn_IsWarning()
    {
        const string text = "select u.nope from users u";
        var d = Assert.Single(Diagnose(text, Schema));
        Assert.Equal("SQL1002", d.Code);
        Assert.Equal("Unknown column 'nope' in 'u'", d.Message);
        Assert.Equal("nope", text.Substring(d.Range.Start, d.Range.Length));
    }

    [Fact]
    public void GetDiagnostics_UnqualifiedColumn_CheckedOnlyWithSingleSource()
    {
        var single = Assert.Single(Diagnose("select nope from users", Schema));
        Assert.Equal("SQL1002", single.Code);
        Assert.Empty(Diagnose("select nope from users u join orders o on o.id = u.id", Schema));
    }

    [Fact]
    public void GetDiagnostics_SelectAlias_IsNotFlagged()
    {
        Assert.Empty(Diagnose("select name as n from users order by n", Schema));
    }

    [Fact]
    public void GetHover_Table_DescribesIt()
    {
        var hover = Hover("select * from us|ers");
        Assert.NotNull(hover);
        Assert.Equal("**table** public.users (2 columns)", hover.Markdown);
        Assert.Equal(new TextRange(14, 5), hover.Range);
    }

    [Fact]
    public void GetHover_Column_DescribesTypeNullabilityAndKey()
    {
        Assert.Equal("`id`: int, not null, PK", Hover("select u.i|d from users u")?.Markdown);
        Assert.Equal("`name`: text, nullable", Hover("select na|me from users")?.Markdown);
    }

    [Fact]
    public void GetHover_NothingToSay_IsNull()
    {
        Assert.Null(Hover("sel|ect * from users"));
        Assert.Null(Hover("select * from no|where"));
        Assert.Null(Hover("select * from us|ers", SchemaSnapshot.Empty));
    }
}
