using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Squill.Core;
using Squill.Presentation.Localization;

namespace Squill.Presentation.Results;

/// <summary>A column offered by the filter editor.</summary>
/// <param name="Column">The column description.</param>
public sealed record ColumnChoice(ColumnInfo Column)
{
    /// <summary>Gets the column name.</summary>
    public string Name => Column.Name;
}

/// <summary>An operator offered by the filter editor.</summary>
/// <param name="Operator">The comparison.</param>
/// <param name="Text">The localized name.</param>
public sealed record OperatorChoice(GridFilterOperator Operator, string Text);

/// <summary>The <c>+ Filter</c> popup: column, operator and operand of one new filter. Dispose it when the popup closes.</summary>
public sealed partial class FilterEditorViewModel : ViewModelBase, IDisposable
{
    private readonly ResultViewState _state;

    internal FilterEditorViewModel(ResultViewState state, IReadOnlyList<ColumnInfo> columns)
    {
        _state = state;
        Columns = [.. columns.Select(c => new ColumnChoice(c))];
        _state.PropertyChanged += OnStateChanged;
        SelectedColumn = Columns.Count > 0 ? Columns[0] : null;
    }

    /// <summary>Gets the filterable columns.</summary>
    public IReadOnlyList<ColumnChoice> Columns { get; }

    /// <summary>Gets or sets the column to filter.</summary>
    [ObservableProperty]
    public partial ColumnChoice? SelectedColumn { get; set; }

    /// <summary>Gets the operators of the selected column, without the value-set operator (the column menu builds those).</summary>
    [ObservableProperty]
    public partial IReadOnlyList<OperatorChoice> Operators { get; private set; } = [];

    /// <summary>Gets or sets the operator.</summary>
    [ObservableProperty]
    public partial OperatorChoice? SelectedOperator { get; set; }

    /// <summary>Gets or sets the typed operand.</summary>
    [ObservableProperty]
    public partial string OperandText { get; set; } = "";

    /// <summary>Gets whether the selected operator takes an operand.</summary>
    public bool NeedsOperand => SelectedOperator is { Operator: not (GridFilterOperator.IsNull or GridFilterOperator.IsNotNull) };

    /// <summary>Gets the message of the last failed apply, or <see langword="null"/>.</summary>
    [ObservableProperty]
    public partial string? Error { get; private set; }

    /// <summary>Gets whether the popup is open: false after a successful apply or a cancel.</summary>
    [ObservableProperty]
    public partial bool IsOpen { get; private set; } = true;

    /// <summary>Gets whether a filter may be added (false while the result has pending changes).</summary>
    public bool CanChange => _state.CanChange;

    /// <summary>Gets why <see cref="CanChange"/> is false, or <see langword="null"/>.</summary>
    public string? BlockedReason => _state.BlockedReason;

    /// <inheritdoc/>
    public void Dispose() => _state.PropertyChanged -= OnStateChanged;

    partial void OnSelectedColumnChanged(ColumnChoice? value)
    {
        Operators = value is null
            ? []
            : [.. GridFilterMatcher.Operators(value.Column).Where(o => o != GridFilterOperator.In).Select(o => new OperatorChoice(o, OperatorText(o)))];
        SelectedOperator = Operators.Count > 0 ? Operators[0] : null;
        Error = null;
    }

    partial void OnSelectedOperatorChanged(OperatorChoice? value) => OnPropertyChanged(nameof(NeedsOperand));

    [RelayCommand(CanExecute = nameof(CanChange))]
    private void Apply()
    {
        if (SelectedColumn is null || SelectedOperator is null)
            return;
        if (!GridFilterMatcher.TryCreate(SelectedColumn.Column, SelectedOperator.Operator, OperandText, out var filter, out var error))
        {
            Error = error;
            return;
        }

        Error = null;
        _state.AddFilter(filter);
        IsOpen = false;
    }

    [RelayCommand]
    private void Cancel() => IsOpen = false;

    private void OnStateChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(ResultViewState.CanChange))
        {
            OnPropertyChanged(nameof(CanChange));
            OnPropertyChanged(nameof(BlockedReason));
            ApplyCommand.NotifyCanExecuteChanged();
        }
    }

    private static string OperatorText(GridFilterOperator op) => op switch
    {
        GridFilterOperator.Equals => UiText.Get("results.filter.op.equals"),
        GridFilterOperator.NotEquals => UiText.Get("results.filter.op.notEquals"),
        GridFilterOperator.Contains => UiText.Get("results.filter.op.contains"),
        GridFilterOperator.StartsWith => UiText.Get("results.filter.op.startsWith"),
        GridFilterOperator.EndsWith => UiText.Get("results.filter.op.endsWith"),
        GridFilterOperator.GreaterThan => UiText.Get("results.filter.op.greaterThan"),
        GridFilterOperator.GreaterOrEqual => UiText.Get("results.filter.op.greaterOrEqual"),
        GridFilterOperator.LessThan => UiText.Get("results.filter.op.lessThan"),
        GridFilterOperator.LessOrEqual => UiText.Get("results.filter.op.lessOrEqual"),
        GridFilterOperator.IsNull => UiText.Get("results.filter.op.isNull"),
        GridFilterOperator.IsNotNull => UiText.Get("results.filter.op.isNotNull"),
        _ => UiText.Get("results.filter.op.in"),
    };
}
