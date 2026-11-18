using System.Collections.Specialized;
using System.ComponentModel;
using System.Windows.Input;
using Microsoft.UI.Input;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Squill.Core;
using Squill.Core.Security;
using Squill.Presentation.Localization;
using Squill.Presentation.Security;
using Windows.System;
using Windows.UI.Core;
using ColumnDefinition = Microsoft.UI.Xaml.Controls.ColumnDefinition;
using RowDefinition = Microsoft.UI.Xaml.Controls.RowDefinition;

namespace Squill.App.WinUI.Views;

/// <summary>The security tab (mockup M11), built in code from <see cref="SecurityViewModel"/>: the role list, the role header, the privilege matrix with its legend, and the generated SQL with its Copy / Cancel / Apply footer.</summary>
public sealed partial class SecurityView : UserControl
{
    private const double NameWidth = 280;
    private const double CellWidth = 92;
    private const double RowHeight = 28;

    // shortcut: only the first SecurityViewModel.MaxShownRows are built as elements (no virtualization), a notice under the matrix says so; upgrade to a Win2D canvas if a database routinely lists more.
    private static readonly PrivilegeKind[] Kinds = Enum.GetValues<PrivilegeKind>();

    private readonly Border _topLine = new() { Height = 2 };
    private readonly TextBox _roleFilter = new() { Margin = new Thickness(12, 12, 12, 8) };
    private readonly StackPanel _roleList = new();
    private readonly TextBlock _roleName = new() { FontSize = 20, FontWeight = FontWeights.SemiBold, FontFamily = Mono, VerticalAlignment = VerticalAlignment.Center };
    private readonly StackPanel _badges = new() { Orientation = Orientation.Horizontal, Spacing = 6, VerticalAlignment = VerticalAlignment.Center };
    private readonly TextBlock _roleSubtitle = new() { TextWrapping = TextWrapping.Wrap };
    private readonly Button _schemas = new() { Flyout = new Flyout { Placement = Microsoft.UI.Xaml.Controls.Primitives.FlyoutPlacementMode.BottomEdgeAlignedLeft } };
    private readonly CheckBox _showInherited = new() { MinWidth = 0 };
    private readonly StackPanel _legend = new() { Orientation = Orientation.Horizontal, Spacing = 14, VerticalAlignment = VerticalAlignment.Center };
    private readonly TextBlock _message = new() { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(24, 8, 24, 8) };
    private readonly TextBlock _rowsCut = new() { Margin = new Thickness(12, 8, 12, 8), Visibility = Visibility.Collapsed };
    private readonly Grid _matrixHeader = new() { Height = RowHeight + 4 };
    private readonly Grid _matrixBody = new() { Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent) };
    private readonly Border _matrix = new() { IsTabStop = true, BorderThickness = new Thickness(0) };
    private readonly TextBlock _sqlTitle = new() { FontSize = 12, FontWeight = FontWeights.Bold, VerticalAlignment = VerticalAlignment.Center };
    private readonly Border _changesPill = new() { Padding = new Thickness(6, 1, 6, 1), CornerRadius = new CornerRadius(4), VerticalAlignment = VerticalAlignment.Center, Child = new TextBlock { FontSize = 11, FontWeight = FontWeights.Bold } };
    private readonly TextBlock _sql = new() { TextWrapping = TextWrapping.Wrap, IsTextSelectionEnabled = true };
    private readonly Button _copy = new();
    private readonly Button _discard = new();
    private readonly Button _apply = new();
    private readonly List<Border> _panels = [];

    private SecurityViewModel? _vm;
    private int _row;
    private int _column;
    private bool _syncQueued;
    private bool _matrixFocused;

    /// <summary>Creates the view; set <see cref="ViewModel"/> to draw it.</summary>
    public SecurityView()
    {
        HorizontalContentAlignment = HorizontalAlignment.Stretch;
        VerticalContentAlignment = VerticalAlignment.Stretch;
        Content = BuildLayout();
        ActualThemeChanged += (_, _) => Paint();
        Loaded += (_, _) => Paint();
        var save = new KeyboardAccelerator { Key = VirtualKey.S, Modifiers = VirtualKeyModifiers.Control };
        save.Invoked += (_, args) =>
        {
            if (_vm is null)
                return;
            Run(_vm.ApplyCommand);
            args.Handled = true;
        };
        KeyboardAccelerators.Add(save);
        _roleFilter.TextChanged += (_, _) => { if (_vm is { } vm && vm.RoleFilter != _roleFilter.Text) vm.RoleFilter = _roleFilter.Text; };
        _showInherited.Click += (_, _) => { if (_vm is { } vm) vm.ShowInherited = _showInherited.IsChecked == true; };
        _schemas.Flyout.Opening += (_, _) => BuildSchemaFlyout();
        _copy.Click += (_, _) => Run(_vm?.CopyScriptCommand);
        _discard.Click += (_, _) => Run(_vm?.DiscardChangesCommand);
        _apply.Click += (_, _) => Run(_vm?.ApplyCommand);
        _matrixBody.Tapped += OnMatrixTapped;
        _matrix.KeyDown += OnMatrixKeyDown;
        _matrix.GotFocus += (_, _) => SetMatrixFocus(true);
        _matrix.LostFocus += (_, _) => SetMatrixFocus(false);
    }

    /// <summary>Gets or sets the security tab drawn; the view redraws every part.</summary>
    public SecurityViewModel? ViewModel
    {
        get => _vm;
        set
        {
            if (ReferenceEquals(_vm, value))
                return;
            if (_vm is not null)
                Detach(_vm);
            _vm = value;
            _row = _column = 0;
            if (_vm is not null)
                Attach(_vm);
            Texts();
            Paint();
        }
    }

    private void Attach(SecurityViewModel vm)
    {
        vm.PropertyChanged += OnChanged;
        vm.Rows.CollectionChanged += OnCollectionChanged;
        vm.GroupRoles.CollectionChanged += OnCollectionChanged;
        vm.UserRoles.CollectionChanged += OnCollectionChanged;
    }

    private void Detach(SecurityViewModel vm)
    {
        vm.PropertyChanged -= OnChanged;
        vm.Rows.CollectionChanged -= OnCollectionChanged;
        vm.GroupRoles.CollectionChanged -= OnCollectionChanged;
        vm.UserRoles.CollectionChanged -= OnCollectionChanged;
    }

    private static void Run(ICommand? command)
    {
        if (command?.CanExecute(null) == true)
            command.Execute(null);
    }

    private Brush Brush(string key) => Tokens.Brush(this, key);

    private static SolidColorBrush Transparent => new(Microsoft.UI.Colors.Transparent);

    private static FontFamily Mono => (FontFamily)Application.Current.Resources["SquillFontMono"];

    private void OnChanged(object? sender, PropertyChangedEventArgs e) => QueueSync();

    private void OnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e) => QueueSync();

    // A role pick or a toggle raises many changes: redraw once, on the UI thread.
    private void QueueSync()
    {
        if (_syncQueued)
            return;
        _syncQueued = true;
        DispatcherQueue.TryEnqueue(() =>
        {
            _syncQueued = false;
            SyncAll();
        });
    }

    // ---- layout ----

    private Grid BuildLayout()
    {
        var main = new Grid();
        main.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(260) });
        main.ColumnDefinitions.Add(new ColumnDefinition());
        main.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(360) });
        var roles = BuildRoles();
        var center = BuildCenter();
        var sql = BuildSql();
        Grid.SetColumn(center, 1);
        Grid.SetColumn(sql, 2);
        main.Children.Add(roles);
        main.Children.Add(center);
        main.Children.Add(sql);
        var root = new Grid();
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition());
        Grid.SetRow(main, 1);
        root.Children.Add(_topLine);
        root.Children.Add(main);
        return root;
    }

    private Border BuildRoles()
    {
        var list = new ScrollViewer { Content = _roleList, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Padding = new Thickness(8, 0, 8, 8) };
        var grid = new Grid();
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        grid.RowDefinitions.Add(new RowDefinition());
        Grid.SetRow(list, 1);
        grid.Children.Add(_roleFilter);
        grid.Children.Add(list);
        var panel = new Border { BorderThickness = new Thickness(0, 0, 1, 0), Child = grid };
        _panels.Add(panel);
        return panel;
    }

    private Grid BuildCenter()
    {
        var title = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12, Children = { _roleName, _badges } };
        var header = new StackPanel { Spacing = 4, Padding = new Thickness(24, 16, 24, 8), Children = { title, _roleSubtitle } };
        var tools = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 16, Padding = new Thickness(24, 4, 24, 8), Children = { _schemas, _showInherited, _legend } };
        _matrix.Child = new StackPanel { Children = { _matrixHeader, _matrixBody, _rowsCut }, MinWidth = NameWidth + CellWidth * Kinds.Length };
        var scroll = new ScrollViewer { Content = _matrix, HorizontalScrollBarVisibility = ScrollBarVisibility.Auto, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Padding = new Thickness(24, 0, 24, 16) };
        var grid = new Grid();
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        grid.RowDefinitions.Add(new RowDefinition());
        Grid.SetRow(tools, 1);
        Grid.SetRow(_message, 2);
        Grid.SetRow(scroll, 3);
        grid.Children.Add(header);
        grid.Children.Add(tools);
        grid.Children.Add(_message);
        grid.Children.Add(scroll);
        _matrixHeader.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(NameWidth) });
        _matrixBody.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(NameWidth) });
        foreach (var _ in Kinds)
        {
            _matrixHeader.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(CellWidth) });
            _matrixBody.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(CellWidth) });
        }

        return grid;
    }

    private Border BuildSql()
    {
        var head = new Grid { Padding = new Thickness(16, 14, 16, 10), ColumnSpacing = 8 };
        head.ColumnDefinitions.Add(new ColumnDefinition());
        head.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        Grid.SetColumn(_changesPill, 1);
        head.Children.Add(_sqlTitle);
        head.Children.Add(_changesPill);
        var footer = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Padding = new Thickness(16, 10, 16, 14), HorizontalAlignment = HorizontalAlignment.Right, Children = { _copy, _discard, _apply } };
        var body = new ScrollViewer { Content = _sql, Padding = new Thickness(16, 0, 16, 0), VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        var grid = new Grid();
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        grid.RowDefinitions.Add(new RowDefinition());
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        Grid.SetRow(body, 1);
        Grid.SetRow(footer, 2);
        grid.Children.Add(head);
        grid.Children.Add(body);
        grid.Children.Add(footer);
        var panel = new Border { BorderThickness = new Thickness(1, 0, 0, 0), Child = grid };
        _panels.Add(panel);
        return panel;
    }

    // ---- content ----

    private void Texts()
    {
        _roleFilter.PlaceholderText = UiText.Get("security.filterPlaceholder");
        AutomationProperties.SetName(_roleFilter, UiText.Get("security.filterPlaceholder"));
        _showInherited.Content = UiText.Get("security.showInherited");
        _sqlTitle.Text = UiText.Get("security.sql");
        _copy.Content = UiText.Get("security.copy");
        _discard.Content = UiText.Get("security.discard");
        _apply.Content = UiText.Get("security.apply") + "  Ctrl+S";
        AutomationProperties.SetName(_apply, UiText.Get("security.apply"));
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
        ((TextBlock)_changesPill.Child).Foreground = Brush("SquillAccentTextBrush");
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
        if (_roleFilter.Text != vm.RoleFilter)
            _roleFilter.Text = vm.RoleFilter;
        SyncRoles(vm);
        SyncHeader(vm);
        SyncMatrix(vm);
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
        _roleList.Children.Add(new TextBlock { Text = heading, FontSize = 11, FontWeight = FontWeights.Bold, Foreground = Brush("SquillFg2Brush"), Margin = new Thickness(8, 12, 8, 4) });
        foreach (var role in roles)
        {
            var selected = vm.SelectedRole == role;
            var text = new StackPanel { Children = { new TextBlock { Text = role.Name, FontFamily = Mono, Foreground = Brush("SquillFgBrush") }, new TextBlock { Text = SecurityViewModel.RoleSubtitle(role), FontSize = 11, Foreground = Brush("SquillFg2Brush") } } };
            var item = new Border { Child = text, Padding = new Thickness(10, 6, 10, 6), CornerRadius = new CornerRadius(6), Background = selected ? Brush("SquillAccentSoftBrush") : Transparent, IsTabStop = true };
            AutomationProperties.SetName(item, role.Name);
            var picked = role;
            item.Tapped += (_, _) => _ = vm.SelectRoleAsync(picked);
            item.KeyDown += (_, e) =>
            {
                if (e.Key is not (VirtualKey.Enter or VirtualKey.Space))
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
                Padding = new Thickness(6, 1, 6, 1),
                CornerRadius = new CornerRadius(4),
                Background = Brush(danger ? "SquillDangerSoftBrush" : "SquillBgActiveBrush"),
                Child = new TextBlock { Text = badge, FontSize = 11, FontWeight = FontWeights.Bold, Foreground = Brush(danger ? "SquillDangerBrush" : "SquillFg2Brush") },
            });
        }

        _schemas.Content = vm.SchemaSelectorText;
        _showInherited.IsChecked = vm.ShowInherited;
        _legend.Children.Clear();
        foreach (var display in new[] { PrivilegeCellDisplay.Granted, PrivilegeCellDisplay.Inherited, PrivilegeCellDisplay.PendingGrant, PrivilegeCellDisplay.PendingRevoke, PrivilegeCellDisplay.Empty })
            _legend.Children.Add(new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, Children = { CellBox(display, false), new TextBlock { Text = UiText.Get(SecurityFormat.StateKey(display)), FontSize = 12, Foreground = Brush("SquillFg2Brush"), VerticalAlignment = VerticalAlignment.Center } } });
        _message.Foreground = Brush(vm.Error.Length > 0 ? "SquillDangerBrush" : "SquillFg2Brush");
        _message.Text = vm.Error.Length > 0 ? vm.Error : vm.SelectedRole is null ? UiText.Get("security.pick") : vm.IsLoading ? UiText.Get("security.loading") : "";
        _message.Visibility = _message.Text.Length == 0 ? Visibility.Collapsed : Visibility.Visible;
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
                var chosen = boxes.Where(b => b.IsChecked == true).Select(b => (string)b.Content).ToList();
                if (chosen.Count == 0)
                    box.IsChecked = true; // at least one schema stays shown
                else if (!await vm.SetSchemasAsync(chosen))
                    box.IsChecked = !box.IsChecked; // the user kept the pending changes
            };
            boxes.Add(box);
            panel.Children.Add(box);
        }

        ((Flyout)_schemas.Flyout).Content = new ScrollViewer { Content = panel, MaxHeight = 320 };
    }

    // ---- matrix ----

    private void SyncMatrix(SecurityViewModel vm)
    {
        _rowsCut.Text = vm.RowsCutText;
        _rowsCut.Foreground = Brush("SquillFg3Brush");
        _rowsCut.Visibility = vm.HiddenRowCount > 0 ? Visibility.Visible : Visibility.Collapsed;
        _matrixHeader.Children.Clear();
        _matrixBody.Children.Clear();
        _matrixBody.RowDefinitions.Clear();
        _matrixHeader.Visibility = vm.Rows.Count == 0 ? Visibility.Collapsed : Visibility.Visible;
        _matrixHeader.Children.Add(HeaderText(UiText.Get("security.col.object"), 0, TextAlignment.Left));
        for (var i = 0; i < Kinds.Length; i++)
            _matrixHeader.Children.Add(HeaderText(Kinds[i].ToString().ToUpperInvariant(), i + 1, TextAlignment.Center));
        var rows = Math.Min(vm.Rows.Count, SecurityViewModel.MaxShownRows);
        _row = Math.Clamp(_row, 0, Math.Max(rows - 1, 0));
        for (var r = 0; r < rows; r++)
        {
            _matrixBody.RowDefinitions.Add(new RowDefinition { Height = new GridLength(RowHeight) });
            var row = vm.Rows[r];
            var label = new TextBlock { Text = $"{row.Schema}.{row.Name}", FontFamily = Mono, Foreground = Brush("SquillFgBrush"), TextTrimming = TextTrimming.CharacterEllipsis, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 12, 0) };
            Grid.SetRow(label, r);
            _matrixBody.Children.Add(label);
            for (var c = 0; c < Kinds.Length; c++)
            {
                var cell = row.Cells[c];
                var ring = _matrixFocused && r == _row && c == _column ? Brush("SquillAccentBrush") : Transparent;
                var wrap = new Border { BorderThickness = new Thickness(2), CornerRadius = new CornerRadius(4), Margin = new Thickness(1), Child = CellBox(cell.Display, true), BorderBrush = ring };
                var state = UiText.Get(SecurityFormat.StateKey(cell.Display));
                AutomationProperties.SetName(wrap, $"{row.Schema}.{row.Name} {Kinds[c].ToString().ToUpperInvariant()} {state}");
                if (cell.Tooltip.Length > 0)
                    ToolTipService.SetToolTip(wrap, cell.Tooltip);
                Grid.SetRow(wrap, r);
                Grid.SetColumn(wrap, c + 1);
                _matrixBody.Children.Add(wrap);
            }
        }
    }

    private TextBlock HeaderText(string text, int column, TextAlignment alignment)
    {
        var block = new TextBlock { Text = text, FontSize = 11, FontWeight = FontWeights.Bold, Foreground = Brush("SquillFg2Brush"), VerticalAlignment = VerticalAlignment.Center, HorizontalTextAlignment = alignment };
        Grid.SetColumn(block, column);
        return block;
    }

    // The drawn checkbox: filled when held, outlined when pending or empty, a check or a minus inside.
    private Border CellBox(PrivilegeCellDisplay display, bool centered)
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
            Background = fill is null ? Transparent : Brush(fill),
            HorizontalAlignment = centered ? HorizontalAlignment.Center : HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Center,
            Child = glyph.Length == 0 ? null : new TextBlock { Text = glyph, FontSize = 11, FontWeight = FontWeights.Bold, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, Foreground = Brush(fill is null ? "SquillDangerBrush" : "SquillOnAccentBrush") },
        };
    }

    private void SetMatrixFocus(bool focused)
    {
        _matrixFocused = focused;
        if (_vm is not null)
            SyncMatrix(_vm);
    }

    private void OnMatrixTapped(object sender, TappedRoutedEventArgs e)
    {
        if (_vm is not { } vm)
            return;
        var point = e.GetPosition(_matrixBody);
        if (SecurityFormat.HitTest(point.X, point.Y, NameWidth, CellWidth, RowHeight, Math.Min(vm.Rows.Count, SecurityViewModel.MaxShownRows), Kinds.Length) is not var (row, column))
            return;
        _row = row;
        _column = column;
        _matrix.Focus(FocusState.Pointer);
        vm.ToggleCell(row, column);
        SyncMatrix(vm);
    }

    private void OnMatrixKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (_vm is not { } vm)
            return;
        if (e.Key == VirtualKey.Space)
        {
            if (InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Shift).HasFlag(CoreVirtualKeyStates.Down))
                vm.ToggleRow(_row);
            else
                vm.ToggleCell(_row, _column);
            SyncMatrix(vm);
            e.Handled = true;
            return;
        }

        var (rowStep, columnStep) = e.Key switch
        {
            VirtualKey.Up => (-1, 0),
            VirtualKey.Down => (1, 0),
            VirtualKey.Left => (0, -1),
            VirtualKey.Right => (0, 1),
            _ => (0, 0),
        };
        if (rowStep == 0 && columnStep == 0)
            return;
        (_row, _column) = SecurityFormat.Move(rowStep, columnStep, _row, _column, Math.Min(vm.Rows.Count, SecurityViewModel.MaxShownRows), Kinds.Length);
        SyncMatrix(vm);
        e.Handled = true;
    }

    // ---- generated SQL ----

    private void SyncSql(SecurityViewModel vm)
    {
        ((TextBlock)_changesPill.Child).Text = vm.ChangesText;
        _changesPill.Visibility = vm.ChangeCount > 0 ? Visibility.Visible : Visibility.Collapsed;
        var script = vm.ScriptText;
        _sql.Text = script.Length > 0 ? script : UiText.Get("security.noChanges");
        _sql.FontFamily = script.Length > 0 ? Mono : FontFamily;
        _sql.Foreground = Brush(script.Length > 0 ? "SquillFgBrush" : "SquillFg2Brush");
        _copy.IsEnabled = vm.CopyScriptCommand.CanExecute(null);
        _discard.IsEnabled = vm.DiscardChangesCommand.CanExecute(null);
        _apply.IsEnabled = vm.ApplyCommand.CanExecute(null);
    }
}
