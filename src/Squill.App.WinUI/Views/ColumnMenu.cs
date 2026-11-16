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
    private readonly Flyout _flyout = new() { Placement = FlyoutPlacementMode.BottomEdgeAlignedLeft };
    private DataGridCanvas? _grid;
    private ColumnMenuViewModel? _vm;

    public ColumnMenu() => _flyout.Closed += OnClosed;

    /// <summary>Shows the menu of a column under its header.</summary>
    /// <param name="grid">The grid whose header is the anchor.</param>
    /// <param name="vm">The menu state; disposed when the flyout closes.</param>
    /// <param name="hasFilter">Whether the column carries a filter (shows the clear entry).</param>
    /// <param name="anchor">The anchor point in the grid's coordinates.</param>
    public void Show(DataGridCanvas grid, ColumnMenuViewModel vm, bool hasFilter, Point anchor)
    {
        _vm?.Dispose();
        _grid = grid;
        _vm = vm;
        grid.MenuColumn = vm.Column;
        _flyout.Content = Build(grid, vm, hasFilter);
        _flyout.ShowAt(grid, new FlyoutShowOptions { Position = anchor, Placement = FlyoutPlacementMode.BottomEdgeAlignedLeft });
    }

    private static Microsoft.UI.Xaml.Media.Brush Brush(FrameworkElement owner, string key) => Tokens.Brush(owner, key);

    private static Style Res(string key) => (Style)Application.Current.Resources[key];

    private void OnClosed(object? sender, object e)
    {
        _vm?.Dispose();
        _vm = null;
        _flyout.Content = null;
        if (_grid is { } grid)
        {
            grid.MenuColumn = -1;
            grid.FocusSurface();
        }
    }

    private Button Entry(FrameworkElement owner, string text, bool isChecked, bool enabled, string? blocked, Action run)
    {
        var tick = Icons.Create("SquillIconCheck", 12, Brush(owner, "SquillAccentTextBrush"));
        tick.Visibility = isChecked ? Visibility.Visible : Visibility.Collapsed;
        var row = new Grid { ColumnDefinitions = { new ColumnDefinition { Width = new GridLength(18) }, new ColumnDefinition() } };
        row.Children.Add(tick);
        var label = new TextBlock { Text = text, VerticalAlignment = VerticalAlignment.Center };
        Grid.SetColumn(label, 1);
        row.Children.Add(label);
        var button = new Button
        {
            Content = row,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Left,
            Background = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Transparent),
            BorderThickness = new Thickness(0),
            Height = 30,
            MinHeight = 30,
            IsEnabled = enabled,
        };
        AutomationProperties.SetName(button, text);
        if (!enabled)
            ToolTipService.SetToolTip(button, blocked);
        button.Click += (_, _) =>
        {
            _flyout.Hide();
            run();
        };
        return button;
    }

    private static Border Separator(FrameworkElement owner) => new() { Height = 1, Margin = new Thickness(0, 4, 0, 4), Background = Brush(owner, "SquillBorderSubtleBrush") };

    private StackPanel Build(FrameworkElement owner, ColumnMenuViewModel vm, bool hasFilter)
    {
        var canChange = vm.CanSort;
        var sortAsc = Entry(owner, UiText.Get("results.columnMenu.sortAsc"), vm.CurrentSort is { Descending: false }, canChange, vm.BlockedReason, () => vm.SortAscendingCommand.Execute(null));
        var sortDesc = Entry(owner, UiText.Get("results.columnMenu.sortDesc"), vm.CurrentSort is { Descending: true }, canChange, vm.BlockedReason, () => vm.SortDescendingCommand.Execute(null));
        var search = new TextBox { PlaceholderText = UiText.Get("results.columnMenu.searchValue"), Height = 28, MinHeight = 28, Text = vm.ValueSearch, IsEnabled = canChange };
        AutomationProperties.SetName(search, UiText.Get("results.columnMenu.searchValue"));
        var list = new ListView { SelectionMode = ListViewSelectionMode.None, IsItemClickEnabled = true, MaxHeight = 240, ItemsSource = vm.Values, IsEnabled = canChange };
        var loading = new ProgressBar { IsIndeterminate = true, Visibility = vm.IsLoadingValues ? Visibility.Visible : Visibility.Collapsed };
        var hint = new TextBlock { Foreground = Brush(owner, "SquillFg3Brush"), FontSize = 11, Text = vm.ValuesHint ?? "", Visibility = vm.ValuesHint is null ? Visibility.Collapsed : Visibility.Visible };
        var apply = new Button { Style = Res("PrimaryButtonStyle"), Content = UiText.Get("results.filter.editor.apply"), Height = 28, MinHeight = 28, HorizontalAlignment = HorizontalAlignment.Right, IsEnabled = canChange && !vm.IsLoadingValues };
        ToolTipService.SetToolTip(apply, vm.BlockedReason);

        list.ContainerContentChanging += (_, a) =>
        {
            if (a.InRecycleQueue || a.Item is not ValueItem item)
                return;
            var container = (ListViewItem)a.ItemContainer;
            container.MinHeight = 28;
            container.Padding = new Thickness(6, 0, 6, 0);
            container.Content = BuildValueRow(owner, item);
        };
        list.ItemClick += (_, e) =>
        {
            if (e.ClickedItem is ValueItem item)
                Toggle(list, item);
        };

        void Refresh(string? property)
        {
            if (property is nameof(ColumnMenuViewModel.Values))
                list.ItemsSource = vm.Values;
            loading.Visibility = vm.IsLoadingValues ? Visibility.Visible : Visibility.Collapsed;
            hint.Text = vm.ValuesHint ?? "";
            hint.Visibility = vm.ValuesHint is null ? Visibility.Collapsed : Visibility.Visible;
            apply.IsEnabled = canChange && !vm.IsLoadingValues;
        }

        vm.PropertyChanged += (_, e) => Refresh(e.PropertyName);
        search.TextChanged += (_, _) => vm.ValueSearch = search.Text;
        apply.Click += (_, _) =>
        {
            _flyout.Hide();
            vm.ApplyValuesCommand.Execute(null);
        };

        var panel = new StackPanel { Width = MenuWidth, Spacing = 2 };
        panel.Children.Add(sortAsc);
        panel.Children.Add(sortDesc);
        panel.Children.Add(Separator(owner));
        panel.Children.Add(new TextBlock { Text = UiText.Get("results.columnMenu.filterByValue"), Style = Res("CapsTextBlockStyle"), Margin = new Thickness(8, 4, 0, 4) });
        panel.Children.Add(search);
        panel.Children.Add(loading);
        panel.Children.Add(list);
        panel.Children.Add(hint);
        panel.Children.Add(new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 4, 0, 0), Children = { apply } });
        panel.Children.Add(Separator(owner));
        if (hasFilter)
            panel.Children.Add(Entry(owner, UiText.Get("results.columnMenu.clearFilter"), false, canChange, vm.BlockedReason, () => vm.ClearColumnFilterCommand.Execute(null)));
        // No Ctrl+C chip: Ctrl+C in the grid copies the selection, not the column name.
        panel.Children.Add(Entry(owner, UiText.Get("results.columnMenu.copyName"), false, true, null, () =>
        {
            var package = new DataPackage();
            package.SetText(vm.ColumnName);
            Clipboard.SetContent(package);
        }));

        // Tunnelling, so Enter applies and Space checks before the list items turn them into a click.
        panel.PreviewKeyDown += (_, e) => OnKey(e, vm, list, apply);
        return panel;
    }

    private void OnKey(KeyRoutedEventArgs e, ColumnMenuViewModel vm, ListView list, Button apply)
    {
        if (e.Key == VirtualKey.Escape)
        {
            _flyout.Hide();
            e.Handled = true;
        }
        else if (e.Key == VirtualKey.Enter && apply.IsEnabled)
        {
            _flyout.Hide();
            vm.ApplyValuesCommand.Execute(null);
            e.Handled = true;
        }
        else if (e.Key == VirtualKey.Space && FocusManager.GetFocusedElement(list.XamlRoot) is ListViewItem { Content: FrameworkElement { Tag: ValueItem item } })
        {
            Toggle(list, item);
            e.Handled = true;
        }
    }

    private static void Toggle(ListView list, ValueItem item)
    {
        item.IsChecked = !item.IsChecked;
        if (list.ContainerFromItem(item) is ListViewItem { Content: Grid { Children: [CheckBox box, ..] } })
            box.IsChecked = item.IsChecked;
    }

    private static Grid BuildValueRow(FrameworkElement owner, ValueItem item)
    {
        var muted = Brush(owner, "SquillFg3Brush");
        var isNull = item.Value is null;
        var box = new CheckBox { IsChecked = item.IsChecked, MinWidth = 0, Width = 24, IsTabStop = false, IsHitTestVisible = false, VerticalAlignment = VerticalAlignment.Center };
        var text = new TextBlock { Text = item.Text, TextTrimming = TextTrimming.CharacterEllipsis, VerticalAlignment = VerticalAlignment.Center };
        var count = new TextBlock { Text = item.Count.ToString(System.Globalization.CultureInfo.CurrentCulture), Foreground = muted, FontSize = 12, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(8, 0, 0, 0) };
        if (isNull)
        {
            text.FontStyle = Windows.UI.Text.FontStyle.Italic;
            text.Foreground = muted;
        }

        Grid.SetColumn(text, 1);
        Grid.SetColumn(count, 2);
        return new Grid
        {
            Tag = item,
            ColumnDefinitions = { new ColumnDefinition { Width = GridLength.Auto }, new ColumnDefinition(), new ColumnDefinition { Width = GridLength.Auto } },
            Children = { box, text, count },
        };
    }
}
