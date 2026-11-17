using System.ComponentModel;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using Squill.App.WinUI.Controls;
using Squill.Presentation.Localization;
using Squill.Presentation.Results;
using Windows.ApplicationModel.DataTransfer;
using Windows.Foundation;
using Windows.System;

namespace Squill.App.WinUI.Views;

/// <summary>The flyout of a grid column (M05): sort, filter by value, clear, copy name. It only draws <see cref="ColumnMenuViewModel"/>.</summary>
internal sealed class ColumnMenu
{
    private const double MenuWidth = 300;
    private Flyout? _flyout;
    private DataGridCanvas? _grid;
    private ColumnMenuViewModel? _vm;

    /// <summary>Shows the menu of a column under its header.</summary>
    /// <param name="grid">The grid whose header is the anchor.</param>
    /// <param name="vm">The menu state; disposed when the flyout closes.</param>
    /// <param name="hasFilter">Whether the column carries a filter (shows the clear entry).</param>
    /// <param name="anchor">The anchor point in the grid's coordinates.</param>
    public void Show(DataGridCanvas grid, ColumnMenuViewModel vm, bool hasFilter, Point anchor)
    {
        _vm?.Dispose();
        _vm = null;
        if (grid.XamlRoot is null)
        {
            vm.Dispose();
            return;
        }

        _grid = grid;
        _vm = vm;
        grid.MenuColumn = vm.Column;
        // A fresh flyout per show: nothing of a previous menu (content, handlers, placement target) can leak into this one.
        var flyout = new Flyout { Placement = FlyoutPlacementMode.BottomEdgeAlignedLeft };
        flyout.Closed += OnClosed;
        _flyout = flyout;
        flyout.Content = Build(grid, vm, hasFilter, flyout);
        flyout.ShowAt(grid, new FlyoutShowOptions { Position = anchor, Placement = FlyoutPlacementMode.BottomEdgeAlignedLeft });
    }

    private static Microsoft.UI.Xaml.Media.Brush Brush(FrameworkElement owner, string key) => Tokens.Brush(owner, key);

    private static Style Res(string key) => (Style)Application.Current.Resources[key];

    private void OnClosed(object? sender, object e)
    {
        if (sender is Flyout closed)
        {
            closed.Closed -= OnClosed;
            closed.Content = null;
            if (!ReferenceEquals(closed, _flyout))
                return;
        }

        _vm?.Dispose();
        _vm = null;
        if (_grid is { } grid)
        {
            grid.MenuColumn = -1;
            grid.FocusSurface();
        }
    }

    private static Button Entry(Flyout flyout, FrameworkElement owner, string icon, string text, bool isChecked, bool enabled, string? blocked, Action run)
    {
        var glyph = Icons.Create(icon, 14, Brush(owner, isChecked ? "SquillAccentTextBrush" : "SquillFg3Brush"));
        glyph.HorizontalAlignment = HorizontalAlignment.Left;
        var row = new Grid { ColumnDefinitions = { new ColumnDefinition { Width = new GridLength(26) }, new ColumnDefinition(), new ColumnDefinition { Width = GridLength.Auto } } };
        row.Children.Add(glyph);
        var label = new TextBlock { Text = text, VerticalAlignment = VerticalAlignment.Center };
        Grid.SetColumn(label, 1);
        row.Children.Add(label);
        if (isChecked)
        {
            var tick = Icons.Create("SquillIconCheck", 12, Brush(owner, "SquillAccentTextBrush"));
            Grid.SetColumn(tick, 2);
            row.Children.Add(tick);
        }

        var button = new Button
        {
            Content = row,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Left,
            Background = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Transparent),
            BorderThickness = new Thickness(0),
            Height = 32,
            MinHeight = 32,
            Padding = new Thickness(10, 0, 10, 0),
            IsEnabled = enabled,
        };
        AutomationProperties.SetName(button, text);
        if (!enabled)
            ToolTipService.SetToolTip(button, blocked);
        button.Click += (_, _) =>
        {
            flyout.Hide();
            run();
        };
        return button;
    }

    private static Border Separator(FrameworkElement owner) => new() { Height = 1, Margin = new Thickness(0, 4, 0, 4), Background = Brush(owner, "SquillBorderSubtleBrush") };

    private static StackPanel Build(FrameworkElement owner, ColumnMenuViewModel vm, bool hasFilter, Flyout flyout)
    {
        var canChange = vm.CanSort;
        var sortAsc = Entry(flyout, owner, "SquillIconSortAsc", UiText.Get("results.columnMenu.sortAsc"), vm.CurrentSort is { Descending: false }, canChange, vm.BlockedReason, () => vm.SortAscendingCommand.Execute(null));
        var sortDesc = Entry(flyout, owner, "SquillIconSortDesc", UiText.Get("results.columnMenu.sortDesc"), vm.CurrentSort is { Descending: true }, canChange, vm.BlockedReason, () => vm.SortDescendingCommand.Execute(null));
        var search = new TextBox { PlaceholderText = UiText.Get("results.columnMenu.searchValue"), Height = 28, MinHeight = 28, Margin = new Thickness(10, 0, 10, 4), Text = vm.ValueSearch, IsEnabled = canChange };
        AutomationProperties.SetName(search, UiText.Get("results.columnMenu.searchValue"));
        // Plain check boxes in a stack panel: no ItemsSource marshalling, no containers to recycle (the list is capped at 500 values).
        var list = new StackPanel();
        var scroller = new ScrollViewer { MaxHeight = 190, Padding = new Thickness(0, 0, 12, 0), Content = list, IsEnabled = canChange, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        var loading = new ProgressBar { IsIndeterminate = true, Visibility = vm.IsLoadingValues ? Visibility.Visible : Visibility.Collapsed };
        var hint = new TextBlock { Foreground = Brush(owner, "SquillFg3Brush"), FontSize = 11, Text = vm.ValuesHint ?? "", Visibility = vm.ValuesHint is null ? Visibility.Collapsed : Visibility.Visible };
        var apply = new Button { Style = Res("PrimaryButtonStyle"), Content = UiText.Get("results.filter.editor.apply"), Height = 28, MinHeight = 28, HorizontalAlignment = HorizontalAlignment.Right, IsEnabled = canChange && !vm.IsLoadingValues };
        ToolTipService.SetToolTip(apply, vm.BlockedReason);

        Fill(list, owner, vm.Values);

        void Refresh(string? property)
        {
            if (property is nameof(ColumnMenuViewModel.Values))
                Fill(list, owner, vm.Values);
            loading.Visibility = vm.IsLoadingValues ? Visibility.Visible : Visibility.Collapsed;
            hint.Text = vm.ValuesHint ?? "";
            hint.Visibility = vm.ValuesHint is null ? Visibility.Collapsed : Visibility.Visible;
            apply.IsEnabled = canChange && !vm.IsLoadingValues;
        }

        vm.PropertyChanged += (_, e) => Refresh(e.PropertyName);
        search.TextChanged += (_, _) => vm.ValueSearch = search.Text;
        apply.Click += (_, _) =>
        {
            flyout.Hide();
            vm.ApplyValuesCommand.Execute(null);
        };

        var panel = new StackPanel { Width = MenuWidth, Spacing = 2 };
        panel.Children.Add(sortAsc);
        panel.Children.Add(sortDesc);
        panel.Children.Add(Separator(owner));
        panel.Children.Add(new TextBlock { Text = UiText.Get("results.columnMenu.filterByValue"), Style = Res("CapsTextBlockStyle"), Margin = new Thickness(10, 6, 0, 4) });
        panel.Children.Add(search);
        panel.Children.Add(loading);
        panel.Children.Add(scroller);
        panel.Children.Add(hint);
        panel.Children.Add(new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 6, 10, 2), Children = { apply } });
        panel.Children.Add(Separator(owner));
        if (hasFilter)
            panel.Children.Add(Entry(flyout, owner, "SquillIconClose", UiText.Get("results.columnMenu.clearFilter"), false, canChange, vm.BlockedReason, () => vm.ClearColumnFilterCommand.Execute(null)));
        // No Ctrl+C chip: Ctrl+C in the grid copies the selection, not the column name.
        panel.Children.Add(Entry(flyout, owner, "SquillIconCopy", UiText.Get("results.columnMenu.copyName"), false, true, null, () =>
        {
            var package = new DataPackage();
            package.SetText(vm.ColumnName);
            Clipboard.SetContent(package);
        }));

        // Tunnelling, so Enter applies before a focused check box sees it; Space toggles a focused check box natively.
        panel.PreviewKeyDown += (_, e) => OnKey(e, vm, apply, flyout);
        return panel;
    }

    private static void OnKey(KeyRoutedEventArgs e, ColumnMenuViewModel vm, Button apply, Flyout flyout)
    {
        if (e.Key == VirtualKey.Escape)
        {
            flyout.Hide();
            e.Handled = true;
        }
        else if (e.Key == VirtualKey.Enter && apply.IsEnabled)
        {
            flyout.Hide();
            vm.ApplyValuesCommand.Execute(null);
            e.Handled = true;
        }
    }

    private static void Fill(StackPanel list, FrameworkElement owner, IReadOnlyList<ValueItem> values)
    {
        list.Children.Clear();
        foreach (var item in values)
            list.Children.Add(BuildValueRow(owner, item));
    }

    private static CheckBox BuildValueRow(FrameworkElement owner, ValueItem item)
    {
        var muted = Brush(owner, "SquillFg3Brush");
        var text = new TextBlock { Text = item.Text, TextTrimming = TextTrimming.CharacterEllipsis, VerticalAlignment = VerticalAlignment.Center };
        var count = new TextBlock { Text = item.Count.ToString(System.Globalization.CultureInfo.CurrentCulture), Foreground = muted, FontSize = 12, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(12, 0, 0, 0) };
        if (item.Value is null)
        {
            text.FontStyle = Windows.UI.Text.FontStyle.Italic;
            text.Foreground = muted;
        }

        Grid.SetColumn(count, 1);
        var box = new CheckBox
        {
            IsChecked = item.IsChecked,
            Height = 28,
            MinHeight = 28,
            Padding = new Thickness(8, 0, 4, 0),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            Content = new Grid { ColumnDefinitions = { new ColumnDefinition(), new ColumnDefinition { Width = GridLength.Auto } }, Children = { text, count } },
        };
        AutomationProperties.SetName(box, item.Text);
        box.Click += (_, _) => item.IsChecked = box.IsChecked == true;
        return box;
    }
}
