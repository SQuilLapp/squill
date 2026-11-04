using Avalonia.Controls;
using Avalonia.Interactivity;
using Squill.Core;
using Squill.Presentation.Explorer;
using Squill.Presentation.Localization;
using Squill.Presentation.Workspace;

namespace Squill.App.Avalonia.Views;

/// <summary>One column line of the properties panel.</summary>
/// <param name="Name">Column name.</param>
/// <param name="DataType">Data type as the engine reports it.</param>
/// <param name="IsPrimaryKey">Whether the column is part of the primary key.</param>
/// <param name="IsForeignKey">Whether the column is part of a foreign key.</param>
public sealed record ColumnRow(string Name, string DataType, bool IsPrimaryKey, bool IsForeignKey)
{
    /// <summary>Gets the key glyph: a diamond for primary keys, an arrow for foreign keys, a dot otherwise.</summary>
    public string Glyph => IsPrimaryKey ? "◆" : IsForeignKey ? "→" : "·";
}

/// <summary>The right panel describing the table or view selected in the object explorer: columns and indexes.</summary>
public sealed partial class PropertiesPanel : UserControl
{
    private TableInfo? _table;
    private WorkspaceViewModel? _workspace;

    /// <summary>Creates the panel, hidden until a table or view is shown.</summary>
    public PropertiesPanel() => InitializeComponent();

    /// <summary>Shows a table or view node, or hides the panel for any other node.</summary>
    /// <param name="node">The selected explorer node, or <see langword="null"/>.</param>
    /// <param name="workspace">The workspace whose design-table command the footer link runs.</param>
    public void Show(ExplorerNode? node, WorkspaceViewModel workspace)
    {
        _workspace = workspace;
        _table = node is { Kind: ExplorerNodeKind.Table or ExplorerNodeKind.View, Table: { } table } ? table : null;
        IsVisible = _table is not null;
        if (_table is not { } t)
            return;
        var isView = t.Kind.IsViewLike();
        KindIcon.Data = (global::Avalonia.Media.Geometry?)(this.FindResource(isView ? "SquillIconView" : "SquillIconTable"));
        NameText.Text = t.Name;
        var kind = UiText.Get(isView ? "properties.kindView" : "properties.kindTable");
        SubtitleText.Text = string.IsNullOrEmpty(t.Schema) ? kind : $"{kind} · {t.Schema}";
        ColumnsHeader.Text = UiText.Format("properties.columns", t.Columns.Count);
        ColumnList.ItemsSource = t.Columns
            .Select(c => new ColumnRow(c.Name, c.DataType, c.IsPrimaryKey, t.ForeignKeys.Any(k => k.Columns.Contains(c.Name))))
            .ToList();
        IndexesHeader.Text = UiText.Format("properties.indexes", t.Indexes.Count);
        IndexList.ItemsSource = t.Indexes.Select(i => i.Name).ToList();
        OpenDesigner.IsVisible = !isView;
    }

    private void OnOpenDesignerClicked(object? sender, RoutedEventArgs e)
    {
        if (_table is not null)
            _workspace?.DesignTableCommand.Execute(_table);
    }
}
