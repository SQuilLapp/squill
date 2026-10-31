using Avalonia.Input;
using Avalonia.Media;
using AvaloniaEdit;
using AvaloniaEdit.CodeCompletion;
using AvaloniaEdit.Document;
using AvaloniaEdit.Editing;
using Squill.Core;
using Squill.Presentation.Editor;
using Squill.Sql.Analysis;
using Squill.Sql.Completion;

namespace Squill.App.Avalonia.Editors;

/// <summary>An <see cref="ISqlEditor"/> on AvaloniaEdit with highlighting, completion, diagnostics and hover.</summary>
public sealed class AvaloniaSqlEditor : ISqlEditor
{
    private readonly SqlColorizer _colorizer = new();
    private ISqlCompletionProvider? _completion;
    private Func<SqlRequest, HoverInfo?>? _hover;
    private CompletionWindow? _window;

    /// <summary>Initializes a new instance of the <see cref="AvaloniaSqlEditor"/> class with an empty script.</summary>
    public AvaloniaSqlEditor()
    {
        Control = new TextEditor { FontFamily = new FontFamily("Cascadia Code, Consolas, Menlo, monospace"), ShowLineNumbers = true };
        var area = Control.TextArea;
        area.TextView.LineTransformers.Add(_colorizer);
        Control.TextChanged += (_, _) =>
        {
            _colorizer.Invalidate();
            area.TextView.Redraw();
            TextChanged?.Invoke(this, EventArgs.Empty);
        };
        area.Caret.PositionChanged += (_, _) => CaretChanged?.Invoke(this, EventArgs.Empty);
        area.TextEntered += (_, e) =>
        {
            if (e.Text is { Length: > 0 } t && (char.IsLetterOrDigit(t[0]) || t[0] is '_' or '.'))
            {
                ShowCompletion();
            }
        };
        area.KeyDown += (_, e) =>
        {
            if (e.Key == Key.Space && e.KeyModifiers == KeyModifiers.Control)
            {
                e.Handled = true;
                ShowCompletion();
            }
        };
        area.TextView.PointerMoved += (_, e) => UpdateHover(e);
    }

    /// <summary>Gets the AvaloniaEdit control to place in a view.</summary>
    public TextEditor Control { get; }

    /// <inheritdoc />
    public string Text
    {
        get => Control.Text ?? "";
        set => Control.Text = value ?? throw new ArgumentNullException(nameof(value));
    }

    /// <inheritdoc />
    public int CaretOffset
    {
        get => Control.CaretOffset;
        set => Control.CaretOffset = Math.Clamp(value, 0, Control.Document.TextLength);
    }

    /// <inheritdoc />
    public TextRange Selection
    {
        get => new(Control.SelectionStart, Control.SelectionLength);
        set
        {
            var start = Math.Clamp(value.Start, 0, Control.Document.TextLength);
            Control.Select(start, Math.Clamp(value.Length, 0, Control.Document.TextLength - start));
        }
    }

    /// <inheritdoc />
    public event EventHandler? TextChanged;

    /// <inheritdoc />
    public event EventHandler? CaretChanged;

    /// <inheritdoc />
    public void SetDialect(DatabaseDialect d)
    {
        _colorizer.Dialect = d;
        _colorizer.Invalidate();
        Control.TextArea.TextView.Redraw();
    }

    /// <inheritdoc />
    public void SetDiagnostics(IReadOnlyList<SqlDiagnostic> diagnostics)
    {
        _colorizer.SetDiagnostics(diagnostics);
        Control.TextArea.TextView.Redraw();
    }

    /// <inheritdoc />
    public void SetCompletionProvider(ISqlCompletionProvider provider) => _completion = provider;

    /// <inheritdoc />
    public void SetHoverProvider(Func<SqlRequest, HoverInfo?> provider) => _hover = provider;

    /// <inheritdoc />
    public void FocusEditor() => Control.TextArea.Focus();

    /// <summary>Asks the completion provider for items at the caret and keeps the ones matching the typed prefix.</summary>
    /// <returns>The filtered items; empty when no provider is set.</returns>
    public IReadOnlyList<CompletionItem> RequestCompletions()
    {
        if (_completion is null)
        {
            return [];
        }

        var items = _completion.GetCompletions(new SqlRequest(Text, CaretOffset, _colorizer.Dialect, null));
        return items.Count == 0 ? items : CompletionFilter.Filter(items, Prefix(items[0].ReplaceRange));
    }

    private string Prefix(TextRange range) =>
        range.Start >= 0 && range.Start <= CaretOffset ? Text[range.Start..CaretOffset] : "";

    private void ShowCompletion()
    {
        var items = RequestCompletions();
        if (items.Count == 0)
        {
            _window?.Close();
            return;
        }

        if (_window is null)
        {
            _window = new CompletionWindow(Control.TextArea);
            _window.Closed += (_, _) => _window = null;
            _window.StartOffset = items[0].ReplaceRange.Start;
            Fill(_window, items);
            _window.Show();
        }
        else
        {
            _window.CompletionList.CompletionData.Clear();
            Fill(_window, items);
        }
    }

    private static void Fill(CompletionWindow window, IReadOnlyList<CompletionItem> items)
    {
        foreach (var item in items)
        {
            window.CompletionList.CompletionData.Add(new Item(item));
        }
    }

    private void UpdateHover(PointerEventArgs e)
    {
        var view = Control.TextArea.TextView;
        var pos = view.GetPosition(e.GetPosition(view) + view.ScrollOffset);
        string? tip = null;
        if (_hover is not null && pos is { } p)
        {
            var offset = Control.Document.GetOffset(p.Location);
            tip = _hover(new SqlRequest(Text, offset, _colorizer.Dialect, null))?.Markdown;
        }

        global::Avalonia.Controls.ToolTip.SetTip(Control, tip);
    }

    private sealed class Item(CompletionItem source) : ICompletionData
    {
        public IImage? Image => null;

        public string Text => source.Label;

        public object Content => source.Label;

        public object? Description => source.Detail;

        public double Priority => 0;

        public void Complete(TextArea textArea, ISegment completionSegment, EventArgs insertionRequestEventArgs) =>
            textArea.Document.Replace(completionSegment, source.InsertText);
    }
}
