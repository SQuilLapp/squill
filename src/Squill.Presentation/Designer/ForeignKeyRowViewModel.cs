using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Squill.Core;
using Squill.Core.Design;

namespace Squill.Presentation.Designer;

/// <summary>An editable foreign key row of the table designer.</summary>
public sealed partial class ForeignKeyRowViewModel : ViewModelBase
{
    internal ForeignKeyRowViewModel(ForeignKeyDesign key)
    {
        OriginalName = key.OriginalName;
        Name = key.Name;
        ColumnsText = string.Join(", ", key.Columns);
        ReferencedSchema = key.ReferencedSchema;
        ReferencedTable = key.ReferencedTable;
        ReferencedColumnsText = string.Join(", ", key.ReferencedColumns);
        OnDelete = key.OnDelete;
        OnUpdate = key.OnUpdate;
    }

    /// <summary>Gets the name in the database; null for a key added in the designer.</summary>
    public string? OriginalName { get; }

    /// <summary>Gets or sets the constraint name.</summary>
    [ObservableProperty]
    public partial string Name { get; set; }

    /// <summary>Gets or sets the comma separated referencing column names.</summary>
    [ObservableProperty]
    public partial string ColumnsText { get; set; }

    /// <summary>Gets or sets the schema of the referenced table, if any.</summary>
    [ObservableProperty]
    public partial string? ReferencedSchema { get; set; }

    /// <summary>Gets or sets the referenced table name.</summary>
    [ObservableProperty]
    public partial string ReferencedTable { get; set; }

    /// <summary>Gets or sets the comma separated referenced column names.</summary>
    [ObservableProperty]
    public partial string ReferencedColumnsText { get; set; }

    /// <summary>Gets or sets the action taken when a referenced row is deleted.</summary>
    [ObservableProperty]
    public partial ReferentialAction OnDelete { get; set; }

    /// <summary>Gets or sets the action taken when a referenced key is updated.</summary>
    [ObservableProperty]
    public partial ReferentialAction OnUpdate { get; set; }

    internal event Action? Edited;

    internal ForeignKeyDesign ToDesign() => new(
        OriginalName,
        Name,
        TableDesignerViewModel.SplitNames(ColumnsText),
        ReferencedSchema,
        ReferencedTable,
        TableDesignerViewModel.SplitNames(ReferencedColumnsText),
        OnDelete,
        OnUpdate);

    /// <inheritdoc />
    protected override void OnPropertyChanged(PropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        Edited?.Invoke();
    }
}
