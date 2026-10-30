using Squill.Core;
using Squill.Sql.Analysis;
using Squill.Sql.Completion;

namespace Squill.Presentation.Editor;

/// <summary>The SQL editing surface that each head implements on top of its own text control.</summary>
public interface ISqlEditor
{
    /// <summary>Gets or sets the whole script text.</summary>
    string Text { get; set; }

    /// <summary>Gets or sets the caret offset in <see cref="Text"/>.</summary>
    int CaretOffset { get; set; }

    /// <summary>Gets or sets the selected range; a zero length means no selection.</summary>
    TextRange Selection { get; set; }

    /// <summary>Raised after <see cref="Text"/> changed.</summary>
    event EventHandler? TextChanged;

    /// <summary>Raised after <see cref="CaretOffset"/> changed.</summary>
    event EventHandler? CaretChanged;

    /// <summary>Selects the tokenizer and grammar used for highlighting.</summary>
    /// <param name="d">The database engine of the active connection.</param>
    void SetDialect(DatabaseDialect d);

    /// <summary>Replaces the diagnostics shown as squiggles.</summary>
    /// <param name="diagnostics">The current diagnostics.</param>
    void SetDiagnostics(IReadOnlyList<SqlDiagnostic> diagnostics);

    /// <summary>Sets the source of completion items.</summary>
    /// <param name="provider">The completion provider.</param>
    void SetCompletionProvider(ISqlCompletionProvider provider);

    /// <summary>Sets the source of hover descriptions.</summary>
    /// <param name="provider">A function returning the description at the request offset, or <see langword="null"/>.</param>
    void SetHoverProvider(Func<SqlRequest, HoverInfo?> provider);

    /// <summary>Moves keyboard focus to the editor.</summary>
    void FocusEditor();
}
