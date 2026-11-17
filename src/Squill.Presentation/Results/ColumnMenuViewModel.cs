using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Squill.Core;
using Squill.Presentation.Localization;

namespace Squill.Presentation.Results;

/// <summary>One row of the value list of the column menu.</summary>
/// <param name="value">The typed value, <see langword="null"/> for the NULL row.</param>
/// <param name="text">The formatted value as shown in the list.</param>
/// <param name="count">How many loaded rows hold the value.</param>
public sealed partial class ValueItem(object? value, string text, int count) : ObservableObject
{
    /// <summary>Gets the typed value, <see langword="null"/> for the NULL row.</summary>
    public object? Value { get; } = value;

    /// <summary>Gets the formatted value as shown in the list.</summary>
    public string Text { get; } = text;

    /// <summary>Gets how many loaded rows hold the value.</summary>
    public int Count { get; } = count;

    /// <summary>Gets or sets whether the value is kept by the filter.</summary>
    [ObservableProperty]
    public partial bool IsChecked { get; set; }
}

/// <summary>The column menu of the grid header: sort, filter by value, clear, copy name. Dispose it when the menu closes.</summary>
public sealed partial class ColumnMenuViewModel : ViewModelBase, IDisposable
{
    private const int ValueCap = 500;
    private readonly ResultViewState _state;
    private readonly ColumnInfo _info;
    private readonly CancellationTokenSource _cts = new();
    private List<ValueItem> _all = [];
    private IReadOnlyList<ValueItem> _values = [];
    private string _valueSearch = "";

    internal ColumnMenuViewModel(ResultViewState state, IGridDataSource grid, int column, IUiDispatcher dispatcher)
    {
        _state = state;
        Column = column;
        _info = grid.Columns[column];
        IsLoadingValues = true;
        ValuesLoaded = LoadAsync(grid, dispatcher);
    }

    /// <summary>Gets the ordinal of the column.</summary>
    public int Column { get; }

    /// <summary>Gets the column name, for the head to copy to the clipboard (no clipboard service is reachable from Presentation).</summary>
    public string ColumnName => _info.Name;

    /// <summary>Gets whether a sort may be switched on (false while the result has pending changes).</summary>
    public bool CanSort => _state.CanChange;

    /// <summary>Gets why the menu cannot change the view, or <see langword="null"/>.</summary>
    public string? BlockedReason => _state.BlockedReason;

    /// <summary>Gets the sort applied to this column, or <see langword="null"/>.</summary>
    public GridSort? CurrentSort => _state.Sort is { } s && s.Column == Column ? s : null;

    /// <summary>Gets whether the value list is still being computed.</summary>
    [ObservableProperty]
    public partial bool IsLoadingValues { get; private set; }

    /// <summary>Gets whether the column holds more distinct values than the list shows.</summary>
    [ObservableProperty]
    public partial bool ValuesTruncated { get; private set; }

    /// <summary>Gets the hint under a truncated list, or <see langword="null"/>.</summary>
    public string? ValuesHint => ValuesTruncated ? UiText.Format("results.columnMenu.valuesTruncated", ValueCap) : null;

    /// <summary>Gets a task that completes when the value list is ready.</summary>
    public Task ValuesLoaded { get; }

    /// <summary>Gets or sets the text that narrows <see cref="Values"/> (case-insensitive); a non-empty text also unchecks every value it hides.</summary>
    public string ValueSearch
    {
        get => _valueSearch;
        set
        {
            value ??= "";
            if (!SetProperty(ref _valueSearch, value))
                return;
            var shown = Narrow(_all);
            // Searching selects: only the values that come up stay checked.
            if (value.Length > 0)
            {
                var kept = shown.ToHashSet();
                foreach (var item in _all)
                    item.IsChecked = kept.Contains(item);
            }

            Values = shown;
        }
    }

    /// <summary>Gets the value rows matching <see cref="ValueSearch"/>.</summary>
    public IReadOnlyList<ValueItem> Values
    {
        get => _values;
        private set => SetProperty(ref _values, value);
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        _cts.Cancel();
        _cts.Dispose();
    }

    [RelayCommand]
    private void SortAscending() => _state.SetSort(new GridSort(Column, false));

    [RelayCommand]
    private void SortDescending() => _state.SetSort(new GridSort(Column, true));

    // All values checked means "no value filter"; a set with nothing checked matches no row.
    [RelayCommand]
    private void ApplyValues()
    {
        if (!_state.CanChange || IsLoadingValues)
            return;
        var kept = _all.Where(v => v.IsChecked).Select(v => v.Value).ToList();
        RemoveFilters(valueFiltersOnly: true);
        if (kept.Count != _all.Count)
            _state.AddFilter(GridFilterMatcher.CreateIn(_info, kept));
    }

    [RelayCommand]
    private void ClearColumnFilter()
    {
        RemoveFilters(valueFiltersOnly: false);
        foreach (var item in _all)
            item.IsChecked = true;
    }

    private void RemoveFilters(bool valueFiltersOnly)
    {
        foreach (var chip in _state.Filters
            .Where(c => c.Filter.Column == Column && (!valueFiltersOnly || c.Filter.Operator is GridFilterOperator.In or GridFilterOperator.Equals))
            .ToList())
            _state.RemoveFilter(chip);
    }

    private List<ValueItem> Narrow(List<ValueItem> all) =>
        _valueSearch.Length == 0 ? all : [.. all.Where(v => v.Text.Contains(_valueSearch, StringComparison.OrdinalIgnoreCase))];

    private async Task LoadAsync(IGridDataSource grid, IUiDispatcher dispatcher)
    {
        var ct = _cts.Token;
        var existing = _state.Filters.Select(c => c.Filter)
            .FirstOrDefault(f => f.Column == Column && f.Operator is GridFilterOperator.In or GridFilterOperator.Equals);
        var done = new TaskCompletionSource();
        try
        {
            var (counts, truncated) = await Task.Run(() => ColumnValueCounts.Compute(grid, Column, ValueCap, ct), ct).ConfigureAwait(false);
            dispatcher.Post(() =>
            {
                try
                {
                    if (ct.IsCancellationRequested)
                        return;
                    _all = [.. counts.Select(c => new ValueItem(c.Value, c.Value is null ? "NULL" : CellFormatter.Format(c.Value, _info) ?? "NULL", c.Count)
                    {
                        IsChecked = existing is null || GridFilterMatcher.Matches(existing, _info, c.Value),
                    })];
                    Values = Narrow(_all);
                    ValuesTruncated = truncated;
                    OnPropertyChanged(nameof(ValuesHint));
                    IsLoadingValues = false;
                }
                finally
                {
                    done.SetResult();
                }
            });
            await done.Task.ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
        }
    }
}
