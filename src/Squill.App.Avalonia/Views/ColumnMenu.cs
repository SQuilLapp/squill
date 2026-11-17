using System.ComponentModel;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Squill.App.Avalonia.Controls;
using Squill.Presentation.Localization;
using Squill.Presentation.Results;
using AvaloniaPath = Avalonia.Controls.Shapes.Path;

namespace Squill.App.Avalonia.Views;

/// <summary>The flyout of a grid column (M05): sort, filter by value, clear, copy name. It only draws <see cref="ColumnMenuViewModel"/>.</summary>
internal sealed class ColumnMenu
{
    private const double MenuWidth = 320;
    private const double MenuHeight = 400;
    private readonly Flyout _flyout = new() { Placement = PlacementMode.BottomEdgeAlignedLeft };
    private DataGridView? _grid;
    private ColumnMenuViewModel? _vm;

    /// <summary>Creates the menu; it stays hidden until <see cref="Show"/>.</summary>
    public ColumnMenu() => _flyout.Closed += OnClosed;

    /// <summary>Gets the content of the open menu, or <see langword="null"/> when it is closed.</summary>
    public Control? Content => _flyout.Content as Control;

    /// <summary>Shows the menu of a column under its header.</summary>
    /// <param name="grid">The grid whose header is the anchor.</param>
    /// <param name="vm">The menu state; disposed when the flyout closes.</param>
    /// <param name="hasFilter">Whether the column carries a filter (shows the clear entry).</param>
    /// <param name="anchor">The anchor point in the grid canvas' coordinates.</param>
    public void Show(DataGridView grid, ColumnMenuViewModel vm, bool hasFilter, Point anchor)
    {
        _vm?.Dispose();
        _grid = grid;
        _vm = vm;
        grid.Canvas.MenuColumn = vm.Column;
        _flyout.Content = Build(grid, vm, hasFilter);
        _flyout.ShowAt(grid.AnchorAt(anchor));
    }

    private static IBrush Brush(Control owner, string key) =>
        owner.TryFindResource(key, owner.ActualThemeVariant, out var value) && value is IBrush brush ? brush : Brushes.Gray;

    private void OnClosed(object? sender, EventArgs e)
    {
        _vm?.Dispose();
        _vm = null;
        _flyout.Content = null;
        if (_grid is { } grid)
        {
            grid.Canvas.MenuColumn = -1;
            grid.Canvas.Focus();
        }
    }

    private static Button IconButton(Control owner, string icon, string name, bool active, bool enabled, string? blocked)
    {
        var path = new AvaloniaPath { Width = 14, Height = 14, Stretch = Stretch.Uniform };
        path.Classes.Add("icon");
        path.Bind(AvaloniaPath.DataProperty, owner.GetResourceObservable(icon));
        if (active)
        {
            path.Stroke = Brush(owner, "SquillAccentTextBrush");
        }

        var button = new Button
        {
            Content = path,
            Width = 32,
            Height = 32,
            MinWidth = 0,
            MinHeight = 0,
            Padding = new Thickness(0),
            HorizontalContentAlignment = HorizontalAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Center,
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            IsEnabled = enabled,
        };
        AutomationProperties.SetName(button, name);
        ToolTip.SetShowOnDisabled(button, true);
        ToolTip.SetTip(button, enabled ? name : blocked ?? name);
        return button;
    }

    private static void Place(Grid grid, Control child, int row)
    {
        Grid.SetRow(child, row);
        grid.Children.Add(child);
    }

    private Grid Build(Control owner, ColumnMenuViewModel vm, bool hasFilter)
    {
        var canChange = vm.CanSort;
        var muted = Brush(owner, "SquillFg3Brush");
        var title = new TextBlock { Text = UiText.Format("results.columnMenu.title", vm.ColumnName), FontWeight = FontWeight.SemiBold, TextTrimming = TextTrimming.CharacterEllipsis, VerticalAlignment = VerticalAlignment.Center };
        var copy = IconButton(owner, "SquillIconCopy", UiText.Get("results.columnMenu.copyName"), false, true, null);
        // No Ctrl+C chip: Ctrl+C in the grid copies the selection, not the column name.
        copy.Click += (_, _) =>
        {
            if (TopLevel.GetTopLevel(owner)?.Clipboard is { } clipboard)
            {
                _ = clipboard.SetTextAsync(vm.ColumnName);
            }
        };
        Grid.SetColumn(copy, 1);
        var titleRow = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), Children = { title, copy } };

        var search = new TextBox { Name = "ValueSearch", PlaceholderText = UiText.Get("results.columnMenu.searchValue"), Height = 32, MinHeight = 32, Text = vm.ValueSearch, IsEnabled = canChange };
        AutomationProperties.SetName(search, UiText.Get("results.columnMenu.searchValue"));
        var sortAsc = IconButton(owner, "SquillIconSortAsc", UiText.Get("results.columnMenu.sortAsc"), vm.CurrentSort is { Descending: false }, canChange, vm.BlockedReason);
        var sortDesc = IconButton(owner, "SquillIconSortDesc", UiText.Get("results.columnMenu.sortDesc"), vm.CurrentSort is { Descending: true }, canChange, vm.BlockedReason);
        sortAsc.Click += (_, _) =>
        {
            _flyout.Hide();
            vm.SortAscendingCommand.Execute(null);
        };
        sortDesc.Click += (_, _) =>
        {
            _flyout.Hide();
            vm.SortDescendingCommand.Execute(null);
        };
        Grid.SetColumn(sortAsc, 1);
        Grid.SetColumn(sortDesc, 2);
        var searchRow = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto,Auto"), ColumnSpacing = 4, Children = { search, sortAsc, sortDesc } };

        var all = new CheckBox { Name = "SelectAll", IsThreeState = false, MinWidth = 0, Width = 24, VerticalAlignment = VerticalAlignment.Center };
        AutomationProperties.SetName(all, UiText.Get("results.columnMenu.selectAll"));
        var valueHeader = new TextBlock { Text = UiText.Get("results.columnMenu.valueHeader"), Foreground = muted, VerticalAlignment = VerticalAlignment.Center };
        var countHeader = new TextBlock { Text = UiText.Get("results.columnMenu.countHeader"), Foreground = muted, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 12, 0) };
        Grid.SetColumn(valueHeader, 1);
        Grid.SetColumn(countHeader, 2);
        var headerRow = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto"), Margin = new Thickness(8, 0, 0, 0), Children = { all, valueHeader, countHeader } };
        var headerBox = new Border { BorderBrush = Brush(owner, "SquillBorderSubtleBrush"), BorderThickness = new Thickness(0, 0, 0, 1), Child = headerRow };

        var list = new ListBox { Name = "Values", ItemsSource = vm.Values, IsEnabled = canChange, SelectionMode = SelectionMode.Single, Padding = new Thickness(0, 0, 12, 0) };
        list.ItemTemplate = new FuncDataTemplate<ValueItem>((item, _) => item is null ? null : BuildValueRow(owner, item, SyncAll));
        var loading = new ProgressBar { IsIndeterminate = true, VerticalAlignment = VerticalAlignment.Bottom, IsVisible = vm.IsLoadingValues };
        var hint = new TextBlock { FontSize = 11, TextTrimming = TextTrimming.CharacterEllipsis, VerticalAlignment = VerticalAlignment.Center, Foreground = muted };
        var apply = new Button { Name = "Apply", Content = UiText.Get("results.filter.editor.apply"), Height = 28, MinHeight = 28, IsEnabled = canChange && !vm.IsLoadingValues };
        apply.Classes.Add("primary");
        ToolTip.SetShowOnDisabled(apply, true);
        ToolTip.SetTip(apply, vm.BlockedReason);
        var clear = new Button { Name = "ClearFilter", Content = UiText.Get("results.columnMenu.clearFilter"), Height = 28, MinHeight = 28, HorizontalAlignment = HorizontalAlignment.Left, IsVisible = hasFilter, IsEnabled = canChange };
        Grid.SetColumn(apply, 1);
        var footer = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), Children = { clear, apply } };

        void SyncAll()
        {
            var checkedCount = vm.Values.Count(v => v.IsChecked);
            all.IsChecked = checkedCount == 0 ? false : checkedCount == vm.Values.Count ? true : null;
        }

        void Refresh(string? property)
        {
            if (property is nameof(ColumnMenuViewModel.Values))
            {
                list.ItemsSource = vm.Values;
            }

            SyncAll();
            loading.IsVisible = vm.IsLoadingValues;
            hint.Text = vm.ValuesHint ?? UiText.Get("results.columnMenu.hint");
            apply.IsEnabled = canChange && !vm.IsLoadingValues;
        }

        Refresh(nameof(ColumnMenuViewModel.Values));
        vm.PropertyChanged += (_, e) => Refresh(e.PropertyName);
        search.TextChanged += (_, _) => vm.ValueSearch = search.Text ?? "";
        all.Click += (_, _) =>
        {
            var target = vm.Values.Any(v => !v.IsChecked);
            foreach (var item in vm.Values)
            {
                item.IsChecked = target;
            }

            list.ItemsSource = null;
            list.ItemsSource = vm.Values;
            SyncAll();
        };
        apply.Click += (_, _) =>
        {
            _flyout.Hide();
            vm.ApplyValuesCommand.Execute(null);
        };
        clear.Click += (_, _) =>
        {
            _flyout.Hide();
            vm.ClearColumnFilterCommand.Execute(null);
        };

        // The popup keeps one size whatever the list holds: only the list scrolls.
        var panel = new Grid { Width = MenuWidth, Height = MenuHeight, RowSpacing = 8, RowDefinitions = new RowDefinitions("Auto,Auto,Auto,*,Auto,Auto") };
        Place(panel, titleRow, 0);
        Place(panel, searchRow, 1);
        Place(panel, headerBox, 2);
        Place(panel, list, 3);
        Place(panel, new Grid { Height = 18, Children = { hint, loading } }, 4);
        Place(panel, footer, 5);

        // Tunnelling, so Enter applies before a focused control turns it into something else.
        panel.AddHandler(InputElement.KeyDownEvent, (_, e) => OnKey(e, vm, apply), RoutingStrategies.Tunnel);
        return panel;
    }

    private void OnKey(KeyEventArgs e, ColumnMenuViewModel vm, Button apply)
    {
        if (e.Key == Key.Escape)
        {
            _flyout.Hide();
            e.Handled = true;
        }
        else if (e.Key == Key.Enter && apply.IsEnabled)
        {
            _flyout.Hide();
            vm.ApplyValuesCommand.Execute(null);
            e.Handled = true;
        }
    }

    private static CheckBox BuildValueRow(Control owner, ValueItem item, Action changed)
    {
        var muted = Brush(owner, "SquillFg3Brush");
        var text = new TextBlock { Text = item.Text, TextTrimming = TextTrimming.CharacterEllipsis, VerticalAlignment = VerticalAlignment.Center };
        var count = new TextBlock { Text = item.Count.ToString(System.Globalization.CultureInfo.CurrentCulture), Foreground = muted, FontSize = 12, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(12, 0, 0, 0) };
        if (item.Value is null)
        {
            text.FontStyle = FontStyle.Italic;
            text.Foreground = muted;
        }

        Grid.SetColumn(count, 1);
        var row = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), Children = { text, count } };
        var box = new CheckBox { IsChecked = item.IsChecked, Content = row, MinHeight = 24, HorizontalAlignment = HorizontalAlignment.Stretch };
        AutomationProperties.SetName(box, item.Text);
        box.IsCheckedChanged += (_, _) =>
        {
            item.IsChecked = box.IsChecked == true;
            changed();
        };
        return box;
    }
}
