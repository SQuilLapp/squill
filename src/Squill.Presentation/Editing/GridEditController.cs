using Squill.Core;
using Squill.Core.Editing;
using Squill.Presentation.Localization;
using Squill.Presentation.Results;

namespace Squill.Presentation.Editing;

/// <summary>Where the selection goes after a cell edit is committed.</summary>
public enum EditMove
{
    /// <summary>Stay on the edited cell.</summary>
    None,

    /// <summary>Move one row down.</summary>
    Down,

    /// <summary>Move one column right.</summary>
    Right,

    /// <summary>Move one column left.</summary>
    Left,
}

/// <summary>Framework-free state machine behind in-place cell editing, driven by the keyboard handlers of both heads.</summary>
/// <param name="editor">The editor view-model that records the edits.</param>
/// <param name="selection">The selection of the grid being edited.</param>
public sealed class GridEditController(ResultEditorViewModel editor, GridSelection selection)
{
    private bool _subscribed;

    /// <summary>Raised after every change of the edit state.</summary>
    public event EventHandler? EditStateChanged;

    /// <summary>Gets whether a cell is being edited.</summary>
    public bool IsEditing { get; private set; }

    /// <summary>Gets the row of the cell being edited.</summary>
    public long EditRow { get; private set; }

    /// <summary>Gets the column of the cell being edited.</summary>
    public int EditColumn { get; private set; }

    /// <summary>Gets the kind of editor the cell being edited gets.</summary>
    public CellEditorKind EditorKind { get; private set; }

    /// <summary>Gets a format hint for an empty editor, or <see langword="null"/>.</summary>
    public string? EditHint => CellEditorKinds.Hint(EditorKind);

    /// <summary>Gets the text typed so far.</summary>
    public string EditText { get; private set; } = "";

    /// <summary>Gets whether the text holds a line break, so the one-line editor shows it read-only.</summary>
    public bool IsMultiLine => IsEditing && !UsesDefault && !CellText.IsSingleLine(EditText);

    /// <summary>Gets what the one-line editor box shows: the text, or its one-line preview when <see cref="IsMultiLine"/>.</summary>
    public string EditDisplayText => IsMultiLine ? CellText.Preview(EditText) : EditText;

    /// <summary>Gets whether the text stands for the default value of the column, not for a typed value.</summary>
    public bool UsesDefault { get; private set; }

    /// <summary>Gets whether the column of the cell being edited has a default value to fall back on.</summary>
    public bool CanUseDefault => IsEditing && editor.HasDefault(EditColumn);

    /// <summary>Gets the reason the last commit was refused, or <see langword="null"/>.</summary>
    public string? Error { get; private set; }

    /// <summary>Starts editing the active cell.</summary>
    /// <param name="initialText">Replaces the current text when not <see langword="null"/> (a typed character).</param>
    /// <returns><see langword="false"/> when the cell cannot be edited.</returns>
    public bool Begin(string? initialText = null)
    {
        var grid = editor.Grid;
        if (!selection.HasSelection || !grid.CanEdit || !IsColumnEditable(selection.ActiveColumn)
            || selection.ActiveRow >= grid.RowCount || grid.GetRowState(selection.ActiveRow) == RowState.Deleted)
            return false;
        if (!_subscribed)
        {
            _subscribed = true;
            grid.Changed += (_, _) => Cancel();
        }

        EditRow = selection.ActiveRow;
        EditColumn = selection.ActiveColumn;
        EditorKind = CellEditorKinds.For(grid.Columns[EditColumn]);
        UsesDefault = initialText is null && grid.GetCell(EditRow, EditColumn) is SqlDefault;
        EditText = UsesDefault
            ? CellEditorKinds.DefaultLabel
            : CellEditorKinds.Sanitize(EditorKind, initialText ?? CellFormatter.Format(grid.GetCell(EditRow, EditColumn), grid.Columns[EditColumn]) ?? "");
        Error = UsesDefault ? null : Validate(EditText);
        IsEditing = true;
        Raise();
        return true;
    }

    /// <summary>Replaces the typed text (without the characters the column type refuses) and checks it, so a bad value shows while typing.</summary>
    /// <param name="text">The new text.</param>
    public void SetText(string text)
    {
        UsesDefault = false;
        EditText = CellEditorKinds.Sanitize(EditorKind, text);
        Error = Validate(EditText);
        Raise();
    }

    /// <summary>Pastes a text into the value being edited, keeping it exact (line breaks included).</summary>
    /// <param name="text">The clipboard text.</param>
    /// <param name="selectionStart">Start of the replaced range in the box text; ignored while the value is multi-line or the default.</param>
    /// <param name="selectionLength">Length of the replaced range; a negative length counts as 0.</param>
    public void PasteText(string text, int selectionStart, int selectionLength)
    {
        ArgumentNullException.ThrowIfNull(text);
        if (!IsEditing)
            return;
        if (IsMultiLine || UsesDefault)
        {
            SetText(text);
            return;
        }

        var start = Math.Clamp(selectionStart, 0, EditText.Length);
        var length = Math.Clamp(selectionLength, 0, EditText.Length - start);
        SetText(string.Concat(EditText.AsSpan(0, start), text, EditText.AsSpan(start + length)));
    }

    /// <summary>Applies a choice of the popup: the default value, or a text; a boolean choice is recorded at once.</summary>
    /// <param name="text">The chosen text, or <see langword="null"/> for the default value.</param>
    /// <returns><see langword="true"/> when the edit ended, so the grid takes the focus back.</returns>
    public bool Pick(string? text)
    {
        if (text is null)
            SetDefault();
        else
            SetText(text);
        return EditorKind == CellEditorKind.Boolean && Commit(EditMove.None);
    }

    /// <summary>Leaves the cell to the default value of its column.</summary>
    public void SetDefault()
    {
        if (!IsEditing)
            return;
        UsesDefault = true;
        EditText = CellEditorKinds.DefaultLabel;
        Error = null;
        Raise();
    }

    /// <summary>Moves the value of a boolean, number or date cell by one step: next choice, plus or minus one, next or previous day.</summary>
    /// <param name="direction">1 for up, -1 for down.</param>
    public void Step(int direction)
    {
        if (!IsEditing)
            return;
        var column = editor.Grid.Columns[EditColumn];
        switch (EditorKind)
        {
            case CellEditorKind.Boolean:
                var choices = CellEditorKinds.BooleanChoices;
                var at = 0;
                for (var i = 0; i < choices.Count; i++)
                {
                    if (string.Equals(choices[i].Text, EditText.Trim(), StringComparison.OrdinalIgnoreCase))
                        at = i;
                }

                SetText(choices[(at + direction + choices.Count) % choices.Count].Text);
                break;
            case CellEditorKind.Whole or CellEditorKind.Fractional
                when CellValueParser.TryParse(EditText.Trim().Length == 0 ? "0" : EditText, column, out var number, out _) && number is not null:
                SetText(CellFormatter.Format(Add(number, direction), column) ?? "");
                break;
            case CellEditorKind.Date when CellValueParser.TryParse(EditText, column, out var date, out _) && date is DateOnly day:
                SetText(CellFormatter.Format(day.AddDays(direction), column) ?? "");
                break;
        }
    }

    private static object Add(object number, int delta) => number switch
    {
        byte v => (byte)Math.Clamp(v + delta, byte.MinValue, byte.MaxValue),
        sbyte v => (sbyte)Math.Clamp(v + delta, sbyte.MinValue, sbyte.MaxValue),
        short v => (short)Math.Clamp(v + delta, short.MinValue, short.MaxValue),
        ushort v => (ushort)Math.Clamp(v + delta, ushort.MinValue, ushort.MaxValue),
        int v => (int)Math.Clamp((long)v + delta, int.MinValue, int.MaxValue),
        uint v => (uint)Math.Clamp((long)v + delta, uint.MinValue, uint.MaxValue),
        long v => (v == long.MaxValue && delta > 0) || (v == long.MinValue && delta < 0) ? v : v + delta,
        ulong v => delta < 0 ? (v == 0 ? v : v - 1) : (v == ulong.MaxValue ? v : v + 1),
        float v => v + delta,
        double v => v + delta,
        decimal v => v + delta,
        _ => number,
    };

    // A refusal of the text as it stands, or null; empty text is valid (NULL).
    private string? Validate(string text) =>
        text.Length == 0 || CellValueParser.TryParse(text, editor.Grid.Columns[EditColumn], out _, out var error) ? null : error;

    /// <summary>Records the typed text and moves the selection.</summary>
    /// <param name="move">Where the selection goes on success.</param>
    /// <returns><see langword="false"/> when the text was refused; editing continues and <see cref="Error"/> is set.</returns>
    public bool Commit(EditMove move)
    {
        if (!IsEditing)
            return false;
        var error = UsesDefault ? editor.TrySetDefault(EditRow, EditColumn) : editor.TrySetCell(EditRow, EditColumn, EditText);
        if (error is not null)
        {
            Error = error;
            Raise();
            return false;
        }

        End();
        var grid = editor.Grid;
        var (dr, dc) = move switch { EditMove.Down => (1, 0), EditMove.Right => (0, 1), EditMove.Left => (0, -1), _ => (0, 0) };
        if (dr != 0 || dc != 0)
            selection.Move(dr, dc, grid.RowCount, grid.Columns.Count, false);
        return true;
    }

    /// <summary>Ends the edit without recording anything.</summary>
    public void Cancel()
    {
        if (IsEditing)
            End();
    }

    /// <summary>Sets SQL NULL in every editable cell of the selection.</summary>
    public void ClearSelection()
    {
        var grid = editor.Grid;
        if (!selection.HasSelection || !grid.CanEdit)
            return;
        var last = Math.Min(selection.LastColumn, grid.Columns.Count - 1);
        var lastRow = Math.Min(selection.LastRow(grid.RowCount), grid.RowCount - 1);
        for (var r = selection.FirstRow(grid.RowCount); r <= lastRow; r++)
        {
            if (grid.GetRowState(r) == RowState.Deleted)
                continue;
            for (var c = selection.FirstColumn; c <= last; c++)
            {
                if (IsColumnEditable(c))
                    editor.SetNull(r, c);
            }
        }
    }

    /// <summary>Appends a row and selects its first editable cell.</summary>
    /// <returns>The index of the new row, or -1 while a filter view is active.</returns>
    public long AddRow()
    {
        var row = editor.AddRow();
        if (row < 0)
            return -1;
        var column = Enumerable.Range(0, editor.Grid.Columns.Count).FirstOrDefault(IsColumnEditable);
        selection.SelectCell(row, column, false);
        return row;
    }

    /// <summary>Marks the selected rows for deletion.</summary>
    public void DeleteSelectedRows() => editor.DeleteRows(SelectedRows());

    /// <summary>Drops the pending changes of the selected rows.</summary>
    public void RevertSelectedRows() => editor.RevertRows(SelectedRows());

    /// <summary>Writes tab-separated text into the grid starting at the active cell, as one grid change.</summary>
    /// <param name="text">The clipboard text; <c>\N</c> is NULL.</param>
    /// <returns>The first error as <c>Row n, column name: message</c>, or <see langword="null"/>; cells before it stay set.</returns>
    public string? Paste(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var plan = PlanPaste(text);
        return Apply(plan, ParseAll(plan));
    }

    /// <summary>Same as <see cref="Paste"/>, but the cell values are parsed on a thread-pool thread so a long text does not block the UI.</summary>
    /// <param name="text">The clipboard text; <c>\N</c> is NULL.</param>
    /// <param name="cancellationToken">Abandons the parse; nothing is written then.</param>
    /// <returns>The first error as <c>Row n, column name: message</c>, or <see langword="null"/>; cells before it stay set.</returns>
    public async Task<string?> PasteAsync(string text, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(text);
        var plan = PlanPaste(text);
        var parsed = await Task.Run(() => ParseAll(plan, cancellationToken), cancellationToken).ConfigureAwait(true);
        return Apply(plan, parsed);
    }

    // Maps the lines and tabs to target cells on the caller's thread; the later steps only read this plan.
    private List<(long Row, int Column, ColumnInfo Info, string? Text)> PlanPaste(string text)
    {
        var plan = new List<(long, int, ColumnInfo, string?)>();
        if (!selection.HasSelection)
            return plan;
        var grid = editor.Grid;
        var lines = text.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
        var count = lines[^1].Length == 0 ? lines.Length - 1 : lines.Length;
        for (var i = 0; i < count; i++)
        {
            var row = selection.ActiveRow + i;
            if (row >= grid.RowCount)
                break;
            var cells = lines[i].Split('\t');
            for (var j = 0; j < cells.Length; j++)
            {
                var column = selection.ActiveColumn + j;
                if (column >= grid.Columns.Count)
                    break;
                if (IsColumnEditable(column))
                    plan.Add((row, column, grid.Columns[column], cells[j] == "\\N" ? null : cells[j]));
            }
        }

        return plan;
    }

    // Pure: parses the planned cells up to the first error, without touching the grid.
    private static (List<(long Row, int Column, object? Value)> Cells, int ErrorIndex, string? Error) ParseAll(
        List<(long Row, int Column, ColumnInfo Info, string? Text)> plan, CancellationToken cancellationToken = default)
    {
        var cells = new List<(long, int, object?)>(plan.Count);
        for (var i = 0; i < plan.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!CellValueParser.TryParse(plan[i].Text, plan[i].Info, out var value, out var error))
                return (cells, i, error);
            cells.Add((plan[i].Row, plan[i].Column, value));
        }

        return (cells, -1, null);
    }

    private string? Apply(
        List<(long Row, int Column, ColumnInfo Info, string? Text)> plan,
        (List<(long Row, int Column, object? Value)> Cells, int ErrorIndex, string? Error) parsed)
    {
        var refused = editor.TrySetCells(parsed.Cells);
        var index = refused?.Index ?? parsed.ErrorIndex;
        if (index < 0)
            return null;
        var cell = plan[index];
        return UiText.Format("results.pasteIssue", cell.Row + 1, cell.Info.Name, refused?.Error ?? parsed.Error);
    }

    /// <summary>Gets the state of a row.</summary>
    /// <param name="row">Grid row index.</param>
    /// <returns>The row state.</returns>
    public RowState GetRowState(long row) => editor.Grid.GetRowState(row);

    /// <summary>Returns whether a cell holds a pending value.</summary>
    /// <param name="row">Grid row index.</param>
    /// <param name="column">Result column index.</param>
    /// <returns><see langword="true"/> when the cell was edited.</returns>
    public bool IsCellModified(long row, int column) => editor.Grid.IsCellModified(row, column);

    /// <summary>Returns whether a validation issue points at a cell.</summary>
    /// <param name="row">Grid row index.</param>
    /// <param name="column">Result column index.</param>
    /// <returns><see langword="true"/> when an issue targets the row and this column, or the whole row.</returns>
    public bool IsCellInvalid(long row, int column)
    {
        var name = editor.Target.ByOrdinal(column)?.Definition.Name;
        return editor.Issues.Any(i => i.Row == row && (i.Column is null || string.Equals(i.Column, name, StringComparison.OrdinalIgnoreCase)));
    }

    /// <summary>Returns whether a result column maps to a table column.</summary>
    /// <param name="column">Result column index.</param>
    /// <returns><see langword="true"/> when the column can be edited.</returns>
    public bool IsColumnEditable(int column) => column >= 0 && column < editor.Grid.Columns.Count && editor.Grid.GetReadOnlyReason(column) is null;

    /// <summary>Returns whether a result column is part of the target table's primary key.</summary>
    /// <param name="column">Result column index.</param>
    /// <returns><see langword="true"/> for a key column.</returns>
    public bool IsKeyColumn(int column) =>
        editor.Target.ByOrdinal(column)?.Definition.Name is { } name
        && editor.Target.KeyColumns.Contains(name, StringComparer.OrdinalIgnoreCase);

    /// <summary>Gets the placeholder drawn in a cell of a pending inserted row that the user has not set.</summary>
    /// <param name="row">Grid row index.</param>
    /// <param name="column">Result column index.</param>
    /// <returns><c>(auto)</c> for a generated column, <c>DEFAULT</c> for a column with a default, otherwise <see langword="null"/>.</returns>
    public string? GetPlaceholder(long row, int column)
    {
        if (GetRowState(row) != RowState.Inserted || IsCellModified(row, column) || editor.Target.ByOrdinal(column)?.Definition is not { } def)
        {
            return null;
        }

        return def.IsAutoIncrement ? "(auto)" : def.DefaultExpression is null ? null : "DEFAULT";
    }

    private List<long> SelectedRows()
    {
        if (!selection.HasSelection)
            return [];
        var count = editor.Grid.RowCount;
        if (selection.WholeColumns)
            return [selection.ActiveRow];
        var last = Math.Min(selection.LastRow(count), count - 1);
        var rows = new List<long>();
        for (var r = selection.FirstRow(count); r <= last; r++)
            rows.Add(r);
        return rows;
    }

    private void End()
    {
        IsEditing = false;
        Error = null;
        Raise();
    }

    private void Raise() => EditStateChanged?.Invoke(this, EventArgs.Empty);
}
