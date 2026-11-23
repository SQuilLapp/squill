using System.Collections.Specialized;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using Squill.Presentation.Explorer;
using Squill.Presentation.Localization;
using Windows.System;

namespace Squill.App.WinUI.Views;

/// <summary>The DataGrip-style popup that edits what the explorer shows; it only draws <see cref="ExplorerFilterViewModel"/>, built from plain controls in code (data-bound item lists break under Native AOT).</summary>
internal static class ExplorerFilterMenu
{
    private const double MenuWidth = 440;
    private const double ListHeight = 420;

    /// <summary>Opens the popup under an explorer row.</summary>
    /// <param name="anchor">The element the popup hangs from.</param>
    /// <param name="vm">The popup state.</param>
    public static void Show(FrameworkElement anchor, ExplorerFilterViewModel vm)
    {
        if (anchor.XamlRoot is null)
            return;
        var flyout = new Flyout { Placement = FlyoutPlacementMode.BottomEdgeAlignedLeft };
        var presenter = new Style(typeof(FlyoutPresenter));
        presenter.Setters.Add(new Setter(Control.PaddingProperty, new Thickness(0)));
        presenter.Setters.Add(new Setter(FrameworkElement.MaxWidthProperty, 600d));
        flyout.FlyoutPresenterStyle = presenter;
        flyout.Closed += (_, _) => flyout.Content = null;
        flyout.Content = Build(anchor, vm);
        flyout.ShowAt(anchor);
    }

    private static Button IconButton(double size, UIElement content) => new()
    {
        Width = size,
        Height = size,
        MinWidth = 0,
        MinHeight = 0,
        Padding = new Thickness(0),
        Background = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Transparent),
        BorderThickness = new Thickness(0),
        Content = content,
    };

    private static Grid Build(FrameworkElement owner, ExplorerFilterViewModel vm)
    {
        var search = new TextBox { PlaceholderText = UiText.Get("explorer.filter"), Text = vm.SearchText, Height = 28, MinHeight = 28 };
        AutomationProperties.SetName(search, UiText.Get("explorer.filter"));
        search.TextChanged += (_, _) => vm.SearchText = search.Text;
        var refresh = ToolButton("", UiText.Get("explorer.refresh"), () => vm.RefreshCommand.Execute(null));
        var expand = ToolButton("", UiText.Get("explorer.filter.expandAll"), () => vm.ExpandAllCommand.Execute(null));
        var collapse = ToolButton("", UiText.Get("explorer.filter.collapseAll"), () => vm.CollapseAllCommand.Execute(null));
        var bar = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 2, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(8, 6, 8, 0), Children = { refresh, expand, collapse } };

        var list = new StackPanel { Padding = new Thickness(0, 4, 0, 4) };
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

        var panel = new Grid { Width = MenuWidth, RowSpacing = 6, RowDefinitions = { new RowDefinition { Height = GridLength.Auto }, new RowDefinition { Height = GridLength.Auto }, new RowDefinition { Height = GridLength.Auto } } };
        var searchBox = new Border { Padding = new Thickness(8, 0, 8, 0), Child = search };
        Grid.SetRow(searchBox, 1);
        Grid.SetRow(scroller, 2);
        panel.Children.Add(bar);
        panel.Children.Add(searchBox);
        panel.Children.Add(scroller);
        return panel;
    }

    private static Button ToolButton(string glyph, string name, Action click)
    {
        var button = IconButton(28, new FontIcon { Glyph = glyph, FontSize = 12 });
        AutomationProperties.SetName(button, name);
        ToolTipService.SetToolTip(button, name);
        button.Click += (_, _) => click();
        return button;
    }

    // One grid per FilterRow; it listens to its own changes and updates its controls in place, so a toggle keeps focus and scroll.
    private static void AddRow(StackPanel list, FrameworkElement owner, FilterRow row)
    {
        var muted = Tokens.Brush(owner, "SquillFg3Brush");
        var grid = new Grid
        {
            Height = 26,
            ColumnDefinitions =
            {
                new ColumnDefinition { Width = new GridLength(8 + (row.Depth * 20)) },
                new ColumnDefinition { Width = new GridLength(20) },
                new ColumnDefinition { Width = new GridLength(28) },
                new ColumnDefinition(),
                new ColumnDefinition { Width = GridLength.Auto },
            },
        };

        FontIcon? chevronIcon = null;
        if (row.CanExpand)
        {
            chevronIcon = new FontIcon { FontSize = 9 };
            var chevron = IconButton(18, chevronIcon);
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
                check.IsChecked = row.IsChecked;
                row.ToggleCommand.Execute(null);
            };
            Grid.SetColumn(check, 2);
            grid.Children.Add(check);
            box = check;
        }

        var text = new TextBlock { Text = row.Text, VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis };
        if (row.IsStrong)
            text.FontWeight = Microsoft.UI.Text.FontWeights.SemiBold;
        var content = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, VerticalAlignment = VerticalAlignment.Center, Children = { text } };
        if (row.Note is { } note)
            content.Children.Add(new TextBlock { Text = note, Foreground = muted, VerticalAlignment = VerticalAlignment.Center });

        TextBox? entry = null;
        HyperlinkButton? add = null;
        if (row.CanAddPattern)
        {
            var link = new HyperlinkButton { Content = new TextBlock { Text = UiText.Get("explorer.filter.addPattern"), FontSize = 11, Foreground = muted }, Padding = new Thickness(4, 0, 4, 0), MinHeight = 0, Height = 22 };
            link.Click += (_, _) => row.StartPatternCommand.Execute(null);
            var field = new TextBox { PlaceholderText = UiText.Get("explorer.filter.patternHint"), Width = 190, Height = 24, MinHeight = 24, Padding = new Thickness(6, 2, 6, 2), Visibility = Visibility.Collapsed };
            AutomationProperties.SetName(field, UiText.Get("explorer.filter.addPattern"));
            field.TextChanged += (_, _) => row.PatternText = field.Text;
            // Tunnelling, so Escape cancels the entry without the flyout seeing it.
            field.PreviewKeyDown += (_, e) =>
            {
                if (e.Key == VirtualKey.Enter)
                {
                    row.CommitPatternCommand.Execute(null);
                    e.Handled = true;
                }
                else if (e.Key == VirtualKey.Escape)
                {
                    row.CancelPatternCommand.Execute(null);
                    e.Handled = true;
                }
            };
            content.Children.Add(link);
            content.Children.Add(field);
            add = link;
            entry = field;
        }

        Grid.SetColumn(content, 3);
        grid.Children.Add(content);
        if (row.Kind == FilterRowKind.Pattern)
        {
            var remove = IconButton(22, new FontIcon { Glyph = "", FontSize = 10 });
            remove.Margin = new Thickness(0, 0, 8, 0);
            ToolTipService.SetToolTip(remove, UiText.Get("explorer.filter.removePattern"));
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
                chevronIcon.Glyph = row.IsExpanded ? "" : "";
            if (entry is not null && add is not null && (property is null or nameof(FilterRow.IsAddingPattern)))
            {
                entry.Visibility = row.IsAddingPattern ? Visibility.Visible : Visibility.Collapsed;
                add.Visibility = row.IsAddingPattern ? Visibility.Collapsed : Visibility.Visible;
                if (row.IsAddingPattern)
                {
                    entry.Text = "";
                    entry.Focus(FocusState.Programmatic);
                }
            }
        }

        Sync(null);
        row.PropertyChanged += (_, e) => Sync(e.PropertyName);
        list.Children.Add(grid);
        if (row is { Kind: FilterRowKind.All, Depth: 0 })
            list.Children.Add(new Border { Height = 1, Background = Tokens.Brush(owner, "SquillBorderBrush"), Margin = new Thickness(8, 2, 8, 2) });
    }
}
