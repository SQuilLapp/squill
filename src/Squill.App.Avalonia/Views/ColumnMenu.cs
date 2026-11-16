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
    private const double MenuWidth = 300;
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

    private Button Entry(Control owner, string text, bool isChecked, bool enabled, string? blocked, Action run)
    {
        var tick = new AvaloniaPath { Width = 12, Height = 12, IsVisible = isChecked };
        tick.Classes.Add("icon");
        tick.Bind(AvaloniaPath.DataProperty, owner.GetResourceObservable("SquillIconCheck"));
        var row = new Grid { ColumnDefinitions = new ColumnDefinitions("18,*") };
        row.Children.Add(tick);
        var label = new TextBlock { Text = text, VerticalAlignment = VerticalAlignment.Center };
        Grid.SetColumn(label, 1);
        row.Children.Add(label);
        var button = new Button
        {
            Content = row,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Left,
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            Height = 30,
            MinHeight = 30,
            IsEnabled = enabled,
        };
        AutomationProperties.SetName(button, text);
        if (!enabled)
        {
            ToolTip.SetShowOnDisabled(button, true);
            ToolTip.SetTip(button, blocked);
        }

        button.Click += (_, _) =>
        {
            _flyout.Hide();
            run();
        };
        return button;
    }

    private static Border Separator(Control owner) => new() { Height = 1, Margin = new Thickness(0, 4), Background = Brush(owner, "SquillBorderSubtleBrush") };

    private StackPanel Build(Control owner, ColumnMenuViewModel vm, bool hasFilter)
    {
        var canChange = vm.CanSort;
        var muted = Brush(owner, "SquillFg3Brush");
        var sortAsc = Entry(owner, UiText.Get("results.columnMenu.sortAsc"), vm.CurrentSort is { Descending: false }, canChange, vm.BlockedReason, () => vm.SortAscendingCommand.Execute(null));
        var sortDesc = Entry(owner, UiText.Get("results.columnMenu.sortDesc"), vm.CurrentSort is { Descending: true }, canChange, vm.BlockedReason, () => vm.SortDescendingCommand.Execute(null));
        var search = new TextBox { Name = "ValueSearch", PlaceholderText = UiText.Get("results.columnMenu.searchValue"), Height = 28, MinHeight = 28, Text = vm.ValueSearch, IsEnabled = canChange };
        AutomationProperties.SetName(search, UiText.Get("results.columnMenu.searchValue"));
        var list = new ListBox { Name = "Values", MaxHeight = 240, ItemsSource = vm.Values, IsEnabled = canChange, SelectionMode = SelectionMode.Single };
        list.ItemTemplate = new FuncDataTemplate<ValueItem>((item, _) => item is null ? null : BuildValueRow(owner, item));
        var loading = new ProgressBar { IsIndeterminate = true, IsVisible = vm.IsLoadingValues };
        var hint = new TextBlock { FontSize = 11, Text = vm.ValuesHint ?? "", IsVisible = vm.ValuesHint is not null, Foreground = muted };
        var apply = new Button { Name = "Apply", Content = UiText.Get("results.filter.editor.apply"), Height = 28, MinHeight = 28, HorizontalAlignment = HorizontalAlignment.Right, IsEnabled = canChange && !vm.IsLoadingValues };
        apply.Classes.Add("primary");
        ToolTip.SetShowOnDisabled(apply, true);
        ToolTip.SetTip(apply, vm.BlockedReason);

        vm.PropertyChanged += (_, e) => Refresh(e);
        void Refresh(PropertyChangedEventArgs e)
        {
            if (e.PropertyName is nameof(ColumnMenuViewModel.Values))
            {
                list.ItemsSource = vm.Values;
            }

            loading.IsVisible = vm.IsLoadingValues;
            hint.Text = vm.ValuesHint ?? "";
            hint.IsVisible = vm.ValuesHint is not null;
            apply.IsEnabled = canChange && !vm.IsLoadingValues;
        }

        search.TextChanged += (_, _) => vm.ValueSearch = search.Text ?? "";
        apply.Click += (_, _) =>
        {
            _flyout.Hide();
            vm.ApplyValuesCommand.Execute(null);
        };

        var caps = new TextBlock { Text = UiText.Get("results.columnMenu.filterByValue"), Margin = new Thickness(8, 4, 0, 4), FontSize = 11, FontWeight = FontWeight.SemiBold, Foreground = muted };
        var panel = new StackPanel { Width = MenuWidth, Spacing = 2 };
        panel.Children.Add(sortAsc);
        panel.Children.Add(sortDesc);
        panel.Children.Add(Separator(owner));
        panel.Children.Add(caps);
        panel.Children.Add(search);
        panel.Children.Add(loading);
        panel.Children.Add(list);
        panel.Children.Add(hint);
        panel.Children.Add(new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 4, 0, 0), Children = { apply } });
        panel.Children.Add(Separator(owner));
        if (hasFilter)
        {
            panel.Children.Add(Entry(owner, UiText.Get("results.columnMenu.clearFilter"), false, canChange, vm.BlockedReason, () => vm.ClearColumnFilterCommand.Execute(null)));
        }

        // No Ctrl+C chip: Ctrl+C in the grid copies the selection, not the column name.
        panel.Children.Add(Entry(owner, UiText.Get("results.columnMenu.copyName"), false, true, null, () =>
        {
            if (TopLevel.GetTopLevel(owner)?.Clipboard is { } clipboard)
            {
                _ = clipboard.SetTextAsync(vm.ColumnName);
            }
        }));

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

    private static CheckBox BuildValueRow(Control owner, ValueItem item)
    {
        var muted = Brush(owner, "SquillFg3Brush");
        var text = new TextBlock { Text = item.Text, TextTrimming = TextTrimming.CharacterEllipsis, VerticalAlignment = VerticalAlignment.Center };
        var count = new TextBlock { Text = item.Count.ToString(System.Globalization.CultureInfo.CurrentCulture), Foreground = muted, FontSize = 12, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(8, 0, 0, 0) };
        if (item.Value is null)
        {
            text.FontStyle = FontStyle.Italic;
            text.Foreground = muted;
        }

        Grid.SetColumn(count, 1);
        var row = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), Children = { text, count } };
        var box = new CheckBox { IsChecked = item.IsChecked, Content = row, MinHeight = 24, HorizontalAlignment = HorizontalAlignment.Stretch };
        AutomationProperties.SetName(box, item.Text);
        box.IsCheckedChanged += (_, _) => item.IsChecked = box.IsChecked == true;
        return box;
    }
}
