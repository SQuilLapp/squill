using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Squill.Core.Design;

namespace Squill.Presentation.Designer;

/// <summary>An editable column row of the table designer.</summary>
public sealed partial class ColumnRowViewModel : ViewModelBase
{
    internal ColumnRowViewModel(ColumnDesign column, bool isPrimaryKey)
    {
        OriginalName = column.OriginalName;
        Name = column.Name;
        DataType = column.DataType;
        IsNullable = column.IsNullable;
        IsPrimaryKey = isPrimaryKey;
        DefaultExpression = column.DefaultExpression;
        IsAutoIncrement = column.IsAutoIncrement;
    }

    /// <summary>Gets the name in the database; null for a column added in the designer.</summary>
    public string? OriginalName { get; }

    /// <summary>Gets or sets the column name.</summary>
    [ObservableProperty]
    public partial string Name { get; set; }

    /// <summary>Gets or sets the data type text.</summary>
    [ObservableProperty]
    public partial string DataType { get; set; }

    /// <summary>Gets or sets whether the column accepts NULL.</summary>
    [ObservableProperty]
    public partial bool IsNullable { get; set; }

    /// <summary>Gets or sets whether the column is part of the primary key; setting it makes the column non-nullable.</summary>
    [ObservableProperty]
    public partial bool IsPrimaryKey { get; set; }

    /// <summary>Gets or sets the default value expression, or null.</summary>
    [ObservableProperty]
    public partial string? DefaultExpression { get; set; }

    /// <summary>Gets or sets whether the engine generates the value.</summary>
    [ObservableProperty]
    public partial bool IsAutoIncrement { get; set; }

    /// <summary>Gets whether the column was added in the designer.</summary>
    [ObservableProperty]
    public partial bool IsNew { get; internal set; }

    /// <summary>Gets whether an existing column differs from the one in the database.</summary>
    [ObservableProperty]
    public partial bool IsModified { get; internal set; }

    /// <summary>Gets the table this column references through a foreign key of the draft, or null.</summary>
    [ObservableProperty]
    public partial string? ForeignKeyTarget { get; internal set; }

    internal event Action? Edited;

    internal ColumnDesign ToDesign() => new(OriginalName, Name, DataType, IsNullable, DefaultExpression, IsAutoIncrement);

    partial void OnIsPrimaryKeyChanged(bool value)
    {
        if (value)
            IsNullable = false;
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(PropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        // The derived state is written by the designer's own recompute: reacting to it would loop.
        if (e.PropertyName is nameof(IsNew) or nameof(IsModified) or nameof(ForeignKeyTarget))
            return;
        Edited?.Invoke();
    }
}
