using Squill.Core;
using Squill.Presentation.Editor;
using Xunit;

namespace Squill.Presentation.Tests;

public sealed class StubSqlEditorTests
{
    [Fact]
    public void Text_Changed_RaisesTextChangedOnce()
    {
        var editor = new StubSqlEditor();
        var count = 0;
        editor.TextChanged += (_, _) => count++;

        editor.Text = "a";
        editor.Text = "a";

        Assert.Equal(1, count);
    }

    [Fact]
    public void CaretOffset_Changed_RaisesCaretChanged()
    {
        var editor = new StubSqlEditor();
        var count = 0;
        editor.CaretChanged += (_, _) => count++;

        editor.CaretOffset = 3;

        Assert.Equal(1, count);
    }

    [Fact]
    public void Setters_Called_RecordLastValues()
    {
        var editor = new StubSqlEditor();

        editor.SetDialect(DatabaseDialect.Sqlite);
        editor.FocusEditor();

        Assert.Equal(DatabaseDialect.Sqlite, editor.Dialect);
        Assert.Equal(1, editor.FocusCount);
        Assert.Empty(editor.Diagnostics);
    }
}
