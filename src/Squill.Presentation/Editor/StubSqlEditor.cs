using Squill.Core;
using Squill.Sql.Analysis;
using Squill.Sql.Completion;

namespace Squill.Presentation.Editor;

/// <summary>In-memory <see cref="ISqlEditor"/> for tests and headless heads; it records what it is given.</summary>
public sealed class StubSqlEditor : ISqlEditor
{
    private string _text = "";
    private int _caretOffset;

    /// <inheritdoc/>
    public string Text
    {
        get => _text;
        set
        {
            ArgumentNullException.ThrowIfNull(value);
            if (_text == value)
            {
                return;
            }

            _text = value;
            TextChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <inheritdoc/>
    public int CaretOffset
    {
        get => _caretOffset;
        set
        {
            if (_caretOffset == value)
            {
                return;
            }

            _caretOffset = value;
            CaretChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <inheritdoc/>
    public TextRange Selection { get; set; }

    /// <inheritdoc/>
    public event EventHandler? TextChanged;

    /// <inheritdoc/>
    public event EventHandler? CaretChanged;

    /// <summary>Gets the last dialect passed to <see cref="SetDialect"/>.</summary>
    public DatabaseDialect? Dialect { get; private set; }

    /// <summary>Gets the last diagnostics passed to <see cref="SetDiagnostics"/>.</summary>
    public IReadOnlyList<SqlDiagnostic> Diagnostics { get; private set; } = [];

    /// <summary>Gets the last provider passed to <see cref="SetCompletionProvider"/>.</summary>
    public ISqlCompletionProvider? CompletionProvider { get; private set; }

    /// <summary>Gets the last provider passed to <see cref="SetHoverProvider"/>.</summary>
    public Func<SqlRequest, HoverInfo?>? HoverProvider { get; private set; }

    /// <summary>Gets how many times <see cref="FocusEditor"/> was called.</summary>
    public int FocusCount { get; private set; }

    /// <inheritdoc/>
    public void SetDialect(DatabaseDialect d) => Dialect = d;

    /// <inheritdoc/>
    public void SetDiagnostics(IReadOnlyList<SqlDiagnostic> diagnostics) => Diagnostics = diagnostics;

    /// <inheritdoc/>
    public void SetCompletionProvider(ISqlCompletionProvider provider) => CompletionProvider = provider;

    /// <inheritdoc/>
    public void SetHoverProvider(Func<SqlRequest, HoverInfo?> provider) => HoverProvider = provider;

    /// <inheritdoc/>
    public void FocusEditor() => FocusCount++;
}
