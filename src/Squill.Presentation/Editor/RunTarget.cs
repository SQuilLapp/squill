using Squill.Core;
using Squill.Sql.Statements;

namespace Squill.Presentation.Editor;

/// <summary>Decides which statements a "run" command executes.</summary>
public static class RunTarget
{
    /// <summary>Resolves the statements to run: the selection, else the statement at the caret.</summary>
    /// <param name="editor">The editor holding the script.</param>
    /// <param name="d">The database engine, used to split statements.</param>
    /// <returns>The statement texts to execute; empty when the editor has no statement.</returns>
    public static IReadOnlyList<string> Resolve(ISqlEditor editor, DatabaseDialect d)
    {
        ArgumentNullException.ThrowIfNull(editor);
        var text = editor.Text;
        var selection = editor.Selection;
        if (selection.Length > 0 && selection.Start >= 0 && selection.End <= text.Length)
        {
            return SplitText(text.Substring(selection.Start, selection.Length), d);
        }

        return SqlStatementSplitter.GetStatementAt(text, editor.CaretOffset, d) is { } span
            ? [text.Substring(span.Start, span.Length)]
            : [];
    }

    /// <summary>Returns every statement of the script.</summary>
    /// <param name="editor">The editor holding the script.</param>
    /// <param name="d">The database engine, used to split statements.</param>
    /// <returns>The statement texts in order.</returns>
    public static IReadOnlyList<string> RunAll(ISqlEditor editor, DatabaseDialect d)
    {
        ArgumentNullException.ThrowIfNull(editor);
        return SplitText(editor.Text, d);
    }

    private static List<string> SplitText(string text, DatabaseDialect d) =>
        [.. SqlStatementSplitter.Split(text, d).Select(s => text.Substring(s.Start, s.Length))];
}
