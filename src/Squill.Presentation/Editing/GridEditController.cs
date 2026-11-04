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

    /// <summary>Gets the text typed so far.</summary>
    public string EditText { get; private set; } = "";

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
        EditText = initialText ?? CellFormatter.Format(grid.GetCell(EditRow, EditColumn), grid.Columns[EditColumn]) ?? "";
        Error = null;
        IsEditing = true;
        Raise();
        return true;
    }

    /// <summary>Replaces the typed text and clears the error.</summary>
    /// <param name="text">The new text.</param>
    public void SetText(string text)
    {
        EditText = text;
        Error = null;
        Raise();
    }

    /// <summary>Records the typed text and moves the selection.</summary>
    /// <param name="move">Where the selection goes on success.</param>
    /// <returns><see langword="false"/> when the text was refused; editing continues and <see cref="Error"/> is set.</returns>
    public bool Commit(EditMove move)
    {
        if (!IsEditing)
            return false;
        var error = editor.TrySetCell(EditRow, EditColumn, EditText);
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
    /// <returns>The index of the new row.</returns>
    public long AddRow()
    {
        var row = editor.AddRow();
        var column = Enumerable.Range(0, editor.Grid.Columns.Count).FirstOrDefault(IsColumnEditable);
        selection.SelectCell(row, column, false);
        return row;
    }

    /// <summary>Marks the selected rows for deletion.</summary>
    public void DeleteSelectedRows() => editor.DeleteRows(SelectedRows());

    /// <summary>Drops the pending changes of the selected rows.</summary>
    public void RevertSelectedRows() => editor.RevertRows(SelectedRows());

    /// <summary>Writes tab-separated text into the grid starting at the active cell.</summary>
    /// <param name="text">The clipboard text; <c>\N</c> is NULL.</param>
    /// <returns>The first error as <c>Row n, column name: message</c>, or <see langword="null"/>; cells before it stay set.</returns>
    public string? Paste(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        if (!selection.HasSelection)
            return null;
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
                if (!IsColumnEditable(column))
                    continue;
                var error = editor.TrySetCell(row, column, cells[j] == "\\N" ? null : cells[j]);
                if (error is not null)
                    return UiText.Format("results.pasteIssue", row + 1, grid.Columns[column].Name, error);
            }
        }

        return null;
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
