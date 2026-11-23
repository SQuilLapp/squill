using System.Collections.Specialized;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Squill.Presentation.Explorer;
using Squill.Presentation.Localization;
using AvaloniaPath = Avalonia.Controls.Shapes.Path;

namespace Squill.App.Avalonia.Views;

/// <summary>The DataGrip-style popup that edits what the explorer shows; it only draws <see cref="ExplorerFilterViewModel"/>, built from plain controls in code.</summary>
internal static class ExplorerFilterMenu
{
    private const double MenuWidth = 440;
    private const double ListHeight = 420;

    /// <summary>Opens the popup under a control.</summary>
    /// <param name="anchor">The control the popup hangs from.</param>
    /// <param name="vm">The popup state.</param>
    /// <returns>The flyout, so a test can read its content.</returns>
    public static Flyout Show(Control anchor, ExplorerFilterViewModel vm)
    {
        var flyout = new Flyout { Placement = PlacementMode.BottomEdgeAlignedLeft, Content = Build(anchor, vm) };
        flyout.Closed += (_, _) => flyout.Content = null;
        flyout.ShowAt(anchor);
        return flyout;
    }

    private static IBrush Brush(Control owner, string key) =>
        owner.TryFindResource(key, owner.ActualThemeVariant, out var value) && value is IBrush brush ? brush : Brushes.Gray;

    private static Button IconButton(Control owner, string icon, double size, double glyph)
    {
        var path = new AvaloniaPath { Width = glyph, Height = glyph, Stretch = Stretch.Uniform, RenderTransformOrigin = RelativePoint.Center };
        path.Classes.Add("icon");
        path.Bind(AvaloniaPath.DataProperty, owner.GetResourceObservable(icon));
        return new Button
        {
            Content = path,
            Width = size,
            Height = size,
            MinWidth = 0,
            MinHeight = 0,
            Padding = new Thickness(0),
            HorizontalContentAlignment = HorizontalAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Center,
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0),
        };
    }

    private static Button ToolButton(Control owner, string icon, string name, Action click)
    {
        var button = IconButton(owner, icon, 28, 14);
        AutomationProperties.SetName(button, name);
        ToolTip.SetTip(button, name);
        button.Click += (_, _) => click();
        return button;
    }

    private static StackPanel Build(Control owner, ExplorerFilterViewModel vm)
    {
        var search = new TextBox { Name = "FilterSearch", PlaceholderText = UiText.Get("explorer.filter"), Text = vm.SearchText, Height = 28, MinHeight = 28 };
        AutomationProperties.SetName(search, UiText.Get("explorer.filter"));
        search.TextChanged += (_, _) => vm.SearchText = search.Text ?? "";
        var bar = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 2,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(8, 6, 8, 0),
            Children =
            {
                ToolButton(owner, "SquillIconRefresh", UiText.Get("explorer.refresh"), () => vm.RefreshCommand.Execute(null)),
                ToolButton(owner, "SquillIconCollapse", UiText.Get("explorer.filter.expandAll"), () => vm.ExpandAllCommand.Execute(null)),
                ToolButton(owner, "SquillIconChevron", UiText.Get("explorer.filter.collapseAll"), () => vm.CollapseAllCommand.Execute(null)),
            },
        };

        var list = new StackPanel { Name = "FilterRows", Margin = new Thickness(0, 4) };
        var scroller = new ScrollViewer { MaxHeight = ListHeight, Content = list, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        vm.Rows.CollectionChanged += (_, e) =>
        {
            if (e.Action == NotifyCollectionChangedAction.Reset)
                list.Children.Clear();
            else if (e.Action == NotifyCollectionChangedAction.Add && e.NewItems is not null)
            {
                foreach (FilterRow row in e.NewItems)
                    AddRow(list, owner, row);
            }
        };
        foreach (var row in vm.Rows)
            AddRow(list, owner, row);

        var searchBox = new Border { Padding = new Thickness(8, 0), Child = search };
        return new StackPanel { Width = MenuWidth, Spacing = 6, Children = { bar, searchBox, scroller } };
    }

    // One grid per FilterRow; it listens to its own changes and updates its controls in place, so a toggle keeps focus and scroll.
    private static void AddRow(StackPanel list, Control owner, FilterRow row)
    {
        var muted = Brush(owner, "SquillFg3Brush");
        var grid = new Grid { Height = 26, ColumnDefinitions = new ColumnDefinitions($"{8 + (row.Depth * 20)},20,28,*,Auto") };

        AvaloniaPath? chevronIcon = null;
        if (row.CanExpand)
        {
            var chevron = IconButton(owner, "SquillIconChevron", 18, 9);
            chevronIcon = (AvaloniaPath)chevron.Content!;
            chevron.VerticalAlignment = VerticalAlignment.Center;
            chevron.Click += (_, _) => row.ToggleExpandCommand.Execute(null);
            Grid.SetColumn(chevron, 1);
            grid.Children.Add(chevron);
        }

        CheckBox? box = null;
        if (row.Kind != FilterRowKind.Pattern)
        {
            var check = new CheckBox { MinWidth = 0, Width = 24, VerticalAlignment = VerticalAlignment.Center };
            AutomationProperties.SetName(check, row.Text);
            // Never two-way: the view-model decides the state (a toggle can change other rows), the box only reports the click.
            check.Click += (_, _) =>
            {
                row.ToggleCommand.Execute(null);
                check.IsChecked = row.IsChecked;
            };
            Grid.SetColumn(check, 2);
            grid.Children.Add(check);
            box = check;
        }

        var text = new TextBlock { Text = row.Text, VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis };
        if (row.IsStrong)
            text.FontWeight = FontWeight.SemiBold;
        var content = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, VerticalAlignment = VerticalAlignment.Center, Children = { text } };
        if (row.Note is { } note)
            content.Children.Add(new TextBlock { Text = note, Foreground = muted, VerticalAlignment = VerticalAlignment.Center });

        TextBox? entry = null;
        Button? add = null;
        if (row.CanAddPattern)
        {
            var link = new Button
            {
                Content = new TextBlock { Text = UiText.Get("explorer.filter.addPattern"), FontSize = 11, Foreground = muted },
                Padding = new Thickness(4, 0),
                MinHeight = 0,
                Height = 22,
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
            };
            link.Click += (_, _) => row.StartPatternCommand.Execute(null);
            var field = new TextBox { Name = "PatternEntry", PlaceholderText = UiText.Get("explorer.filter.patternHint"), Width = 190, Height = 24, MinHeight = 24, Padding = new Thickness(6, 2), IsVisible = false };
            AutomationProperties.SetName(field, UiText.Get("explorer.filter.addPattern"));
            field.TextChanged += (_, _) => row.PatternText = field.Text ?? "";
            // Tunnelling, so Escape cancels the entry without the flyout seeing it.
            field.AddHandler(InputElement.KeyDownEvent, (_, e) =>
            {
                if (e.Key == Key.Enter)
                {
                    row.CommitPatternCommand.Execute(null);
                    e.Handled = true;
                }
                else if (e.Key == Key.Escape)
                {
                    row.CancelPatternCommand.Execute(null);
                    e.Handled = true;
                }
            }, RoutingStrategies.Tunnel);
            content.Children.Add(link);
            content.Children.Add(field);
            add = link;
            entry = field;
        }

        Grid.SetColumn(content, 3);
        grid.Children.Add(content);
        if (row.Kind == FilterRowKind.Pattern)
        {
            var remove = IconButton(owner, "SquillIconClose", 22, 10);
            remove.Margin = new Thickness(0, 0, 8, 0);
            ToolTip.SetTip(remove, UiText.Get("explorer.filter.removePattern"));
            AutomationProperties.SetName(remove, UiText.Get("explorer.filter.removePattern"));
            remove.Click += (_, _) => row.RemovePatternCommand.Execute(null);
            Grid.SetColumn(remove, 4);
            grid.Children.Add(remove);
        }

        void Sync(string? property)
        {
            if (box is not null && (property is null or nameof(FilterRow.IsChecked)))
                box.IsChecked = row.IsChecked;
            if (chevronIcon is not null && (property is null or nameof(FilterRow.IsExpanded)))
                chevronIcon.RenderTransform = new RotateTransform(row.IsExpanded ? 90 : 0);
            if (entry is not null && add is not null && (property is null or nameof(FilterRow.IsAddingPattern)))
            {
                entry.IsVisible = row.IsAddingPattern;
                add.IsVisible = !row.IsAddingPattern;
                if (row.IsAddingPattern)
                {
                    entry.Text = "";
                    entry.Focus();
                }
            }
        }

        Sync(null);
        row.PropertyChanged += (_, e) => Sync(e.PropertyName);
        list.Children.Add(grid);
        if (row is { Kind: FilterRowKind.All, Depth: 0 })
            list.Children.Add(new Border { Height = 1, Background = Brush(owner, "SquillBorderBrush"), Margin = new Thickness(8, 2) });
    }
}
