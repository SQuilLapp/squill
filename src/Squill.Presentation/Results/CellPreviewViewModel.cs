using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
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

    private static readonly JsonWriterOptions PrettyOptions = new() { Indented = true, Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    private readonly ResultViewModel _result;
    private IGridDataSource? _grid;
    private string _original = "";
    private bool _dismissed;
    private long _row;
    private int _column;

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
        Watch();
    }

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
        IsDirty = CanEdit && value != _original;
        Error = Validate(value);
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
        if (TryPretty(Text, out var pretty, out _))
            Text = pretty;
    }

    [RelayCommand(CanExecute = nameof(CanApply))]
    private void Apply()
    {
        if (_result.Editor is not { } editor)
            return;
        var error = editor.TrySetCell(_row, _column, Text);
        if (error is not null)
        {
            Error = error;
            return;
        }

        _original = Text;
        IsDirty = false;
    }

    private bool CanApply() => CanEdit && IsDirty && Error is null;

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
        var isJson = json && TryPretty(value, out value, out _);
        Title = column.Name;
        TypeText = column.DataTypeName;
        IsJson = isJson;
        CanEdit = _result is { EditController: { } edit, Editor: { } editor } && editor.Grid.CanEdit
            && edit.IsColumnEditable(_column) && editor.Grid.GetRowState(_row) != RowState.Deleted;
        _original = value;
        Text = value;
        IsDirty = false;
        Error = Validate(value);
        IsOpen = !_dismissed && (isJson || json || value.Length >= LongTextLength || value.Contains('\n', StringComparison.Ordinal));
    }

    private string? Validate(string text)
    {
        if (!CanEdit)
            return null;
        if (CellEditorKinds.For(_result.DisplayGrid!.Columns[_column]) == CellEditorKind.Json && !TryPretty(text, out _, out var jsonError))
            return UiText.Format("preview.invalidJson", jsonError);
        return CellValueParser.TryParse(text, _result.DisplayGrid!.Columns[_column], out _, out var error) ? null : error;
    }

    private static bool LooksLikeJson(string text)
    {
        var t = text.AsSpan().TrimStart();
        return t.Length > 1 && (t[0] == '{' || t[0] == '[');
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
