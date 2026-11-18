using System.Collections.Specialized;
using System.ComponentModel;
using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Squill.App.Avalonia.Controls;
using Squill.Core;
using Squill.Core.Security;
using Squill.Presentation.Localization;
using Squill.Presentation.Security;

namespace Squill.App.Avalonia.Views;

/// <summary>The security tab (mockup M11), built in code from <see cref="SecurityViewModel"/>: the role list, the role header, the privilege matrix with its legend, and the generated SQL with its Copy / Cancel / Apply footer.</summary>
public sealed class SecurityView : UserControl
{
    private static readonly FontFamily Mono = ErDiagramCanvas.Mono;

    private readonly Border _topLine = new() { Height = 2, VerticalAlignment = VerticalAlignment.Top };
    private readonly TextBox _roleFilter = new() { Margin = new Thickness(12, 12, 12, 8) };
    private readonly StackPanel _roleList = new();
    private readonly TextBlock _roleName = new() { FontSize = 20, FontWeight = FontWeight.SemiBold, FontFamily = Mono, VerticalAlignment = VerticalAlignment.Center };
    private readonly StackPanel _badges = new() { Orientation = Orientation.Horizontal, Spacing = 6, VerticalAlignment = VerticalAlignment.Center };
    private readonly TextBlock _roleSubtitle = new() { TextWrapping = TextWrapping.Wrap };
    private readonly Button _schemas = new() { Flyout = new Flyout { Placement = PlacementMode.BottomEdgeAlignedLeft } };
    private readonly CheckBox _showInherited = new() { MinWidth = 0 };
    private readonly StackPanel _legend = new() { Orientation = Orientation.Horizontal, Spacing = 14, VerticalAlignment = VerticalAlignment.Center };
    private readonly TextBlock _message = new() { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(24, 8) };
    private readonly TextBlock _rowsCut = new() { Name = "RowsCutNotice", Margin = new Thickness(12, 8), IsVisible = false };
    private readonly PrivilegeMatrixCanvas _matrix = new() { HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top };
    private readonly TextBlock _sqlTitle = new() { FontSize = 12, FontWeight = FontWeight.Bold, VerticalAlignment = VerticalAlignment.Center };
    private readonly Border _changesPill = new() { Padding = new Thickness(6, 1), CornerRadius = new CornerRadius(4), VerticalAlignment = VerticalAlignment.Center, Child = new TextBlock { FontSize = 11, FontWeight = FontWeight.Bold } };
    private readonly SelectableTextBlock _sql = new() { TextWrapping = TextWrapping.Wrap };
    private readonly Button _copy = new();
    private readonly Button _discard = new();
    private readonly Button _apply = new();
    private readonly List<Border> _panels = [];

    private SecurityViewModel? _vm;
    private bool _syncQueued;

    /// <summary>Creates the view; set <see cref="StyledElement.DataContext"/> to a <see cref="SecurityViewModel"/> to draw it.</summary>
    public SecurityView()
    {
        Content = BuildLayout();
        ActualThemeVariantChanged += (_, _) => Paint();
        KeyBindings.Add(new KeyBinding { Gesture = new KeyGesture(Key.S, KeyModifiers.Control), Command = new DelegateCommand(() => Run(_vm?.ApplyCommand)) });
        _roleFilter.TextChanged += (_, _) => { if (_vm is { } vm && vm.RoleFilter != (_roleFilter.Text ?? "")) vm.RoleFilter = _roleFilter.Text ?? ""; };
        _showInherited.IsCheckedChanged += (_, _) => { if (_vm is { } vm && vm.ShowInherited != (_showInherited.IsChecked == true)) vm.ShowInherited = _showInherited.IsChecked == true; };
        ((Flyout)_schemas.Flyout!).Opening += (_, _) => BuildSchemaFlyout();
        _copy.Click += (_, _) => Run(_vm?.CopyScriptCommand);
        _discard.Click += (_, _) => Run(_vm?.DiscardChangesCommand);
        _apply.Click += (_, _) => Run(_vm?.ApplyCommand);
        Texts();
    }

    /// <inheritdoc />
    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        if (_vm is { } old)
        {
            old.PropertyChanged -= OnChanged;
            old.CopyRequested -= OnCopyRequested;
            old.Rows.CollectionChanged -= OnCollectionChanged;
            old.GroupRoles.CollectionChanged -= OnCollectionChanged;
            old.UserRoles.CollectionChanged -= OnCollectionChanged;
        }

        _vm = DataContext as SecurityViewModel;
        if (_vm is { } vm)
        {
            vm.PropertyChanged += OnChanged;
            vm.CopyRequested += OnCopyRequested;
            vm.Rows.CollectionChanged += OnCollectionChanged;
            vm.GroupRoles.CollectionChanged += OnCollectionChanged;
            vm.UserRoles.CollectionChanged += OnCollectionChanged;
        }

        _matrix.ViewModel = _vm;
        Paint();
    }

    private static void Run(ICommand? command)
    {
        if (command?.CanExecute(null) == true)
            command.Execute(null);
    }

    private async void OnCopyRequested(string text)
    {
        if (TopLevel.GetTopLevel(this)?.Clipboard is { } clipboard)
            await clipboard.SetTextAsync(text);
    }

    private IBrush Brush(string key) =>
        this.TryFindResource(key, ActualThemeVariant, out var value) && value is IBrush brush ? brush : Brushes.Magenta;

    private void OnChanged(object? sender, PropertyChangedEventArgs e) => QueueSync();

    private void OnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e) => QueueSync();

    // A role pick or a toggle raises many changes: redraw once, on the UI thread.
    private void QueueSync()
    {
        if (_syncQueued)
            return;
        _syncQueued = true;
        Dispatcher.UIThread.Post(() =>
        {
            _syncQueued = false;
            SyncAll();
        });
    }

    // ---- layout ----

    private Grid BuildLayout()
    {
        var main = new Grid { ColumnDefinitions = new ColumnDefinitions("260,*,360") };
        var center = BuildCenter();
        var sql = BuildSql();
        Grid.SetColumn(center, 1);
        Grid.SetColumn(sql, 2);
        main.Children.Add(BuildRoles());
        main.Children.Add(center);
        main.Children.Add(sql);
        return new Grid { Children = { main, _topLine } };
    }

    private Border BuildRoles()
    {
        var list = new ScrollViewer { Content = _roleList, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Padding = new Thickness(8, 0, 8, 8) };
        var grid = new Grid { RowDefinitions = new RowDefinitions("Auto,*") };
        Grid.SetRow(list, 1);
        grid.Children.Add(_roleFilter);
        grid.Children.Add(list);
        return Panel(grid, new Thickness(0, 0, 1, 0));
    }

    private Grid BuildCenter()
    {
        var title = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12, Children = { _roleName, _badges } };
        var header = new StackPanel { Spacing = 4, Margin = new Thickness(24, 16, 24, 8), Children = { title, _roleSubtitle } };
        var tools = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 16, Margin = new Thickness(24, 4, 24, 8), Children = { _schemas, _showInherited, _legend } };
        var scroll = new ScrollViewer { Content = new StackPanel { Children = { _matrix, _rowsCut } }, HorizontalScrollBarVisibility = ScrollBarVisibility.Auto, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Padding = new Thickness(24, 0, 24, 16) };
        var grid = new Grid { RowDefinitions = new RowDefinitions("Auto,Auto,Auto,*") };
        Grid.SetRow(tools, 1);
        Grid.SetRow(_message, 2);
        Grid.SetRow(scroll, 3);
        grid.Children.Add(header);
        grid.Children.Add(tools);
        grid.Children.Add(_message);
        grid.Children.Add(scroll);
        return grid;
    }

    private Border BuildSql()
    {
        var head = new Grid { Margin = new Thickness(16, 14, 16, 10), ColumnSpacing = 8, ColumnDefinitions = new ColumnDefinitions("*,Auto") };
        Grid.SetColumn(_changesPill, 1);
        head.Children.Add(_sqlTitle);
        head.Children.Add(_changesPill);
        var footer = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Margin = new Thickness(16, 10, 16, 14), HorizontalAlignment = HorizontalAlignment.Right, Children = { _copy, _discard, _apply } };
        var body = new ScrollViewer { Content = _sql, Padding = new Thickness(16, 0), VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        var grid = new Grid { RowDefinitions = new RowDefinitions("Auto,*,Auto") };
        Grid.SetRow(body, 1);
        Grid.SetRow(footer, 2);
        grid.Children.Add(head);
        grid.Children.Add(body);
        grid.Children.Add(footer);
        return Panel(grid, new Thickness(1, 0, 0, 0));
    }

    private Border Panel(Control child, Thickness border)
    {
        var panel = new Border { BorderThickness = border, Child = child };
        _panels.Add(panel);
        return panel;
    }

    // ---- content ----

    private void Texts()
    {
        _roleFilter.PlaceholderText = UiText.Get("security.filterPlaceholder");
        ToolTip.SetTip(_roleFilter, UiText.Get("security.filterPlaceholder"));
        _showInherited.Content = UiText.Get("security.showInherited");
        _sqlTitle.Text = UiText.Get("security.sql");
        _copy.Content = UiText.Get("security.copy");
        _discard.Content = UiText.Get("security.discard");
        _apply.Content = UiText.Get("security.apply") + "  Ctrl+S";
    }

    private void Paint()
    {
        foreach (var panel in _panels)
        {
            panel.Background = Brush("SquillBgSurfaceBrush");
            panel.BorderBrush = Brush("SquillBorderBrush");
        }

        _roleName.Foreground = Brush("SquillFgBrush");
        _roleSubtitle.Foreground = Brush("SquillFg2Brush");
        _sqlTitle.Foreground = Brush("SquillFg2Brush");
        _changesPill.Background = Brush("SquillAccentSoftBrush");
        ((TextBlock)_changesPill.Child!).Foreground = Brush("SquillAccentTextBrush");
        _apply.Background = Brush("SquillAccentBrush");
        _apply.Foreground = Brush("SquillOnAccentBrush");
        SyncAll();
    }

    private void SyncAll()
    {
        if (_vm is not { } vm)
            return;
        Texts();
        _topLine.Background = vm.Environment == ConnectionEnvironment.Production ? Brush("SquillDangerBrush") : null;
        if ((_roleFilter.Text ?? "") != vm.RoleFilter)
            _roleFilter.Text = vm.RoleFilter;
        SyncRoles(vm);
        SyncHeader(vm);
        _matrix.Refresh();
        _rowsCut.Text = vm.RowsCutText;
        _rowsCut.Foreground = Brush("SquillFg3Brush");
        _rowsCut.IsVisible = vm.HiddenRowCount > 0;
        SyncSql(vm);
    }

    private void SyncRoles(SecurityViewModel vm)
    {
        _roleList.Children.Clear();
        AddRoles(vm.GroupsHeading, vm.GroupRoles, vm);
        AddRoles(vm.UsersHeading, vm.UserRoles, vm);
    }

    private void AddRoles(string heading, IReadOnlyList<RoleInfo> roles, SecurityViewModel vm)
    {
        _roleList.Children.Add(new TextBlock { Text = heading, FontSize = 11, FontWeight = FontWeight.Bold, Foreground = Brush("SquillFg2Brush"), Margin = new Thickness(8, 12, 8, 4) });
        foreach (var role in roles)
        {
            var text = new StackPanel { Children = { new TextBlock { Text = role.Name, FontFamily = Mono, Foreground = Brush("SquillFgBrush") }, new TextBlock { Text = SecurityViewModel.RoleSubtitle(role), FontSize = 11, Foreground = Brush("SquillFg2Brush") } } };
            var item = new Border { Child = text, Padding = new Thickness(10, 6), CornerRadius = new CornerRadius(6), Background = vm.SelectedRole == role ? Brush("SquillAccentSoftBrush") : Brushes.Transparent, Focusable = true };
            var picked = role;
            item.PointerPressed += (_, _) => _ = vm.SelectRoleAsync(picked);
            item.KeyDown += (_, e) =>
            {
                if (e.Key is not (Key.Enter or Key.Space))
                    return;
                _ = vm.SelectRoleAsync(picked);
                e.Handled = true;
            };
            _roleList.Children.Add(item);
        }
    }

    private void SyncHeader(SecurityViewModel vm)
    {
        _roleName.Text = vm.SelectedRole?.Name ?? "";
        _roleSubtitle.Text = vm.RoleSubtitleText;
        _badges.Children.Clear();
        foreach (var badge in vm.RoleBadges)
        {
            var danger = badge == "SUPERUSER";
            _badges.Children.Add(new Border
            {
                Padding = new Thickness(6, 1),
                CornerRadius = new CornerRadius(4),
                Background = Brush(danger ? "SquillDangerSoftBrush" : "SquillBgActiveBrush"),
                Child = new TextBlock { Text = badge, FontSize = 11, FontWeight = FontWeight.Bold, Foreground = Brush(danger ? "SquillDangerBrush" : "SquillFg2Brush") },
            });
        }

        _schemas.Content = vm.SchemaSelectorText;
        _showInherited.IsChecked = vm.ShowInherited;
        _legend.Children.Clear();
        foreach (var display in new[] { PrivilegeCellDisplay.Granted, PrivilegeCellDisplay.Inherited, PrivilegeCellDisplay.PendingGrant, PrivilegeCellDisplay.PendingRevoke, PrivilegeCellDisplay.Empty })
            _legend.Children.Add(new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, Children = { LegendBox(display), new TextBlock { Text = UiText.Get(SecurityFormat.StateKey(display)), FontSize = 12, Foreground = Brush("SquillFg2Brush"), VerticalAlignment = VerticalAlignment.Center } } });
        _message.Foreground = Brush(vm.Error.Length > 0 ? "SquillDangerBrush" : "SquillFg2Brush");
        _message.Text = vm.Error.Length > 0 ? vm.Error : vm.SelectedRole is null ? UiText.Get("security.pick") : vm.IsLoading ? UiText.Get("security.loading") : "";
        _message.IsVisible = _message.Text.Length > 0;
    }

    // The legend swatch, drawn like a matrix cell.
    private Border LegendBox(PrivilegeCellDisplay display)
    {
        var fill = SecurityFormat.FillBrush(display);
        var glyph = SecurityFormat.Glyph(display);
        return new Border
        {
            Width = 16,
            Height = 16,
            CornerRadius = new CornerRadius(3),
            BorderThickness = new Thickness(1.5),
            BorderBrush = Brush(SecurityFormat.OutlineBrush(display)),
            Background = fill is null ? Brushes.Transparent : Brush(fill),
            VerticalAlignment = VerticalAlignment.Center,
            Child = glyph.Length == 0 ? null : new TextBlock { Text = glyph, FontSize = 11, FontWeight = FontWeight.Bold, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, Foreground = Brush(fill is null ? "SquillDangerBrush" : "SquillOnAccentBrush") },
        };
    }

    private void BuildSchemaFlyout()
    {
        if (_vm is not { } vm)
            return;
        var panel = new StackPanel { Spacing = 2 };
        var boxes = new List<CheckBox>();
        foreach (var schema in vm.AvailableSchemas)
        {
            var box = new CheckBox { Content = schema, IsChecked = vm.SelectedSchemas.Contains(schema), MinWidth = 0 };
            box.Click += async (_, _) =>
            {
                var chosen = boxes.Where(b => b.IsChecked == true).Select(b => (string)b.Content!).ToList();
                if (chosen.Count == 0)
                    box.IsChecked = true; // at least one schema stays shown
                else if (!await vm.SetSchemasAsync(chosen))
                    box.IsChecked = !box.IsChecked; // the user kept the pending changes
            };
            boxes.Add(box);
            panel.Children.Add(box);
        }

        ((Flyout)_schemas.Flyout!).Content = new ScrollViewer { Content = panel, MaxHeight = 320 };
    }

    // ---- generated SQL ----

    private void SyncSql(SecurityViewModel vm)
    {
        ((TextBlock)_changesPill.Child!).Text = vm.ChangesText;
        _changesPill.IsVisible = vm.ChangeCount > 0;
        var script = vm.ScriptText;
        _sql.Text = script.Length > 0 ? script : UiText.Get("security.noChanges");
        _sql.FontFamily = script.Length > 0 ? Mono : FontFamily;
        _sql.Foreground = Brush(script.Length > 0 ? "SquillFgBrush" : "SquillFg2Brush");
        _copy.IsEnabled = vm.CopyScriptCommand.CanExecute(null);
        _discard.IsEnabled = vm.DiscardChangesCommand.CanExecute(null);
        _apply.IsEnabled = vm.ApplyCommand.CanExecute(null);
    }

    private sealed class DelegateCommand(Action run) : ICommand
    {
        public event EventHandler? CanExecuteChanged
        {
            add { }
            remove { }
        }

        public bool CanExecute(object? parameter) => true;

        public void Execute(object? parameter) => run();
    }
}
