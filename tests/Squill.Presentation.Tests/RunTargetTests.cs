using Squill.Core;
using Squill.Presentation.Editor;
using Xunit;

namespace Squill.Presentation.Tests;

public sealed class RunTargetTests
{
    private const string Script = "SELECT 1; SELECT 2;";

    [Fact]
    public void Resolve_SelectionSpanningTwoStatements_ReturnsBoth()
    {
        var editor = new StubSqlEditor { Text = Script, Selection = new TextRange(0, Script.Length) };

        Assert.Equal(["SELECT 1;", "SELECT 2;"], RunTarget.Resolve(editor, DatabaseDialect.Sqlite));
    }

    [Fact]
    public void Resolve_CaretInSecondStatement_ReturnsSecond()
    {
        var editor = new StubSqlEditor { Text = Script, CaretOffset = 15 };

        Assert.Equal(["SELECT 2;"], RunTarget.Resolve(editor, DatabaseDialect.Sqlite));
    }

    [Fact]
    public void Resolve_CaretAfterLastSemicolon_ReturnsLastStatement()
    {
        var editor = new StubSqlEditor { Text = Script, CaretOffset = Script.Length };

        Assert.Equal(["SELECT 2;"], RunTarget.Resolve(editor, DatabaseDialect.Sqlite));
    }

    [Fact]
    public void Resolve_EmptyText_ReturnsEmpty()
    {
        Assert.Empty(RunTarget.Resolve(new StubSqlEditor(), DatabaseDialect.Sqlite));
    }

    [Fact]
    public void RunAll_TwoStatements_ReturnsBoth()
    {
        var editor = new StubSqlEditor { Text = Script };

        Assert.Equal(["SELECT 1;", "SELECT 2;"], RunTarget.RunAll(editor, DatabaseDialect.Sqlite));
    }
}
