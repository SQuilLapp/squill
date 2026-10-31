using Squill.Core;
using Squill.Presentation.Editor;
using Squill.Sql.Highlighting;
using Xunit;

namespace Squill.Presentation.Tests.Editor;

public sealed class SqlDemoScriptTests
{
    [Theory]
    [InlineData(1)]
    [InlineData(250)]
    public void Generate_ProducesTheRequestedNumberOfLines(int lines)
    {
        var text = SqlDemoScript.Generate(lines);

        Assert.Equal(lines, text.Split('\n').Length);
        Assert.DoesNotContain('\r', text);
    }

    [Fact]
    public void Generate_ProducesHighlightableSql()
    {
        var spans = SqlHighlighter.Classify(SqlDemoScript.Generate(20), DatabaseDialect.PostgreSql);

        Assert.Contains(spans, s => s.Kind == HighlightKind.Keyword);
        Assert.Contains(spans, s => s.Kind == HighlightKind.StringLiteral);
        Assert.Contains(spans, s => s.Kind == HighlightKind.Comment);
        Assert.Contains(spans, s => s.Kind == HighlightKind.Function);
    }

    [Fact]
    public void Generate_RejectsAnEmptyScript() =>
        Assert.Throws<ArgumentOutOfRangeException>(() => SqlDemoScript.Generate(0));
}
