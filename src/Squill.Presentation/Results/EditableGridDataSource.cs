using Squill.Core;
using Squill.Core.Editing;
using Squill.Presentation.Localization;

namespace Squill.Presentation.Results;

/// <summary>
/// <see cref="IGridDataSource"/> that wraps a loaded result and records pending cell edits, inserted rows and deleted
/// rows without touching the database or the wrapped data. Not thread-safe: use it from the UI thread only.
/// </summary>
public sealed class EditableGridDataSource : IGridDataSource
{
    private readonly IGridDataSource _source;
    private readonly EditTarget _target;
    private readonly Dictionary<(long Row, int Column), object?> _changes = [];
    private readonly Dictionary<long, int> _rowChanges = [];
    private readonly HashSet<long> _deleted = [];
    private readonly List<long> _removed = [];
    private readonly List<ExtraRow> _extra = [];
    private readonly Dictionary<(long Row, int Column), object?> _committed = [];

    /// <summary>Initializes a new instance of the <see cref="EditableGridDataSource"/> class.</summary>
    /// <param name="source">The loaded result; it is never modified.</param>
    /// <param name="target">The table the result edits; decides which columns are editable.</param>
    public EditableGridDataSource(IGridDataSource source, EditTarget target)
    {
        _source = source;
        _target = target;
        source.Changed += (_, e) => Changed?.Invoke(this, e);
    }

    /// <inheritdoc />
    public event EventHandler? Changed;

    /// <inheritdoc />
    public IReadOnlyList<ColumnInfo> Columns => _source.Columns;

    /// <inheritdoc />
    public long RowCount => _source.RowCount - _removed.Count + _extra.Count;

    /// <inheritdoc />
    public bool IsComplete => _source.IsComplete;

    /// <inheritdoc />
    public bool IsTruncated => _source.IsTruncated;

    /// <summary>Gets whether edits are allowed; false while rows are still streaming because indices are not stable.</summary>
    public bool CanEdit => _source.IsComplete;

    /// <summary>Gets whether any row is inserted, modified or deleted and not yet committed.</summary>
    public bool HasPendingChanges => PendingCount > 0;

    /// <summary>Gets the number of rows whose state is not <see cref="RowState.Unchanged"/>.</summary>
    public int PendingCount => _rowChanges.Count + _deleted.Count + _extra.Count(e => e.IsPending || e.Deleted || e.Changes.Count > 0);

    private long BaseVisibleCount => _source.RowCount - _removed.Count;

    /// <inheritdoc />
    public object? GetCell(long row, int column)
    {
        if (row < 0 || row >= RowCount || column < 0 || column >= Columns.Count)
        {
            return null;
        }

        if (row >= BaseVisibleCount)
        {
            var e = _extra[(int)(row - BaseVisibleCount)];
            return e.Changes.TryGetValue(column, out var c) ? c : e.Values[column];
        }

        var b = ToBase(row);
        if (_changes.TryGetValue((b, column), out var v) || _committed.TryGetValue((b, column), out v))
        {
            return v;
        }

        return _source.GetCell(b, column);
    }

    /// <summary>Gets the edit state of a row.</summary>
    /// <param name="row">Visible row index.</param>
    /// <returns>The row state.</returns>
    public RowState GetRowState(long row)
    {
        if (row < 0 || row >= RowCount)
        {
            return RowState.Unchanged;
        }

        if (row >= BaseVisibleCount)
        {
            var e = _extra[(int)(row - BaseVisibleCount)];
            return e.IsPending ? RowState.Inserted : e.Deleted ? RowState.Deleted : e.Changes.Count > 0 ? RowState.Modified : RowState.Unchanged;
        }

        var b = ToBase(row);
        return _deleted.Contains(b) ? RowState.Deleted : _rowChanges.ContainsKey(b) ? RowState.Modified : RowState.Unchanged;
    }

    /// <summary>Gets whether a cell differs from its original value (for an inserted row: whether the user set it).</summary>
    /// <param name="row">Visible row index.</param>
    /// <param name="column">Column index.</param>
    /// <returns><c>true</c> when the cell has a pending value.</returns>
    public bool IsCellModified(long row, int column)
    {
        if (row < 0 || row >= RowCount)
        {
            return false;
        }

        if (row >= BaseVisibleCount)
        {
            var e = _extra[(int)(row - BaseVisibleCount)];
            return e.IsPending ? e.IsSet[column] : e.Changes.ContainsKey(column);
        }

        return _changes.ContainsKey((ToBase(row), column));
    }

    /// <summary>Gets the committed or source value of a cell, ignoring pending changes.</summary>
    /// <param name="row">Visible row index.</param>
    /// <param name="column">Column index.</param>
    /// <returns>The original value; <c>null</c> for pending-inserted rows.</returns>
    public object? GetOriginal(long row, int column)
    {
        if (row < 0 || row >= RowCount)
        {
            return null;
        }

        if (row >= BaseVisibleCount)
        {
            var e = _extra[(int)(row - BaseVisibleCount)];
            return e.IsPending ? null : e.Values[column];
        }

        var b = ToBase(row);
        return _committed.TryGetValue((b, column), out var v) ? v : _source.GetCell(b, column);
    }

    /// <summary>Gets whether the user gave a cell of an inserted row a value.</summary>
    /// <param name="row">Visible row index.</param>
    /// <param name="column">Column index.</param>
    /// <returns><c>true</c> for every cell of a persisted row; for a pending-inserted row, whether it was set.</returns>
    public bool IsCellSet(long row, int column)
    {
        if (row >= BaseVisibleCount && row < RowCount)
        {
            var e = _extra[(int)(row - BaseVisibleCount)];
            return !e.IsPending || e.IsSet[column];
        }

        return true;
    }

    /// <summary>Gets whether a row exists in the database (false for a pending-inserted row).</summary>
    /// <param name="row">Visible row index.</param>
    /// <returns><c>true</c> when the row is persisted.</returns>
    public bool IsPersistedRow(long row) => row < BaseVisibleCount || row >= RowCount || !_extra[(int)(row - BaseVisibleCount)].IsPending;

    /// <summary>Gets why a column cannot be edited.</summary>
    /// <param name="column">Result column index.</param>
    /// <returns>A localized reason, or <c>null</c> when the column can be edited.</returns>
    public string? GetReadOnlyReason(int column)
    {
        var mapped = _target.ByOrdinal(column);
        if (mapped is null || column < 0 || column >= Columns.Count)
        {
            return UiText.Get("results.columnReadOnly");
        }

        var reason = ColumnEditability.Of(mapped.Definition);
        if (reason == ReadOnlyReason.Generated)
        {
            return UiText.Get("results.columnGenerated");
        }

        return reason == ReadOnlyReason.UnsupportedType || !CellValueParser.IsSupported(Columns[column].ClrType)
            ? UiText.Get("results.columnTypeUnsupported")
            : null;
    }

    /// <summary>Records a new value for a cell.</summary>
    /// <param name="row">Visible row index.</param>
    /// <param name="column">Column index; must be editable.</param>
    /// <param name="value">The new value; <c>null</c> for SQL NULL.</param>
    /// <exception cref="InvalidOperationException">The source is still streaming, or the row is marked deleted.</exception>
    /// <exception cref="ArgumentException">The column is read-only.</exception>
    public void SetCell(long row, int column, object? value)
    {
        CheckMutable(row);
        if (GetReadOnlyReason(column) is not null)
        {
            throw new ArgumentException($"Column {column} is read-only.", nameof(column));
        }

        if (row >= BaseVisibleCount)
        {
            var e = _extra[(int)(row - BaseVisibleCount)];
            if (e.IsPending)
            {
                e.Values[column] = value;
                e.IsSet[column] = true;
            }
            else
            {
                CheckNotDeleted(e.Deleted);
                if (ValueEquals(e.Values[column], value))
                {
                    e.Changes.Remove(column);
                }
                else
                {
                    e.Changes[column] = value;
                }
            }
        }
        else
        {
            var b = ToBase(row);
            CheckNotDeleted(_deleted.Contains(b));
            var original = _committed.TryGetValue((b, column), out var o) ? o : _source.GetCell(b, column);
            if (ValueEquals(original, value))
            {
                if (_changes.Remove((b, column)) && --_rowChanges[b] == 0)
                {
                    _rowChanges.Remove(b);
                }
            }
            else if (_changes.TryAdd((b, column), value))
            {
                _rowChanges[b] = _rowChanges.GetValueOrDefault(b) + 1;
            }
            else
            {
                _changes[(b, column)] = value;
            }
        }

        Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Appends a pending-inserted row with every cell null and unset.</summary>
    /// <returns>The index of the new row.</returns>
    /// <exception cref="InvalidOperationException">The source is still streaming.</exception>
    public long AddRow()
    {
        CheckCanEdit();
        var n = Columns.Count;
        _extra.Add(new ExtraRow(new object?[n], new bool[n]) { IsPending = true });
        Changed?.Invoke(this, EventArgs.Empty);
        return RowCount - 1;
    }

    /// <summary>Marks a row for deletion; a pending-inserted row is removed outright and later indices shift down.</summary>
    /// <param name="row">Visible row index.</param>
    /// <exception cref="InvalidOperationException">The source is still streaming.</exception>
    public void DeleteRow(long row)
    {
        CheckMutable(row);
        if (row >= BaseVisibleCount)
        {
            var i = (int)(row - BaseVisibleCount);
            if (_extra[i].IsPending)
            {
                _extra.RemoveAt(i);
            }
            else
            {
                _extra[i].Changes.Clear();
                _extra[i].Deleted = true;
            }
        }
        else
        {
            var b = ToBase(row);
            DropChanges(b);
            _deleted.Add(b);
        }

        Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Drops the pending changes and deletion flag of a row, or removes a pending-inserted row.</summary>
    /// <param name="row">Visible row index.</param>
    /// <exception cref="InvalidOperationException">The source is still streaming.</exception>
    public void RevertRow(long row)
    {
        CheckMutable(row);
        if (row >= BaseVisibleCount)
        {
            var i = (int)(row - BaseVisibleCount);
            if (_extra[i].IsPending)
            {
                _extra.RemoveAt(i);
            }
            else
            {
                _extra[i].Changes.Clear();
                _extra[i].Deleted = false;
            }
        }
        else
        {
            var b = ToBase(row);
            DropChanges(b);
            _deleted.Remove(b);
        }

        Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Drops every pending change, deletion and inserted row.</summary>
    /// <exception cref="InvalidOperationException">The source is still streaming.</exception>
    public void DiscardAll()
    {
        CheckCanEdit();
        _changes.Clear();
        _rowChanges.Clear();
        _deleted.Clear();
        _extra.RemoveAll(e => e.IsPending);
        foreach (var e in _extra)
        {
            e.Changes.Clear();
            e.Deleted = false;
        }

        Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Makes the pending state the new baseline after a successful commit.</summary>
    /// <param name="generatedKeys">Maps the grid row index (before this call) of each inserted row to its generated key value.</param>
    /// <exception cref="InvalidOperationException">The source is still streaming.</exception>
    public void AcceptChanges(IReadOnlyDictionary<long, object?> generatedKeys)
    {
        CheckCanEdit();
        var keyName = _target.KeyColumns.Count > 0 ? _target.KeyColumns[0] : null;
        var keyColumn = _target.Columns.FirstOrDefault(c => string.Equals(c.Definition.Name, keyName, StringComparison.OrdinalIgnoreCase));
        if (keyColumn is not null)
        {
            foreach (var (row, key) in generatedKeys)
            {
                if (row >= BaseVisibleCount && row < RowCount)
                {
                    var e = _extra[(int)(row - BaseVisibleCount)];
                    e.Values[keyColumn.ResultOrdinal] = key;
                    e.IsSet[keyColumn.ResultOrdinal] = true;
                }
            }
        }

        foreach (var (key, v) in _changes)
        {
            _committed[key] = v;
        }

        _changes.Clear();
        _rowChanges.Clear();

        foreach (var b in _deleted)
        {
            for (var c = 0; c < Columns.Count; c++)
            {
                _committed.Remove((b, c));
            }

            _removed.Add(b);
        }

        _deleted.Clear();
        _removed.Sort();

        for (var i = _extra.Count - 1; i >= 0; i--)
        {
            var e = _extra[i];
            if (e.Deleted)
            {
                _extra.RemoveAt(i);
                continue;
            }

            foreach (var (c, v) in e.Changes)
            {
                e.Values[c] = v;
            }

            e.Changes.Clear();
            e.IsPending = false;
        }

        Changed?.Invoke(this, EventArgs.Empty);
    }

    private static bool ValueEquals(object? a, object? b) =>
        a is byte[] x && b is byte[] y ? x.AsSpan().SequenceEqual(y) : Equals(a, b);

    private static void CheckNotDeleted(bool deleted)
    {
        if (deleted)
        {
            throw new InvalidOperationException("The row is marked for deletion.");
        }
    }

    private void CheckCanEdit()
    {
        if (!CanEdit)
        {
            throw new InvalidOperationException("Rows are still loading.");
        }
    }

    private void CheckMutable(long row)
    {
        CheckCanEdit();
        ArgumentOutOfRangeException.ThrowIfNegative(row);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(row, RowCount);
    }

    // Visible row -> base row: removed[i] - i is non-decreasing; lo = number of removed rows before the result.
    private long ToBase(long row)
    {
        int lo = 0, hi = _removed.Count;
        while (lo < hi)
        {
            var mid = (lo + hi) / 2;
            if (_removed[mid] - mid <= row)
            {
                lo = mid + 1;
            }
            else
            {
                hi = mid;
            }
        }

        return row + lo;
    }

    private void DropChanges(long b)
    {
        if (_rowChanges.Remove(b))
        {
            foreach (var key in _changes.Keys.Where(x => x.Row == b).ToList())
            {
                _changes.Remove(key);
            }
        }
    }

    /// <summary>A row added by the user; once committed it stays here and acts like a base row.</summary>
    private sealed class ExtraRow(object?[] values, bool[] isSet)
    {
        public object?[] Values { get; } = values;

        public bool[] IsSet { get; } = isSet;

        public bool IsPending { get; set; }

        public bool Deleted { get; set; }

        public Dictionary<int, object?> Changes { get; } = [];
    }
}
