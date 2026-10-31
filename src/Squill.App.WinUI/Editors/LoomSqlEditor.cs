#if SQUILL_LOOMEDITOR
using System.Diagnostics;
using LoomEditor.Core.Languages.Features;
using LoomEditor.Core.Markers;
using LoomEditor.Core.Model;
using LoomEditor.Core.Text;
using LoomEditor.Themes;
using LoomEditor.View.Decorations;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Squill.Core;
using Squill.Presentation.Editor;
using Squill.Sql.Analysis;
using Squill.Sql.Completion;
using Squill.Sql.Highlighting;
using Squill.Sql.Statements;
using LoomControl = global::LoomEditor.WinUI.Controls.LoomEditor;
using LoomCompletionItem = LoomEditor.Core.Languages.Features.CompletionItem;
using LoomPosition = LoomEditor.Core.Text.Position;
using LoomRange = LoomEditor.Core.Text.Range;
using LoomSelection = LoomEditor.Core.Text.Selection;
using SqlCompletionItem = Squill.Sql.Completion.CompletionItem;
using SqlCompletionKind = Squill.Sql.Completion.CompletionKind;

namespace Squill.App.WinUI.Editors;

/// <summary>
/// <see cref="ISqlEditor"/> over the LoomEditor control. LoomEditor has no SQL grammar and no public way to register one
/// (docs/poc/editor.md), so highlighting comes from <see cref="SqlHighlighter"/> and is drawn as decorations; completion,
/// hover and diagnostics use LoomEditor's provider and marker extension points.
/// </summary>
public sealed class LoomSqlEditor : ISqlEditor, IEditorControl
{
    private const string LanguageId = "sql";
    private const string MarkerOwner = "squill";
    private const int WindowPadding = 150;

    private static readonly Dictionary<HighlightKind, EditorDecorationOptions> KindOptions = RegisterStyles();

    private readonly LoomControl _editor = new() { LanguageId = LanguageId, DocumentUri = "squill://query" };
    private readonly DecorationsCollection _highlight;
    private readonly DispatcherQueueTimer _debounce;
    private readonly SqlProviders _providers;
    private DatabaseDialect _dialect;
    private IReadOnlyList<HighlightSpan>? _spans;
    private int _windowFirst;
    private int _windowLast;
    private int _generation;

    /// <summary>Initializes a new instance of the <see cref="LoomSqlEditor"/> class with an empty script.</summary>
    public LoomSqlEditor()
    {
        _highlight = _editor.CreateDecorationsCollection();
        _providers = new SqlProviders(this);
        _editor.LanguageFeatures.CompletionProviders.Register(LanguageId, _providers);
        _editor.LanguageFeatures.HoverProviders.Register(LanguageId, _providers);
        _debounce = DispatcherQueue.GetForCurrentThread().CreateTimer();
        _debounce.Interval = TimeSpan.FromMilliseconds(120);
        _debounce.IsRepeating = false;
        _debounce.Tick += (_, _) => _ = HighlightAsync(_generation);
        _editor.Model.ContentChanged += (_, _) =>
        {
            _generation++;
            _spans = null;
            _debounce.Stop();
            _debounce.Start();
            TextChanged?.Invoke(this, EventArgs.Empty);
        };
        _editor.ScrollChanged += (_, _) => OnViewportChanged();
        _editor.LayoutChanged += (_, _) => OnViewportChanged();
        _editor.SelectionChanged += (_, _) => CaretChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Gets the element to place in a view.</summary>
    public FrameworkElement Control => _editor;

    /// <summary>Gets how long the last completion request took, from the call to the adapter to the mapped items, in milliseconds.</summary>
    public double LastCompletionMs => _providers.LastCompletionMs;

    /// <summary>Raised on the UI thread when the highlighting of the current text is fully applied.</summary>
    public event EventHandler? HighlightApplied;

    /// <inheritdoc />
    public string Text
    {
        get => _editor.Model.GetValue(EndOfLinePreference.LF);
        set => _editor.ResetText(value?.ReplaceLineEndings("\n") ?? throw new ArgumentNullException(nameof(value)));
    }

    /// <inheritdoc />
    public int CaretOffset
    {
        get => _editor.Model.GetOffsetAt(_editor.Selections[0].Position);
        set => Selection = new TextRange(value, 0);
    }

    /// <inheritdoc />
    public TextRange Selection
    {
        get
        {
            var range = _editor.Selections[0].Range;
            var start = _editor.Model.GetOffsetAt(range.Start);
            return new TextRange(start, _editor.Model.GetOffsetAt(range.End) - start);
        }

        set
        {
            var model = _editor.Model;
            var length = model.GetValueLength(EndOfLinePreference.LF);
            var start = Math.Clamp(value.Start, 0, length);
            var end = Math.Clamp(value.End, start, length);
            _editor.SetSelections((LoomSelection[])[new LoomSelection(model.GetPositionAt(start), model.GetPositionAt(end))]);
        }
    }

    /// <inheritdoc />
    public event EventHandler? TextChanged;

    /// <inheritdoc />
    public event EventHandler? CaretChanged;

    /// <inheritdoc />
    public void SetDialect(DatabaseDialect d)
    {
        _dialect = d;
        _generation++;
        _ = HighlightAsync(_generation);
    }

    /// <inheritdoc />
    public void SetDiagnostics(IReadOnlyList<SqlDiagnostic> diagnostics)
    {
        var model = _editor.Model;
        var markers = diagnostics.Select(d =>
        {
            var start = model.GetPositionAt(d.Range.Start);
            var end = model.GetPositionAt(d.Range.End);
            return new MarkerData(
                d.Severity switch
                {
                    DiagnosticSeverity.Error => MarkerSeverity.Error,
                    DiagnosticSeverity.Warning => MarkerSeverity.Warning,
                    _ => MarkerSeverity.Info,
                },
                d.Message,
                start.LineNumber,
                start.Column,
                end.LineNumber,
                end.Column,
                d.Code);
        }).ToArray();
        _editor.Markers.Change(MarkerOwner, markers);
    }

    /// <inheritdoc />
    public void SetCompletionProvider(ISqlCompletionProvider provider) => _providers.Completion = provider;

    /// <inheritdoc />
    public void SetHoverProvider(Func<SqlRequest, HoverInfo?> provider) => _providers.Hover = provider;

    /// <inheritdoc />
    public void FocusEditor() => _editor.Focus(FocusState.Programmatic);

    /// <summary>Scrolls so that a line is at the top of the viewport.</summary>
    /// <param name="line">The 1-based line number.</param>
    public void ScrollToLine(int line) => _editor.ScrollTop = Math.Max(line - 1, 0) * _editor.LineHeight;

    /// <summary>Requests completion at the caret through the same adapter the suggest widget uses.</summary>
    /// <returns>The number of items returned.</returns>
    public async Task<int> RequestCompletionAsync()
    {
        var list = await _providers.ProvideCompletionItemsAsync(
            _editor.Model, _editor.Selections[0].Position, new CompletionContext(CompletionTriggerKind.Invoke), CancellationToken.None);
        return list?.Suggestions.Count ?? 0;
    }

    private static Dictionary<HighlightKind, EditorDecorationOptions> RegisterStyles()
    {
        (HighlightKind Kind, int Light, int Dark)[] palette =
        [
            (HighlightKind.Keyword, 0x0000FF, 0x569CD6),
            (HighlightKind.StringLiteral, 0xA31515, 0xCE9178),
            (HighlightKind.QuotedIdentifier, 0x267F99, 0x4EC9B0),
            (HighlightKind.Number, 0x098658, 0xB5CEA8),
            (HighlightKind.Comment, 0x008000, 0x6A9955),
            (HighlightKind.Parameter, 0x001080, 0x9CDCFE),
            (HighlightKind.Function, 0x795E26, 0xDCDCAA),
        ];
        var result = new Dictionary<HighlightKind, EditorDecorationOptions>();
        foreach (var (kind, light, dark) in palette)
        {
            var name = $"squill-sql-{kind.ToString().ToLowerInvariant()}";
            CustomDecorationStyles.Register(name, new CustomDecorationStyle { Foreground = Rgb(light), DarkForeground = Rgb(dark) });
            result[kind] = new EditorDecorationOptions { InlineClassName = name };
        }

        return result;
    }

    private static ThemeColor Rgb(int rgb) => ThemeColor.FromRgb((byte)(rgb >> 16), (byte)(rgb >> 8), (byte)rgb);

    // Decorations cost O(total) per change, so only the lines around the viewport are decorated and the window follows the scroll.
    private async Task HighlightAsync(int generation)
    {
        var text = Text;
        var dialect = _dialect;
        var spans = await Task.Run(() => SqlHighlighter.Classify(text, dialect));
        if (generation != _generation)
        {
            return;
        }

        _spans = spans;
        _windowFirst = _windowLast = 0;
        ApplyWindow();
        HighlightApplied?.Invoke(this, EventArgs.Empty);
    }

    private (int First, int Last) VisibleLines()
    {
        var lineHeight = Math.Max(_editor.LineHeight, 1);
        var first = (int)(_editor.ScrollTop / lineHeight) + 1;
        var last = first + (int)(_editor.ActualHeight / lineHeight) + 1;
        return (Math.Clamp(first, 1, _editor.Model.LineCount), Math.Clamp(last, 1, _editor.Model.LineCount));
    }

    private void OnViewportChanged()
    {
        var (first, last) = VisibleLines();
        if (_spans is not null && (first < _windowFirst + WindowPadding / 2 && _windowFirst > 1 || last > _windowLast - WindowPadding / 2 && _windowLast < _editor.Model.LineCount))
        {
            ApplyWindow();
        }
    }

    private void ApplyWindow()
    {
        if (_spans is not { } spans)
        {
            return;
        }

        var model = _editor.Model;
        var (visibleFirst, visibleLast) = VisibleLines();
        _windowFirst = Math.Max(1, visibleFirst - WindowPadding);
        _windowLast = Math.Min(model.LineCount, visibleLast + WindowPadding);
        var from = model.GetOffsetAt(new LoomPosition(_windowFirst, 1));
        var to = model.GetOffsetAt(new LoomPosition(_windowLast, model.GetLineMaxColumn(_windowLast)));
        var index = FirstSpanEndingAfter(spans, from);
        var decorations = new List<EditorDecoration>();
        for (; index < spans.Count && spans[index].Start < to; index++)
        {
            var s = spans[index];
            if (KindOptions.TryGetValue(s.Kind, out var options))
            {
                decorations.Add(new EditorDecoration(new LoomRange(model.GetPositionAt(s.Start), model.GetPositionAt(s.Start + s.Length)), options));
            }
        }

        _highlight.Set(decorations);
    }

    private static int FirstSpanEndingAfter(IReadOnlyList<HighlightSpan> spans, int offset)
    {
        int lo = 0, hi = spans.Count;
        while (lo < hi)
        {
            var mid = (lo + hi) / 2;
            if (spans[mid].Start + spans[mid].Length <= offset)
            {
                lo = mid + 1;
            }
            else
            {
                hi = mid;
            }
        }

        return lo;
    }

    private sealed class SqlProviders(LoomSqlEditor owner) : ICompletionItemProvider, IHoverProvider
    {
        public ISqlCompletionProvider? Completion { get; set; }

        public Func<SqlRequest, HoverInfo?>? Hover { get; set; }

        public double LastCompletionMs { get; private set; }

        public IReadOnlyList<string>? TriggerCharacters { get; } = (string[])["."];

        public async Task<CompletionList?> ProvideCompletionItemsAsync(TextModel model, LoomPosition position, CompletionContext context, CancellationToken token)
        {
            if (Completion is not { } provider)
            {
                return null;
            }

            var started = Stopwatch.GetTimestamp();
            var text = model.GetValue(EndOfLinePreference.LF);
            var offset = model.GetOffsetAt(position);
            var dialect = owner._dialect;
            var items = await Task.Run(
                () =>
                {
                    var (statement, shift) = CurrentStatement(text, offset, dialect);
                    return provider.GetCompletions(new SqlRequest(statement, offset - shift, dialect, null))
                        .Select(i => i with { ReplaceRange = new TextRange(i.ReplaceRange.Start + shift, i.ReplaceRange.Length) })
                        .ToArray();
                },
                token);
            var mapped = items.Select(i => Map(model, i)).ToArray();
            LastCompletionMs = Stopwatch.GetElapsedTime(started).TotalMilliseconds;
            return new CompletionList(mapped);
        }

        public async Task<Hover?> ProvideHoverAsync(TextModel model, LoomPosition position, CancellationToken token)
        {
            if (Hover is not { } provider)
            {
                return null;
            }

            var text = model.GetValue(EndOfLinePreference.LF);
            var offset = model.GetOffsetAt(position);
            var dialect = owner._dialect;
            var info = await Task.Run(
                () =>
                {
                    var (statement, shift) = CurrentStatement(text, offset, dialect);
                    return provider(new SqlRequest(statement, offset - shift, dialect, null)) is { } h
                        ? h with { Range = new TextRange(h.Range.Start + shift, h.Range.Length) }
                        : null;
                },
                token);
            return info is null
                ? null
                : new Hover((MarkdownString[])[new MarkdownString(info.Markdown)], ToRange(model, info.Range));
        }

        // Providers parse the text they get; handing them only the statement at the caret keeps a 100,000-statement script interactive.
        private static (string Text, int Shift) CurrentStatement(string text, int offset, DatabaseDialect dialect)
        {
            foreach (var span in SqlStatementSplitter.Split(text, dialect))
            {
                if (offset >= span.Start && offset <= span.Start + span.Length)
                {
                    return (text.Substring(span.Start, span.Length), span.Start);
                }
            }

            return ("", offset);
        }

        private static LoomCompletionItem Map(TextModel model, SqlCompletionItem item)
        {
            var range = ToRange(model, item.ReplaceRange);
            return new LoomCompletionItem(item.Label, item.InsertText, MapKind(item.Kind))
            {
                Detail = item.Detail,
                SortText = item.SortText,
                Range = new CompletionRange(range, range),
            };
        }

        private static CompletionItemKind MapKind(SqlCompletionKind kind) => kind switch
        {
            SqlCompletionKind.Method => CompletionItemKind.Method,
            SqlCompletionKind.Function => CompletionItemKind.Function,
            SqlCompletionKind.Field => CompletionItemKind.Field,
            SqlCompletionKind.Class => CompletionItemKind.Class,
            SqlCompletionKind.Module => CompletionItemKind.Module,
            SqlCompletionKind.Keyword => CompletionItemKind.Keyword,
            SqlCompletionKind.Snippet => CompletionItemKind.Snippet,
            SqlCompletionKind.Struct => CompletionItemKind.Struct,
            _ => CompletionItemKind.Text,
        };

        private static LoomRange ToRange(TextModel model, TextRange range) =>
            new(model.GetPositionAt(range.Start), model.GetPositionAt(range.End));
    }
}
#endif
