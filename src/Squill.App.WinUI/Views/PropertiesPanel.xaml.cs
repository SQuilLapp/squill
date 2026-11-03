using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Squill.Presentation.Explorer;
using Squill.Presentation.Localization;
using Squill.Presentation.Workspace;

namespace Squill.App.WinUI.Views;

/// <summary>The right panel describing the table or view selected in the object explorer: columns and indexes.</summary>
public sealed partial class PropertiesPanel : UserControl
{
    private PropertiesModel? _model;
    private WorkspaceViewModel? _workspace;

    /// <summary>Creates the panel, hidden until a table or view is shown.</summary>
    public PropertiesPanel()
    {
        InitializeComponent();
        TitleText.Text = UiText.Get("properties.title");
        OpenDesignerText.Text = UiText.Get("properties.openDesigner");
    }

    /// <summary>Shows a table or view node, or hides the panel for any other node.</summary>
    /// <param name="node">The selected explorer node, or <see langword="null"/>.</param>
    /// <param name="workspace">The workspace whose design-table command the footer link runs.</param>
    public void Show(ExplorerNode? node, WorkspaceViewModel workspace)
    {
        _workspace = workspace;
        _model = PropertiesModel.For(node);
        Visibility = ExplorerGlyphs.Show(_model is not null);
        if (_model is not { } model)
            return;
        TableIcon.Visibility = ExplorerGlyphs.Show(!model.IsView);
        ViewIcon.Visibility = ExplorerGlyphs.Show(model.IsView);
        NameText.Text = model.Table.Name;
        SubtitleText.Text = model.Subtitle;
        ColumnsHeader.Text = model.ColumnsHeader;
        ColumnList.ItemsSource = model.Columns;
        IndexesHeader.Text = model.IndexesHeader;
        IndexList.ItemsSource = model.Indexes;
        OpenDesigner.Visibility = ExplorerGlyphs.Show(!model.IsView);
    }

    private void OnOpenDesignerClicked(object sender, RoutedEventArgs e)
    {
        if (_model is not null)
            _workspace?.DesignTableCommand.Execute(_model.Table);
    }
}
