using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Squill.Presentation.Explorer;

/// <summary>What a row of the explorer filter popup is.</summary>
public enum FilterRowKind
{
    /// <summary>The "All ..." row of a level, with its add pattern entry.</summary>
    All,

    /// <summary>The "Default ..." row of a level.</summary>
    Default,

    /// <summary>A wildcard pattern of a level.</summary>
    Pattern,

    /// <summary>A database, schema, table or view.</summary>
    Item,
}

/// <summary>One row of the explorer filter popup; the heads draw the flat list of <see cref="ExplorerFilterViewModel.Rows"/>.</summary>
public sealed partial class FilterRow : ViewModelBase
{
    internal Func<bool>? Evaluate { get; init; }

    internal Action? OnToggle { get; init; }

    internal Action? OnToggleExpand { get; init; }

    internal Func<string, bool>? OnCommitPattern { get; init; }

    internal Action? OnRemovePattern { get; init; }

    /// <summary>Gets what the row is.</summary>
    public FilterRowKind Kind { get; init; }

    /// <summary>Gets the name, the localized "All ..." / "Default ..." text, or the pattern as typed.</summary>
    public string Text { get; init; } = "";

    /// <summary>Gets the dimmer text drawn after <see cref="Text"/>, or <see langword="null"/>.</summary>
    public string? Note { get; init; }

    /// <summary>Gets the nesting level, 0 or 1; the heads indent by <c>Depth * 20</c> px.</summary>
    public int Depth { get; init; }

    /// <summary>Gets whether the text is bold: an item of the first level of a two-level popup.</summary>
    public bool IsStrong { get; init; }

    /// <summary>Gets whether the row can be expanded to the level under it.</summary>
    public bool CanExpand { get; init; }

    /// <summary>Gets whether the row (an "All" row) offers to add a pattern.</summary>
    public bool CanAddPattern => Kind == FilterRowKind.All;

    /// <summary>Gets or sets whether the row is checked; set only by the view-model.</summary>
    [ObservableProperty]
    public partial bool IsChecked { get; set; }

    /// <summary>Gets or sets whether the row is expanded.</summary>
    [ObservableProperty]
    public partial bool IsExpanded { get; set; }

    /// <summary>Gets or sets whether the pattern entry box is open.</summary>
    [ObservableProperty]
    public partial bool IsAddingPattern { get; set; }

    /// <summary>Gets or sets the pattern being typed.</summary>
    [ObservableProperty]
    public partial string PatternText { get; set; } = "";

    /// <summary>Checks or unchecks the row's object.</summary>
    [RelayCommand]
    private void Toggle() => OnToggle?.Invoke();

    /// <summary>Expands or collapses the row.</summary>
    [RelayCommand]
    private void ToggleExpand()
    {
        if (CanExpand)
            OnToggleExpand?.Invoke();
    }

    /// <summary>Opens the pattern entry box.</summary>
    [RelayCommand]
    private void StartPattern()
    {
        PatternText = "";
        IsAddingPattern = true;
    }

    /// <summary>Adds the typed pattern; the box stays open when the pattern is refused (blank or duplicate).</summary>
    [RelayCommand]
    private void CommitPattern()
    {
        if (OnCommitPattern?.Invoke(PatternText) == true)
        {
            IsAddingPattern = false;
            PatternText = "";
        }
    }

    /// <summary>Closes the pattern entry box.</summary>
    [RelayCommand]
    private void CancelPattern()
    {
        IsAddingPattern = false;
        PatternText = "";
    }

    /// <summary>Removes the pattern of a pattern row.</summary>
    [RelayCommand]
    private void RemovePattern() => OnRemovePattern?.Invoke();
}
