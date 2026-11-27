using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Squill.Core;
using Squill.Presentation.Editing;
using Squill.Presentation.Localization;
using Squill.Presentation.Query;

namespace Squill.Presentation.Results;

/// <summary>
/// The full value of the selected grid cell, shown in a pane beside the grid when it is too long for the cell: a long text,
/// a multi-line text or a JSON document (pretty-printed). It can be edited, and applied to the cell, when the cell can be edited.
/// </summary>
public sealed partial class CellPreviewViewModel : ViewModelBase
{
    /// <summary>The length from which a value opens the pane by itself.</summary>
    public const int LongTextLength = 80;

    /// <summary>The most lines the pane puts into its text box; above it the pane shows the start of the value (a native box costs about 0.11 ms per line).</summary>
    public const int MaxShownLines = 2_000;

    /// <summary>The most characters the pane puts into its text box; above it the pane shows the start of the value (measured on the WinUI box: one line of 12,000 characters is drawn, one of 30,000 or more is drawn badly or not at all).</summary>
    public const int MaxShownChars = 10_000;

    private static readonly JsonWriterOptions PrettyOptions = new() { Indented = true, NewLine = "\n", Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    private readonly ResultViewModel _result;
    private IGridDataSource? _grid;
    private string _original = "";
    private bool _dismissed;
    private long _row;
    private int _column;
    private int _request;
    private bool _refreshing;

    /// <summary>Follows the selection and the data of a result.</summary>
    /// <param name="result">The grid result whose selected cell is previewed.</param>
    internal CellPreviewViewModel(ResultViewModel result)
    {
        _result = result;
        result.Selection!.Changed += (_, _) =>
        {
            _dismissed = false;
            Refresh();
        };
        result.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(ResultViewModel.DisplayGrid))
                Watch();
        };
        PropertyChanged += (_, _) => ViewChanged?.Invoke(this, EventArgs.Empty);
        ApplyCommand.CanExecuteChanged += (_, _) => ViewChanged?.Invoke(this, EventArgs.Empty);
        Watch();
    }

    /// <summary>Raised when anything a head shows changed: a property, or whether <see cref="ApplyCommand"/> can run.</summary>
    public event EventHandler? ViewChanged;

    /// <summary>Gets whether the format button is shown.</summary>
    public bool ShowFormat => IsJson || CanEdit || IsTruncated;

    /// <summary>Gets whether the format button can be used: the text is JSON, or only the start of the value is shown and the button shows all of it.</summary>
    public bool CanFormat => IsJson || IsTruncated;

    /// <summary>Gets the text of the format button, "Show all" while only the start of the value is shown.</summary>
    public string FormatLabel => UiText.Get(IsTruncated ? "preview.showAll" : "preview.format");

    /// <summary>Gets the whole cell value, as the grid copies it, while <see cref="IsTruncated"/>; otherwise <see langword="null"/>.</summary>
    public string? FullValue { get; private set; }

    /// <summary>Gets whether the apply button is shown.</summary>
    public bool ShowApply => CanEdit;

    /// <summary>Gets whether a long value is being parsed or checked in the background; <see cref="ApplyCommand"/> waits for it.</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ApplyCommand))]
    public partial bool IsWorking { get; private set; }

    /// <summary>Gets the last background request, completed when the pane is idle.</summary>
    internal Task Pending { get; private set; } = Task.CompletedTask;

    /// <summary>Gets whether <see cref="Error"/> has a message to show.</summary>
    public bool HasError => !string.IsNullOrEmpty(Error);

    /// <summary>Gets whether the pane is shown: the selected cell holds a long or JSON value and the user has not closed the pane.</summary>
    [ObservableProperty]
    public partial bool IsOpen { get; private set; }

    /// <summary>Gets the name of the column of the previewed cell.</summary>
    [ObservableProperty]
    public partial string Title { get; private set; } = "";

    /// <summary>Gets the database type of the column of the previewed cell.</summary>
    [ObservableProperty]
    public partial string TypeText { get; private set; } = "";

    /// <summary>Gets or sets the text shown, pretty-printed for JSON; the user edits it when <see cref="CanEdit"/>.</summary>
    [ObservableProperty]
    public partial string Text { get; set; } = "";

    /// <summary>Gets whether the previewed value is JSON.</summary>
    [ObservableProperty]
    public partial bool IsJson { get; private set; }

    /// <summary>Gets whether the previewed cell can be edited, so the text box is not read-only and <see cref="ApplyCommand"/> exists.</summary>
    [ObservableProperty]
    public partial bool CanEdit { get; private set; }

    /// <summary>Gets whether <see cref="Text"/> is only the start of a very long value: the box is read-only and nothing can be applied.</summary>
    [ObservableProperty]
    public partial bool IsTruncated { get; private set; }

    /// <summary>Gets the sentence that says how much of the value is shown, or <see langword="null"/> when all of it is.</summary>
    [ObservableProperty]
    public partial string? Notice { get; private set; }

    /// <summary>Gets whether <see cref="Text"/> differs from the value of the cell.</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ApplyCommand))]
    public partial bool IsDirty { get; private set; }

    /// <summary>Gets why <see cref="Text"/> is not a valid value for the cell, or <see langword="null"/>.</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ApplyCommand))]
    public partial string? Error { get; private set; }

    partial void OnTextChanged(string value)
    {
        if (_refreshing || IsTruncated)
            return;
        IsDirty = CanEdit && NormalizeLineBreaks(value) != _original;
        if (CanEdit && LongPaste.IsLong(value))
        {
            var column = Column();
            StartBackground(() => Validate(value, column, false), error => Error = error);
            return;
        }

        Supersede();
        Error = Validate(value, Column(), false);
    }

    private ColumnInfo? Column() => _result.DisplayGrid is { } grid && _column < grid.Columns.Count ? grid.Columns[_column] : null;

    private void Supersede()
    {
        _request++;
        IsWorking = false;
    }

    // Runs work on the thread pool; the result is applied on the UI thread unless a newer request replaced this one.
    private void StartBackground<T>(Func<T> work, Action<T> apply)
    {
        var request = ++_request;
        IsWorking = true;
        Pending = Run();

        async Task Run()
        {
            // Yield first: a fast pool thread could finish the work before the await and run apply inline, hiding IsWorking from the caller.
            await Task.Yield();
            var result = await Task.Run(work).ConfigureAwait(true);
            if (request != _request)
                return;
            apply(result);
            IsWorking = false;
        }
    }

    /// <summary>Hides the pane until another cell is selected.</summary>
    [RelayCommand]
    private void Close()
    {
        _dismissed = true;
        IsOpen = false;
    }

    /// <summary>Rewrites <see cref="Text"/> as indented JSON when it is valid JSON.</summary>
    [RelayCommand]
    private void Format()
    {
        if (IsTruncated)
        {
            ShowAll();
            return;
        }

        if (!IsWorking && TryPretty(Text, out var pretty, out _))
            Text = pretty;
    }

    // Replaces the start of a long value with all of it; the native box may block while it is filled, the user asked for it.
    private void ShowAll()
    {
        if (IsWorking || FullValue is not { } raw)
            return;
        var column = Column();
        var canEdit = CanEdit;
        var json = IsJson;
        StartBackground(() =>
        {
            var text = raw;
            var ok = json && TryPretty(raw, out text, out _);
            return (ok, text, Error: canEdit ? Validate(text, column, ok) : null);
        }, r => Show(r.ok, r.text, r.Error));
    }

    [RelayCommand(CanExecute = nameof(CanApply))]
    private void Apply()
    {
        if (_result.Editor is not { } editor)
            return;
        var value = NormalizeLineBreaks(Text);
        var error = editor.TrySetCell(_row, _column, value);
        if (error is not null)
        {
            Error = error;
            return;
        }

        _original = value;
        IsDirty = false;
    }

    private bool CanApply() => CanEdit && IsDirty && Error is null && !IsWorking;

    private void Watch()
    {
        if (_grid is not null)
            _grid.Changed -= OnGridChanged;
        _grid = _result.DisplayGrid;
        if (_grid is not null)
            _grid.Changed += OnGridChanged;
        Refresh();
    }

    // An applied edit or a revert changes the cell under the pane; rows streaming in do not touch it.
    private void OnGridChanged(object? sender, EventArgs e)
    {
        if (!IsDirty)
            Refresh();
    }

    private void Refresh()
    {
        var selection = _result.Selection;
        var grid = _grid;
        if (selection is not { HasSelection: true, WholeColumns: false } || grid is null
            || selection.ActiveRow < 0 || selection.ActiveRow >= grid.RowCount || selection.ActiveColumn >= grid.Columns.Count)
        {
            IsOpen = false;
            return;
        }

        _row = selection.ActiveRow;
        _column = selection.ActiveColumn;
        var column = grid.Columns[_column];
        var value = CellFormatter.Format(grid.GetCell(_row, _column), column);
        if (value is null)
        {
            IsOpen = false;
            return;
        }

        var json = CellEditorKinds.For(column) == CellEditorKind.Json || LooksLikeJson(value);
        Title = column.Name;
        TypeText = column.DataTypeName;
        CanEdit = _result is { EditController: { } edit, Editor: { } editor } && editor.Grid.CanEdit
            && edit.IsColumnEditable(_column) && editor.Grid.GetRowState(_row) != RowState.Deleted;
        IsDirty = false;
        if (json && LongPaste.IsLong(value))
        {
            // Text is set once, when the pretty text is ready, so the pane's native box is not filled twice.
            var canEdit = CanEdit;
            var raw = value;
            StartBackground(() =>
            {
                var ok = TryPretty(raw, out var pretty, out _);
                var head = Head(pretty);
                return (ok, pretty, head, Error: canEdit && head is null ? Validate(pretty, column, ok) : null);
            }, r => Show(r.ok, r.head ?? r.pretty, r.Error, r.head is null ? null : raw));
            IsOpen = !_dismissed;
            return;
        }

        Supersede();
        var whole = value;
        var isJson = json && TryPretty(value, out value, out _);
        if (Head(value) is { } head)
        {
            Show(isJson, head, null, whole);
            IsOpen = !_dismissed;
            return;
        }

        Show(isJson, value, Validate(value, column, isJson));
        IsOpen = !_dismissed && (isJson || json || value.Length >= LongTextLength || value.Contains('\n', StringComparison.Ordinal));
    }

    // full is the whole value when value is only its start.
    private void Show(bool isJson, string value, string? error, string? full = null)
    {
        IsJson = isJson;
        FullValue = full;
        IsTruncated = full is not null;
        Notice = full is null ? null : UiText.Format("preview.truncated", value.Length, full.Length);
        _original = NormalizeLineBreaks(value);
        _refreshing = true;
        try
        {
            Text = value;
        }
        finally
        {
            _refreshing = false;
        }

        IsDirty = false;
        Error = error;
    }

    // jsonKnownValid skips the JSON parse when the caller already parsed the text.
    private string? Validate(string text, ColumnInfo? column, bool jsonKnownValid)
    {
        if (!CanEdit || column is null)
            return null;
        if (!jsonKnownValid && CellEditorKinds.For(column) == CellEditorKind.Json && !TryPretty(text, out _, out var jsonError))
            return UiText.Format("preview.invalidJson", jsonError);
        return CellValueParser.TryParse(text, column, out _, out var error) ? null : error;
    }

    /// <summary>Gets the start of a value too long for the pane's text box.</summary>
    /// <param name="text">The text the pane would show.</param>
    /// <returns>
    /// <see langword="null"/> when <paramref name="text"/> has at most <see cref="MaxShownLines"/> lines and <see cref="MaxShownChars"/> characters;
    /// otherwise the shorter of the first <see cref="MaxShownLines"/> lines (without the line break that ends them) and the first
    /// <see cref="MaxShownChars"/> characters, never cut inside a surrogate pair. It stops scanning at the first limit reached.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="text"/> is <see langword="null"/>.</exception>
    internal static string? Head(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var end = Math.Min(text.Length, MaxShownChars);
        var breaks = 0;
        for (var i = 0; i < end; i++)
        {
            var c = text[i];
            if (c != '\n' && c != '\r')
                continue;
            if (++breaks == MaxShownLines)
                return text[..i];
            if (c == '\r' && i + 1 < text.Length && text[i + 1] == '\n')
                i++;
        }

        if (text.Length <= MaxShownChars)
            return null;
        return char.IsHighSurrogate(text[MaxShownChars - 1]) ? text[..(MaxShownChars - 1)] : text[..MaxShownChars];
    }

    private static bool LooksLikeJson(string text)
    {
        var t = text.AsSpan().TrimStart();
        return t.Length > 1 && (t[0] == '{' || t[0] == '[');
    }

    /// <summary>Replaces every <c>\r\n</c> and every lone <c>\r</c> with <c>\n</c>; nothing else changes.</summary>
    /// <param name="text">The text to normalize.</param>
    /// <returns>The normalized text, or <paramref name="text"/> itself when it has no <c>\r</c>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="text"/> is <see langword="null"/>.</exception>
    public static string NormalizeLineBreaks(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var first = text.IndexOf('\r', StringComparison.Ordinal);
        if (first < 0)
            return text;
        var sb = new StringBuilder(text.Length).Append(text, 0, first);
        for (var i = first; i < text.Length; i++)
        {
            if (text[i] != '\r')
                sb.Append(text[i]);
            else
            {
                sb.Append('\n');
                if (i + 1 < text.Length && text[i + 1] == '\n')
                    i++;
            }
        }

        return sb.ToString();
    }

    /// <summary>Indents a JSON document.</summary>
    /// <param name="text">The JSON text.</param>
    /// <param name="pretty">The indented text, or <paramref name="text"/> when it is not valid JSON.</param>
    /// <param name="error">The parser message when it is not valid JSON.</param>
    /// <returns><see langword="true"/> when <paramref name="text"/> is valid JSON.</returns>
    internal static bool TryPretty(string text, out string pretty, out string? error)
    {
        try
        {
            using var document = JsonDocument.Parse(text);
            using var stream = new MemoryStream();
            using (var writer = new Utf8JsonWriter(stream, PrettyOptions))
                document.WriteTo(writer);
            pretty = Encoding.UTF8.GetString(stream.ToArray());
            error = null;
            return true;
        }
        catch (JsonException ex)
        {
            pretty = text;
            error = ex.Message;
            return false;
        }
    }
}
