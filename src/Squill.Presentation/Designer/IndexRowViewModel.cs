using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Squill.Core.Design;

namespace Squill.Presentation.Designer;

/// <summary>An editable index row of the table designer.</summary>
public sealed partial class IndexRowViewModel : ViewModelBase
{
    internal IndexRowViewModel(IndexDesign index)
    {
        OriginalName = index.OriginalName;
        Name = index.Name;
        IsUnique = index.IsUnique;
        ColumnsText = string.Join(", ", index.Columns);
    }

    /// <summary>Gets the name in the database; null for an index added in the designer.</summary>
    public string? OriginalName { get; }

    /// <summary>Gets or sets the index name.</summary>
    [ObservableProperty]
    public partial string Name { get; set; }

    /// <summary>Gets or sets whether the index enforces uniqueness.</summary>
    [ObservableProperty]
    public partial bool IsUnique { get; set; }

    /// <summary>Gets or sets the comma separated column names.</summary>
    [ObservableProperty]
    public partial string ColumnsText { get; set; }

    internal event Action? Edited;

    internal IndexDesign ToDesign() => new(OriginalName, Name, IsUnique, TableDesignerViewModel.SplitNames(ColumnsText));

    /// <inheritdoc />
    protected override void OnPropertyChanged(PropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        Edited?.Invoke();
    }
}
