using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Styling;
using Avalonia.Threading;
using LoomEditor.Core.Markers;
using LoomEditor.Core.Model;
using LoomEditor.Themes;
using LoomEditor.View.Decorations;
using Squill.Core;
using Squill.Presentation.Editor;
using Squill.Sql.Analysis;
using Squill.Sql.Completion;
using Squill.Sql.Highlighting;
using LoomControl = global::LoomEditor.Avalonia.Controls.LoomEditor;
using LoomPosition = LoomEditor.Core.Text.Position;
using LoomRange = LoomEditor.Core.Text.Range;
using LoomSelection = LoomEditor.Core.Text.Selection;

namespace Squill.App.Avalonia.Editors;

/// <summary>
/// An <see cref="ISqlEditor"/> on the LoomEditor control. LoomEditor has no SQL grammar, so highlighting comes from
/// <see cref="SqlHighlighter"/> and is drawn as decorations; completion, hover and diagnostics use LoomEditor's provider and marker extension points.
/// </summary>
public sealed class AvaloniaSqlEditor : ISqlEditor
{
    private const string LanguageId = "sql";

    // LoomEditor has no "sql" language, so the model of an unknown id is plain text and only providers registered for that id are asked.
    private const string ModelLanguageId = "plaintext";
    private const string MarkerOwner = "squill";
    private const int WindowPadding = 150;

    private static readonly Dictionary<HighlightKind, EditorDecorationOptions> KindOptions = RegisterStyles();

    private readonly LoomControl _editor = new() { LanguageId = LanguageId, DocumentUri = "squill://query" };
    private readonly DecorationsCollection _highlight;
    private readonly DispatcherTimer _debounce = new() { Interval = TimeSpan.FromMilliseconds(120) };
    private readonly LoomSqlProviders _providers;
    private DatabaseDialect _dialect;
    private IReadOnlyList<HighlightSpan>? _spans;
    private int _windowFirst;
    private int _windowLast;
    private int _generation;

    /// <summary>Initializes a new instance of the <see cref="AvaloniaSqlEditor"/> class with an empty script.</summary>
    public AvaloniaSqlEditor()
    {
        _highlight = _editor.CreateDecorationsCollection();
        _providers = new LoomSqlProviders(() => _dialect);
        foreach (var id in (string[])[LanguageId, ModelLanguageId])
        {
            _editor.LanguageFeatures.CompletionProviders.Register(id, _providers);
            _editor.LanguageFeatures.HoverProviders.Register(id, _providers);
        }

        _editor.Bind(TemplatedControl.FontFamilyProperty, _editor.GetResourceObservable("SquillFontMono"));
        _editor.ActualThemeVariantChanged += (_, _) => ApplyTheme();
        ApplyTheme();
        _debounce.Tick += (_, _) =>
        {
            _debounce.Stop();
            _ = HighlightAsync(_generation);
        };
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

    /// <summary>Gets the LoomEditor control to place in a view.</summary>
    public Control Control => _editor;

    /// <summary>Gets or sets a value indicating whether the user cannot edit the text.</summary>
    public bool IsReadOnly
    {
        get => _editor.IsReadOnly;
        set => _editor.IsReadOnly = value;
    }

    /// <summary>Gets or sets a value indicating whether long lines wrap at the width of the editor.</summary>
    public bool WordWrap
    {
        get => _editor.WordWrap != global::LoomEditor.Avalonia.Controls.EditorWordWrap.Off;
        set => _editor.WordWrap = value ? global::LoomEditor.Avalonia.Controls.EditorWordWrap.On : global::LoomEditor.Avalonia.Controls.EditorWordWrap.Off;
    }

    /// <summary>Hides the line numbers, the folding controls and the minimap, for a small read-only preview.</summary>
    public void UseCompactLayout()
    {
        _editor.LineNumbersEnabled = false;
        _editor.FoldingEnabled = false;
        _editor.MinimapEnabled = false;
        _editor.StickyScrollEnabled = false;
        _editor.LineDecorationsWidth = 0;
    }

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
    public void FocusEditor() => _editor.Focus();

    /// <summary>Requests completion at the caret through the same adapter the suggest widget uses.</summary>
    /// <returns>The number of items returned.</returns>
    public async Task<int> RequestCompletionAsync()
    {
        var list = await _providers.ProvideCompletionItemsAsync(
            _editor.Model, _editor.Selections[0].Position, new LoomEditor.Core.Languages.Features.CompletionContext(LoomEditor.Core.Languages.Features.CompletionTriggerKind.Invoke), CancellationToken.None);
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
            (HighlightKind.Operator, 0x000000, 0xD4D4D4),
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

    // The control takes its theme as a plain property, so it follows the application's light or dark variant here.
    private void ApplyTheme() =>
        _editor.Theme = _editor.ActualThemeVariant == ThemeVariant.Light ? EditorTheme.Light : EditorTheme.Dark;

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
    }

    private (int First, int Last) VisibleLines()
    {
        var lineHeight = Math.Max(_editor.LineHeight, 1);
        var first = (int)(_editor.ScrollTop / lineHeight) + 1;
        var last = first + (int)(_editor.Bounds.Height / lineHeight) + 1;
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
}
