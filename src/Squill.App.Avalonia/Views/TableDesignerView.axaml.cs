using Avalonia.Controls;

namespace Squill.App.Avalonia.Views;

/// <summary>The editing surface of a table designer: header, column, index and foreign key tabs, script preview and issues.</summary>
public sealed partial class TableDesignerView : UserControl
{
    /// <summary>Creates the view.</summary>
    public TableDesignerView() => InitializeComponent();
}
