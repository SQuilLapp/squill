using System.ComponentModel;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Shapes;
using Squill.Presentation.Query;

namespace Squill.App.WinUI.Views;

/// <summary>Builds the tab-strip items of the workspace and keeps their title, environment square and unsaved dot in step with the tab view-model.</summary>
internal static class TabHeader
{
    /// <summary>Creates a tab item for a tab view-model.</summary>
    /// <param name="source">A query, designer, diagram or terminal tab view-model; it becomes the item's <see cref="FrameworkElement.Tag"/>.</param>
    /// <returns>The styled item, already connected to the view-model's changes.</returns>
    public static TabViewItem Create(object source)
    {
        var (_, environment, _) = TabInfo.Describe(source);
        var suffix = TabInfo.StyleSuffix(environment);
        var square = new Border { Style = Style($"EnvSquare{suffix}Style"), VerticalAlignment = VerticalAlignment.Center };
        var title = new TextBlock { FontSize = 13, VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis, MaxWidth = 220 };
        var dot = new Ellipse { Style = Style("TabDirtyDotStyle"), VerticalAlignment = VerticalAlignment.Center };
        var header = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Children = { square, title, dot } };
        var item = new TabViewItem { Style = Style($"SquillTabItem{suffix}Style"), Header = header, Tag = source };

        void Refresh()
        {
            var info = TabInfo.Describe(source);
            title.Text = info.Title;
            dot.Visibility = info.IsDirty ? Visibility.Visible : Visibility.Collapsed;
        }

        Refresh();
        if (source is INotifyPropertyChanged changed)
            changed.PropertyChanged += (_, _) => item.DispatcherQueue?.TryEnqueue(Refresh);
        if (source is QueryTabViewModel query)
            query.Editor.TextChanged += (_, _) => Refresh();
        return item;
    }

    private static Style Style(string key) => (Style)Application.Current.Resources[key];
}
