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
    private const double MenuWidth = 320;
    private const double MenuHeight = 400;
    private Flyout? _flyout;
    private DataGridCanvas? _grid;
    private ColumnMenuViewModel? _vm;

    /// <summary>Closes the menu when it is open.</summary>
    public void Hide() => _flyout?.Hide();

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

    private static Grid Build(FrameworkElement owner, ColumnMenuViewModel vm, bool hasFilter, Flyout flyout)
    {
        var canChange = vm.CanSort;
        var muted = Brush(owner, "SquillFg3Brush");
        var title = new TextBlock { Text = UiText.Format("results.columnMenu.title", vm.ColumnName), FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, TextTrimming = TextTrimming.CharacterEllipsis, VerticalAlignment = VerticalAlignment.Center };
        var copy = IconButton(owner, "SquillIconCopy", UiText.Get("results.columnMenu.copyName"), false, true, null);
        copy.Click += (_, _) =>
        {
            var package = new DataPackage();
            package.SetText(vm.ColumnName);
            Clipboard.SetContent(package);
        };
        Grid.SetColumn(copy, 1);
        var titleRow = new Grid { ColumnDefinitions = { new ColumnDefinition(), new ColumnDefinition { Width = GridLength.Auto } }, Children = { title, copy } };

        var search = new TextBox { PlaceholderText = UiText.Get("results.columnMenu.searchValue"), Height = 32, MinHeight = 32, Text = vm.ValueSearch, IsEnabled = canChange };
        AutomationProperties.SetName(search, UiText.Get("results.columnMenu.searchValue"));
        var sortAsc = IconButton(owner, "SquillIconSortAsc", UiText.Get("results.columnMenu.sortAsc"), vm.CurrentSort is { Descending: false }, canChange, vm.BlockedReason);
        var sortDesc = IconButton(owner, "SquillIconSortDesc", UiText.Get("results.columnMenu.sortDesc"), vm.CurrentSort is { Descending: true }, canChange, vm.BlockedReason);
        sortAsc.Click += (_, _) =>
        {
            flyout.Hide();
            vm.SortAscendingCommand.Execute(null);
        };
        sortDesc.Click += (_, _) =>
        {
            flyout.Hide();
            vm.SortDescendingCommand.Execute(null);
        };
        Grid.SetColumn(sortAsc, 1);
        Grid.SetColumn(sortDesc, 2);
        var searchRow = new Grid
        {
            ColumnDefinitions = { new ColumnDefinition(), new ColumnDefinition { Width = GridLength.Auto }, new ColumnDefinition { Width = GridLength.Auto } },
            ColumnSpacing = 4,
            Children = { search, sortAsc, sortDesc },
        };

        // Plain check boxes in a stack panel: no ItemsSource marshalling, no containers to recycle (the list is capped at 500 values).
        var list = new StackPanel();
        var scroller = new ScrollViewer { Padding = new Thickness(0, 0, 12, 0), Content = list, IsEnabled = canChange, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        var all = new CheckBox { MinWidth = 0, Width = 24, Height = 28, IsTabStop = true, VerticalAlignment = VerticalAlignment.Center };
        AutomationProperties.SetName(all, UiText.Get("results.columnMenu.selectAll"));
        var valueHeader = new TextBlock { Text = UiText.Get("results.columnMenu.valueHeader"), Foreground = muted, VerticalAlignment = VerticalAlignment.Center };
        var countHeader = new TextBlock { Text = UiText.Get("results.columnMenu.countHeader"), Foreground = muted, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 12, 0) };
        Grid.SetColumn(valueHeader, 1);
        Grid.SetColumn(countHeader, 2);
        var headerRow = new Grid { ColumnDefinitions = { new ColumnDefinition { Width = GridLength.Auto }, new ColumnDefinition(), new ColumnDefinition { Width = GridLength.Auto } }, Children = { all, valueHeader, countHeader }, Padding = new Thickness(8, 0, 0, 0) };
        var headerBox = new Border { BorderBrush = Brush(owner, "SquillBorderSubtleBrush"), BorderThickness = new Thickness(0, 0, 0, 1), Child = headerRow };
        var loading = new ProgressBar { IsIndeterminate = true, VerticalAlignment = VerticalAlignment.Bottom, Visibility = vm.IsLoadingValues ? Visibility.Visible : Visibility.Collapsed };
        var hint = new TextBlock { Foreground = muted, FontSize = 11, TextTrimming = TextTrimming.CharacterEllipsis, VerticalAlignment = VerticalAlignment.Center };
        var apply = new Button { Style = Res("PrimaryButtonStyle"), Content = UiText.Get("results.filter.editor.apply"), Height = 28, MinHeight = 28, IsEnabled = canChange && !vm.IsLoadingValues };
        ToolTipService.SetToolTip(apply, vm.BlockedReason);
        var clear = new Button { Content = UiText.Get("results.columnMenu.clearFilter"), Height = 28, MinHeight = 28, HorizontalAlignment = HorizontalAlignment.Left, Visibility = hasFilter ? Visibility.Visible : Visibility.Collapsed, IsEnabled = canChange };
        Grid.SetColumn(apply, 1);
        var footer = new Grid { ColumnDefinitions = { new ColumnDefinition(), new ColumnDefinition { Width = GridLength.Auto } }, Children = { clear, apply } };

        void SyncAll()
        {
            var checkedCount = vm.Values.Count(v => v.IsChecked);
            all.IsChecked = checkedCount == 0 ? false : checkedCount == vm.Values.Count ? true : null;
        }

        void Refresh(string? property)
        {
            if (property is nameof(ColumnMenuViewModel.Values))
                Fill(list, owner, vm.Values, SyncAll);
            SyncAll();
            loading.Visibility = vm.IsLoadingValues ? Visibility.Visible : Visibility.Collapsed;
            hint.Text = vm.ValuesHint ?? UiText.Get("results.columnMenu.hint");
            apply.IsEnabled = canChange && !vm.IsLoadingValues;
        }

        Refresh(nameof(ColumnMenuViewModel.Values));
        vm.PropertyChanged += (_, e) => Refresh(e.PropertyName);
        search.TextChanged += (_, _) => vm.ValueSearch = search.Text;
        all.Click += (_, _) =>
        {
            var target = vm.Values.Any(v => !v.IsChecked);
            foreach (var item in vm.Values)
                item.IsChecked = target;
            Fill(list, owner, vm.Values, SyncAll);
            SyncAll();
        };
        apply.Click += (_, _) =>
        {
            flyout.Hide();
            vm.ApplyValuesCommand.Execute(null);
        };
        clear.Click += (_, _) =>
        {
            flyout.Hide();
            vm.ClearColumnFilterCommand.Execute(null);
        };

        // The popup keeps one size whatever the list holds: only the list scrolls.
        var panel = new Grid
        {
            Width = MenuWidth,
            Height = MenuHeight,
            RowSpacing = 8,
            RowDefinitions =
            {
                new RowDefinition { Height = GridLength.Auto },
                new RowDefinition { Height = GridLength.Auto },
                new RowDefinition { Height = GridLength.Auto },
                new RowDefinition { Height = new GridLength(1, GridUnitType.Star) },
                new RowDefinition { Height = GridLength.Auto },
                new RowDefinition { Height = GridLength.Auto },
            },
        };
        Place(panel, titleRow, 0);
        Place(panel, searchRow, 1);
        Place(panel, headerBox, 2);
        Place(panel, scroller, 3);
        Place(panel, new Grid { Height = 18, Children = { hint, loading } }, 4);
        Place(panel, footer, 5);

        // Tunnelling, so Enter applies before a focused check box sees it; Space toggles a focused check box natively.
        panel.PreviewKeyDown += (_, e) => OnKey(e, vm, apply, flyout);
        return panel;
    }

    private static void Place(Grid grid, FrameworkElement child, int row)
    {
        Grid.SetRow(child, row);
        grid.Children.Add(child);
    }

    private static Button IconButton(FrameworkElement owner, string icon, string name, bool active, bool enabled, string? blocked)
    {
        var button = new Button
        {
            Content = Icons.Create(icon, 14, Brush(owner, active ? "SquillAccentTextBrush" : "SquillFg3Brush")),
            Width = 32,
            Height = 32,
            MinWidth = 0,
            MinHeight = 0,
            Padding = new Thickness(0),
            Background = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Transparent),
            BorderThickness = new Thickness(0),
            IsEnabled = enabled,
        };
        AutomationProperties.SetName(button, name);
        ToolTipService.SetToolTip(button, enabled ? name : blocked ?? name);
        return button;
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

    private static void Fill(StackPanel list, FrameworkElement owner, IReadOnlyList<ValueItem> values, Action changed)
    {
        list.Children.Clear();
        foreach (var item in values)
            list.Children.Add(BuildValueRow(owner, item, changed));
    }

    private static CheckBox BuildValueRow(FrameworkElement owner, ValueItem item, Action changed)
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
        box.Click += (_, _) =>
        {
            item.IsChecked = box.IsChecked == true;
            changed();
        };
        return box;
    }
}
