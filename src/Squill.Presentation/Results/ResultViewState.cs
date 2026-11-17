using System.Collections.ObjectModel;
using Squill.Presentation.Localization;
using Squill.Presentation.Query;

namespace Squill.Presentation.Results;

/// <summary>One filter of the filter bar with the display text of its chip.</summary>
/// <param name="Filter">The filter applied to the grid.</param>
/// <param name="Text">The chip text, for example <c>montant &gt; 100</c>; never sent to a server.</param>
public sealed record FilterChip(GridFilter Filter, string Text);

/// <summary>Filters, search text and sort of one grid result, and the filtered view they produce; both heads draw this state.</summary>
public sealed class ResultViewState : ViewModelBase, IDisposable
{
    private readonly ResultViewModel _owner;
    private readonly IUiDispatcher _dispatcher;
    private FilteredGridDataSource? _view;
    private IGridDataSource? _viewInner;
    private string _searchText = "";
    private GridSort? _sort;

    internal ResultViewState(ResultViewModel owner, IUiDispatcher dispatcher)
    {
        _owner = owner;
        _dispatcher = dispatcher;
    }

    /// <summary>Gets the active filters, combined with AND, in the order they were added.</summary>
    public ObservableCollection<FilterChip> Filters { get; } = [];

    /// <summary>Gets or sets the text every row must contain in some column; changes apply at once (the heads debounce typing). Ignored while <see cref="CanChange"/> is false.</summary>
    public string SearchText
    {
        get => _searchText;
        set
        {
            value ??= "";
            if (!CanChange && value.Length > 0)
                return;
            if (SetProperty(ref _searchText, value))
                Apply();
        }
    }

    /// <summary>Gets the sort, or <see langword="null"/> for the source order.</summary>
    public GridSort? Sort => _sort;

    /// <summary>Gets whether a filter, a search or a sort is active, so the grid shows the view and is read-only.</summary>
    public bool IsActive => Filters.Count > 0 || !string.IsNullOrWhiteSpace(_searchText) || _sort is not null;

    /// <summary>Gets whether a filter, search or sort may be switched on: false while the result has pending changes.</summary>
    public bool CanChange => !_owner.HasPendingChanges;

    /// <summary>Gets why <see cref="CanChange"/> is false, or <see langword="null"/>.</summary>
    public string? BlockedReason => CanChange ? null : UiText.Get("results.filter.blocked");

    /// <summary>Gets the footer text under the grid: loaded rows, or matching rows of loaded rows while the view is active.</summary>
    public string FooterText
    {
        get
        {
            if (_view is null || !IsActive)
            {
                var inner = _owner.InnerGrid;
                var loaded = DisplayFormat.Count(inner?.RowCount ?? 0);
                return UiText.Format("results.footer", loaded, inner?.IsTruncated == true ? loaded + "+" : loaded);
            }

            var source = DisplayFormat.Count(_view.SourceRowCount) + (_view.IsTruncated ? "+" : "");
            var key = _view.IsComplete ? "results.footer.filtered.done" : "results.footer.filtered";
            return UiText.FormatPlural(key, (int)Math.Min(_view.RowCount, int.MaxValue), source);
        }
    }

    /// <summary>Gets the filtered view while <see cref="IsActive"/>, otherwise <see langword="null"/>.</summary>
    internal IGridDataSource? ActiveGrid => IsActive ? _view : null;

    /// <summary>Adds a filter; does nothing while <see cref="CanChange"/> is false.</summary>
    /// <param name="filter">The filter; its column must exist in the result.</param>
    public void AddFilter(GridFilter filter)
    {
        ArgumentNullException.ThrowIfNull(filter);
        var columns = _owner.InnerGrid?.Columns;
        if (!CanChange || columns is null || filter.Column < 0 || filter.Column >= columns.Count)
            return;
        Filters.Add(new FilterChip(filter, GridFilterText.Format(filter, columns[filter.Column])));
        Apply();
    }

    /// <summary>Removes one filter.</summary>
    /// <param name="chip">The chip of the filter to remove.</param>
    public void RemoveFilter(FilterChip chip)
    {
        ArgumentNullException.ThrowIfNull(chip);
        if (Filters.Remove(chip))
            Apply();
    }

    /// <summary>Clears the filters, the search text and the sort.</summary>
    public void ClearFilters()
    {
        if (!IsActive)
            return;
        Filters.Clear();
        _sort = null;
        _searchText = "";
        OnPropertyChanged(nameof(SearchText));
        OnPropertyChanged(nameof(Sort));
        Apply();
    }

    /// <summary>Sets the sort; does nothing while <see cref="CanChange"/> is false and a sort is being switched on.</summary>
    /// <param name="sort">The new sort, or <see langword="null"/> for none.</param>
    public void SetSort(GridSort? sort)
    {
        if ((sort is not null && !CanChange) || Equals(_sort, sort))
            return;
        _sort = sort;
        OnPropertyChanged(nameof(Sort));
        Apply();
    }

    /// <summary>Applies one click on a column header: ascending, descending, then no sort.</summary>
    /// <param name="column">The clicked column.</param>
    public void ToggleSort(int column) => SetSort(GridSort.Next(_sort, column));

    /// <summary>Opens the menu of a column header; dispose it when the menu closes.</summary>
    /// <param name="column">The ordinal of the column.</param>
    /// <returns>The menu, or <see langword="null"/> when the result has no such column.</returns>
    public ColumnMenuViewModel? OpenColumnMenu(int column) =>
        _owner.InnerGrid is { } grid && column >= 0 && column < grid.Columns.Count ? new ColumnMenuViewModel(this, grid, column, _dispatcher) : null;

    /// <summary>Opens the <c>+ Filter</c> popup; dispose it when the popup closes.</summary>
    /// <returns>The editor, or <see langword="null"/> when the result has no grid.</returns>
    public FilterEditorViewModel? OpenFilterEditor() =>
        _owner.InnerGrid is { } grid ? new FilterEditorViewModel(this, grid.Columns) : null;

    /// <inheritdoc/>
    public void Dispose()
    {
        _view?.Dispose();
        _view = null;
    }

    internal void NotifyCanChange()
    {
        OnPropertyChanged(nameof(CanChange));
        OnPropertyChanged(nameof(BlockedReason));
    }

    // The inner grid changed (an editor was attached): the view must read the new one.
    internal void InnerChanged()
    {
        if (_view is not null)
            Apply();
    }

    private void Apply()
    {
        FilteredGridDataSource? old = null;
        if (!IsActive)
        {
            old = _view;
            _view = null;
            _viewInner = null;
        }
        else
        {
            var inner = _owner.InnerGrid!;
            if (_view is null || !ReferenceEquals(_viewInner, inner))
            {
                old = _view;
                _view = new FilteredGridDataSource(inner, _dispatcher);
                _view.Changed += (_, _) => OnPropertyChanged(nameof(FooterText));
                _viewInner = inner;
            }

            _view.Apply([.. Filters.Select(c => c.Filter)], string.IsNullOrWhiteSpace(_searchText) ? null : _searchText, _sort);
        }

        OnPropertyChanged(nameof(IsActive));
        OnPropertyChanged(nameof(FooterText));
        _owner.OnViewChanged();
        old?.Dispose();
    }
}
