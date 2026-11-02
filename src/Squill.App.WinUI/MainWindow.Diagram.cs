using System.Collections.Specialized;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Squill.Presentation.Designer;
using Squill.Presentation.Diagram;
using Squill.Presentation.Workspace;

namespace Squill.App.WinUI;

/// <summary>The ER diagram and table designer tabs of the shell.</summary>
public sealed partial class MainWindow
{
    private readonly Dictionary<object, TabViewItem> _extraItems = [];

    private void AttachDiagrams(WorkspaceViewModel workspace)
    {
        workspace.Diagrams.CollectionChanged += OnExtraTabsChanged;
        workspace.Designers.CollectionChanged += OnExtraTabsChanged;
    }

    private void OnExtraTabsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        foreach (var old in e.OldItems ?? new List<object>())
        {
            if (_extraItems.Remove(old, out var item))
                Tabs.TabItems.Remove(item);
        }

        foreach (var added in e.NewItems ?? new List<object>())
        {
            var title = added is DiagramTabViewModel diagram ? diagram.Title : ((TableDesignerViewModel)added).Title;
            var item = new TabViewItem { Header = title, Tag = added };
            _extraItems[added] = item;
            Tabs.TabItems.Add(item);
        }

        if (e.OldItems is not null)
            ShowActiveTab();
    }

    private void SelectExtraTab(object viewModel)
    {
        if (_extraItems.TryGetValue(viewModel, out var item))
            Tabs.SelectedItem = item;
    }

    private void ShowQueryArea()
    {
        HideExtraAreas();
        TerminalArea.Visibility = Visibility.Collapsed;
        ActiveTabView.Visibility = Visibility.Visible;
    }

    private void HideExtraAreas()
    {
        DiagramArea.Visibility = Visibility.Collapsed;
        DesignerArea.Visibility = Visibility.Collapsed;
    }

    private void ShowDiagram(DiagramTabViewModel diagram)
    {
        ShowOnly(DiagramArea);
        DiagramArea.ViewModel = diagram;
    }

    private void ShowDesigner(TableDesignerViewModel designer)
    {
        ShowOnly(DesignerArea);
        DesignerArea.ViewModel = designer;
    }


    private void ShowOnly(UIElement area)
    {
        ActiveTabView.Visibility = Visibility.Collapsed;
        TerminalArea.Visibility = Visibility.Collapsed;
        HideExtraAreas();
        area.Visibility = Visibility.Visible;
    }
}
