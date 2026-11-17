using CommunityToolkit.Mvvm.ComponentModel;
using Squill.Core.Schema.Compare;
using Squill.Presentation.Localization;

namespace Squill.Presentation.Compare;

/// <summary>One row of the difference list: a checkbox, the kind glyph, the name and a short summary.</summary>
public sealed partial class CompareItemViewModel : ViewModelBase
{
    private readonly Action<CompareItemViewModel>? _changed;

    internal CompareItemViewModel(ObjectDifference difference, bool included, Action<CompareItemViewModel> changed)
    {
        Difference = difference;
        Key = ObjectKey.Of(difference);
        IsIncluded = included; // before _changed is set, so that building a row notifies nobody
        _changed = changed;
    }

    /// <summary>Gets the difference the row shows.</summary>
    public ObjectDifference Difference { get; }

    /// <summary>Gets the key the migration script builder uses to include the difference.</summary>
    public ObjectKey Key { get; }

    /// <summary>Gets the name shown, with its schema when there is one.</summary>
    public string Name => Difference.Schema is { Length: > 0 } s && Difference.Category != ObjectCategory.Routine ? $"{s}.{Difference.Name}" : Difference.Name;

    /// <summary>Gets the glyph of the kind: <c>+</c> added, <c>~</c> modified, <c>-</c> removed.</summary>
    public string Glyph => Difference.Kind switch { DifferenceKind.Added => "+", DifferenceKind.Removed => "-", _ => "~" };

    /// <summary>Gets the localized upper-case kind, such as <c>MODIFIÉ</c>.</summary>
    public string KindText => UiText.Get(Difference.Kind switch
    {
        DifferenceKind.Added => "compare.kind.added",
        DifferenceKind.Removed => "compare.kind.removed",
        _ => "compare.kind.modified",
    });

    /// <summary>Gets the short sentence under the name: <c>4 changements</c>, <c>nouvelle</c>, <c>définition</c>...</summary>
    public string Summary => Difference.Kind switch
    {
        DifferenceKind.Added => UiText.Get("compare.summary.new"),
        DifferenceKind.Removed => UiText.Get("compare.summary.missing"),
        _ when Difference.Details.Any(d => d.Kind is DetailKind.DefinitionChanged or DetailKind.DefinitionUnknown) => UiText.Get("compare.summary.definition"),
        _ when Difference.Details.Count > 0 && Difference.Details.All(d => d.Kind is DetailKind.ColumnAdded or DetailKind.ColumnRemoved or DetailKind.ColumnChanged) =>
            UiText.FormatPlural("compare.summary.columns", Difference.Details.Count),
        _ => UiText.FormatPlural("compare.summary.changes", Difference.Details.Count),
    };

    /// <summary>Gets or sets whether the difference is part of the migration script.</summary>
    [ObservableProperty]
    public partial bool IsIncluded { get; set; }

    partial void OnIsIncludedChanged(bool value) => _changed?.Invoke(this);
}

/// <summary>A group of the difference list (tables, views, indexes, functions) with its count.</summary>
/// <param name="Category">The category of every item.</param>
/// <param name="Title">The localized upper-case title.</param>
/// <param name="Items">The rows that pass the filters.</param>
public sealed record CompareGroupViewModel(ObjectCategory Category, string Title, IReadOnlyList<CompareItemViewModel> Items)
{
    /// <summary>Gets the number of rows in the group.</summary>
    public int Count => Items.Count;
}
