using System.Collections.Specialized;
using System.ComponentModel;
using System.Globalization;
using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.VisualTree;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;
using Squill.App.Avalonia.Controls;
using Squill.App.Avalonia.Editors;
using Squill.Core;
using Squill.Core.Schema.Compare;
using Squill.Presentation.Compare;
using Squill.Presentation.Localization;

namespace Squill.App.Avalonia.Views;

/// <summary>The schema compare tab (mockup M09): endpoint chips, the difference list, the two DDL panes and the migration script, all built in code from <see cref="SchemaCompareViewModel"/>.</summary>
public sealed class SchemaCompareView : UserControl
{
    private static readonly FontFamily Mono = ErDiagramCanvas.Mono;

    private readonly Border _header = new() { Padding = new Thickness(16, 8) };
    private readonly Border _sourceChip = new() { BorderThickness = new Thickness(3, 1, 1, 1), CornerRadius = new CornerRadius(6) };
    private readonly Border _targetChip = new() { BorderThickness = new Thickness(3, 1, 1, 1), CornerRadius = new CornerRadius(6) };
    private readonly ComboBox _sources = new() { MinWidth = 150, BorderThickness = new Thickness(0), Background = Brushes.Transparent };
    private readonly ComboBox _targets = new() { MinWidth = 150, BorderThickness = new Thickness(0), Background = Brushes.Transparent };
    private readonly Button _swap = new() { Content = "⇄", VerticalAlignment = VerticalAlignment.Bottom };
    private readonly TextBlock _tabSchema = new() { FontWeight = FontWeight.SemiBold, VerticalAlignment = VerticalAlignment.Bottom, Margin = new Thickness(8, 0, 0, 8) };
    private readonly TextBlock _compared = new() { VerticalAlignment = VerticalAlignment.Center };
    private readonly Button _compare = new() { VerticalAlignment = VerticalAlignment.Bottom };
    private readonly TextBlock _sourceLabel = new() { FontSize = 11, FontWeight = FontWeight.SemiBold };
    private readonly TextBlock _targetLabel = new() { FontSize = 11, FontWeight = FontWeight.SemiBold };
    private readonly Border _sourcePill = new() { Padding = new Thickness(6, 1), CornerRadius = new CornerRadius(4), VerticalAlignment = VerticalAlignment.Center, IsVisible = false };
    private readonly Border _targetPill = new() { Padding = new Thickness(6, 1), CornerRadius = new CornerRadius(4), VerticalAlignment = VerticalAlignment.Center, IsVisible = false };
    private readonly Border _sourceBadge = new() { Padding = new Thickness(4, 2), CornerRadius = new CornerRadius(4), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(8, 0, 0, 0), Child = new TextBlock { FontSize = 11, FontWeight = FontWeight.Bold } };
    private readonly Border _targetBadge = new() { Padding = new Thickness(4, 2), CornerRadius = new CornerRadius(4), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(8, 0, 0, 0), Child = new TextBlock { FontSize = 11, FontWeight = FontWeight.Bold } };

    private readonly ToggleButton _added = new();
    private readonly ToggleButton _modified = new();
    private readonly ToggleButton _removed = new();
    private readonly TextBox _filter = new();
    private readonly CheckBox _all = new() { IsThreeState = true };
    private readonly StackPanel _list = new();
    private readonly Dictionary<CompareItemViewModel, (CheckBox Box, Border Row)> _rows = [];

    private readonly TextBlock _itemName = new() { FontFamily = Mono, FontWeight = FontWeight.SemiBold, VerticalAlignment = VerticalAlignment.Center };
    private readonly Border _kindBadge = new() { Padding = new Thickness(6, 1), CornerRadius = new CornerRadius(4), VerticalAlignment = VerticalAlignment.Center };
    private readonly TextBlock _kindText = new() { FontSize = 11, FontWeight = FontWeight.SemiBold };
    private readonly TextBlock _itemSummary = new() { VerticalAlignment = VerticalAlignment.Center };
    private readonly TextBlock _sourceCaption = new() { Margin = new Thickness(16, 6, 0, 6) };
    private readonly TextBlock _targetCaption = new() { Margin = new Thickness(16, 6, 0, 6) };
    private readonly DdlPane _sourcePane = new(sourceSide: true);
    private readonly DdlPane _targetPane = new(sourceSide: false);
    private readonly TextBlock _empty = new() { HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, TextWrapping = TextWrapping.Wrap, TextAlignment = TextAlignment.Center, MaxWidth = 420 };

    private readonly TextBlock _scriptTitle = new() { FontWeight = FontWeight.SemiBold, VerticalAlignment = VerticalAlignment.Center };
    private readonly TextBlock _scriptInfo = new() { VerticalAlignment = VerticalAlignment.Center };
    private readonly Button _regenerate = new();
    private readonly Button _copy = new();
    private readonly Button _save = new();
    private readonly Button _open = new();
    private readonly Button _execute = new();
    private readonly TextBlock _message = new() { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(16, 0, 16, 6), IsVisible = false };
    private readonly Border _scriptStripe = new() { Width = 3 };
    private readonly AvaloniaSqlEditor _script = new();
    private readonly TextBlock _hint = new() { Margin = new Thickness(16, 8), TextWrapping = TextWrapping.Wrap };

    private SchemaCompareViewModel? _vm;
    private bool _syncing;

    /// <summary>Creates the view; set <see cref="UserControl.DataContext"/> to a <see cref="SchemaCompareViewModel"/> to draw it.</summary>
    public SchemaCompareView()
    {
        Content = BuildLayout();
        ActualThemeVariantChanged += (_, _) => Paint();
        KeyBindings.Add(Accelerator(Key.F7, KeyModifiers.None, vm => vm.NextDifferenceCommand));
        KeyBindings.Add(Accelerator(Key.F7, KeyModifiers.Shift, vm => vm.PreviousDifferenceCommand));
        KeyBindings.Add(Accelerator(Key.S, KeyModifiers.Control, vm => vm.SaveAsCommand));
        KeyBindings.Add(Accelerator(Key.Enter, KeyModifiers.Control, vm => vm.ExecuteScriptCommand));
        _sources.SelectionChanged += (_, _) =>
        {
            if (!_syncing && _vm is { } vm && _sources.SelectedIndex >= 0 && _sources.SelectedIndex < vm.Sources.Count)
                vm.Source = vm.Sources[_sources.SelectedIndex];
        };
        _targets.SelectionChanged += (_, _) =>
        {
            if (!_syncing && _vm is { } vm && _targets.SelectedIndex >= 0 && _targets.SelectedIndex < vm.Targets.Count)
                vm.Target = vm.Targets[_targets.SelectedIndex];
        };
        _added.IsCheckedChanged += (_, _) => { if (!_syncing && _vm is { } vm) vm.ShowAdded = _added.IsChecked == true; };
        _modified.IsCheckedChanged += (_, _) => { if (!_syncing && _vm is { } vm) vm.ShowModified = _modified.IsChecked == true; };
        _removed.IsCheckedChanged += (_, _) => { if (!_syncing && _vm is { } vm) vm.ShowRemoved = _removed.IsChecked == true; };
        _filter.TextChanged += (_, _) => { if (!_syncing && _vm is { } vm) vm.FilterText = _filter.Text ?? ""; };
        _all.Click += (_, _) => _vm?.SetAllIncludedCommand.Execute(_all.IsChecked != false);
        _script.TextChanged += (_, _) =>
        {
            if (!_syncing && _vm is { } vm && vm.Script != _script.Text)
                vm.Script = _script.Text;
        };
        _sourcePane.Scrolled += (_, _) => _targetPane.Follow(_sourcePane.Offset);
        _targetPane.Scrolled += (_, _) => _sourcePane.Follow(_targetPane.Offset);
        Bind(_swap, vm => vm.SwapCommand);
        Bind(_compare, vm => vm.CompareCommand);
        Bind(_regenerate, vm => vm.RegenerateCommand);
        Bind(_copy, vm => vm.CopyCommand);
        Bind(_save, vm => vm.SaveAsCommand);
        Bind(_open, vm => vm.OpenInEditorCommand);
        Bind(_execute, vm => vm.ExecuteScriptCommand);
        Texts();
    }

    /// <inheritdoc />
    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        if (_vm is not null)
        {
            _vm.PropertyChanged -= OnChanged;
            _vm.Groups.CollectionChanged -= OnGroupsChanged;
            _vm.CopyRequested -= OnCopyRequested;
        }

        _vm = DataContext as SchemaCompareViewModel;
        if (_vm is not null)
        {
            _vm.PropertyChanged += OnChanged;
            _vm.Groups.CollectionChanged += OnGroupsChanged;
            _vm.CopyRequested += OnCopyRequested;
        }

        SyncAll();
    }

    private static void Run(ICommand command)
    {
        if (command.CanExecute(null))
            command.Execute(null);
    }

    private void Bind(Button button, Func<SchemaCompareViewModel, ICommand> command) =>
        button.Click += (_, _) =>
        {
            if (_vm is { } vm)
                Run(command(vm));
        };

    private KeyBinding Accelerator(Key key, KeyModifiers modifiers, Func<SchemaCompareViewModel, ICommand> command) =>
        new() { Gesture = new KeyGesture(key, modifiers), Command = new DelegateCommand(() => { if (_vm is { } vm) Run(command(vm)); }) };

    private async void OnCopyRequested(string text)
    {
        if (TopLevel.GetTopLevel(this)?.Clipboard is { } clipboard)
            await clipboard.SetTextAsync(text);
    }

    private IBrush Brush(string key) =>
        this.TryFindResource(key, ActualThemeVariant, out var value) && value is IBrush brush ? brush : Brushes.Magenta;

    private void OnGroupsChanged(object? sender, NotifyCollectionChangedEventArgs e) => Dispatcher.UIThread.Post(RebuildList);

    private void OnChanged(object? sender, PropertyChangedEventArgs e) =>
        Dispatcher.UIThread.Post(() =>
        {
            switch (e.PropertyName)
            {
                case nameof(SchemaCompareViewModel.Sources):
                case nameof(SchemaCompareViewModel.Targets):
                case nameof(SchemaCompareViewModel.Source):
                case nameof(SchemaCompareViewModel.Target):
                    SyncEndpoints();
                    SyncChrome();
                    break;
                case nameof(SchemaCompareViewModel.SelectedItem):
                    SyncSelection();
                    break;
                case nameof(SchemaCompareViewModel.Lines):
                    SyncPanes();
                    break;
                case nameof(SchemaCompareViewModel.CurrentDifferenceLine):
                    _sourcePane.Reveal(_vm?.CurrentDifferenceLine ?? -1);
                    _targetPane.Reveal(_vm?.CurrentDifferenceLine ?? -1);
                    break;
                case nameof(SchemaCompareViewModel.Script):
                    SyncScript();
                    break;
                case nameof(SchemaCompareViewModel.IncludedCount):
                case nameof(SchemaCompareViewModel.AllIncluded):
                    SyncIncluded();
                    break;
                default:
                    SyncChrome();
                    break;
            }
        });

    // ---- layout ----

    private Grid BuildLayout()
    {
        var root = new Grid { RowDefinitions = new RowDefinitions("Auto,*") };
        BuildHeader();
        var body = new Grid { ColumnDefinitions = new ColumnDefinitions("270,*") };
        var center = BuildCenter();
        Grid.SetColumn(center, 1);
        body.Children.Add(BuildLeft());
        body.Children.Add(center);
        Grid.SetRow(body, 1);
        root.Children.Add(_header);
        root.Children.Add(body);
        return root;
    }

    private void BuildHeader()
    {
        _sourceChip.Child = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, Children = { _sourceBadge, _sources, _sourcePill } };
        _targetChip.Child = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, Children = { _targetBadge, _targets, _targetPill } };
        _sourcePill.Margin = _targetPill.Margin = new Thickness(0, 0, 8, 0);
        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,Auto,Auto,Auto,*,Auto"), ColumnSpacing = 12 };
        var source = new StackPanel { Spacing = 4, Children = { _sourceLabel, _sourceChip } };
        var target = new StackPanel { Spacing = 4, Children = { _targetLabel, _targetChip } };
        var right = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12, VerticalAlignment = VerticalAlignment.Bottom, Children = { _compared, _compare } };
        Grid.SetColumn(_swap, 1);
        Grid.SetColumn(target, 2);
        Grid.SetColumn(_tabSchema, 3);
        Grid.SetColumn(right, 5);
        foreach (var element in (Control[])[source, _swap, target, _tabSchema, right])
            grid.Children.Add(element);
        _header.Child = grid;
    }

    private Grid BuildLeft()
    {
        var chips = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, Margin = new Thickness(16, 12, 16, 0), Children = { _added, _modified, _removed } };
        // The three chips are wider than the narrow list pane: scroll them sideways instead of cutting the last one.
        var chipScroll = new ScrollViewer { Content = chips, HorizontalScrollBarVisibility = ScrollBarVisibility.Auto, VerticalScrollBarVisibility = ScrollBarVisibility.Disabled };
        _filter.Margin = new Thickness(16, 10, 16, 0);
        _all.Margin = new Thickness(16, 8, 16, 0);
        var scroll = new ScrollViewer { Content = _list, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled, Margin = new Thickness(0, 8, 0, 0) };
        var panel = new Grid { RowDefinitions = new RowDefinitions("Auto,*,Auto") };
        Grid.SetRow(scroll, 1);
        Grid.SetRow(_hint, 2);
        panel.Children.Add(new StackPanel { Children = { chipScroll, _filter, _all } });
        panel.Children.Add(scroll);
        panel.Children.Add(_hint);
        return panel;
    }

    private Grid BuildCenter()
    {
        var grid = new Grid { RowDefinitions = new RowDefinitions("Auto,*,250") };
        _kindBadge.Child = _kindText;
        grid.Children.Add(new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10, Margin = new Thickness(16, 10), Children = { _itemName, _kindBadge, _itemSummary } });

        var panes = new Grid { ColumnDefinitions = new ColumnDefinitions("*,*"), RowDefinitions = new RowDefinitions("Auto,*") };
        Grid.SetColumn(_targetCaption, 1);
        Grid.SetRow(_sourcePane, 1);
        Grid.SetRow(_targetPane, 1);
        Grid.SetColumn(_targetPane, 1);
        foreach (var element in (Control[])[_sourceCaption, _targetCaption, _sourcePane, _targetPane])
            panes.Children.Add(element);
        Grid.SetRow(panes, 1);
        grid.Children.Add(panes);
        Grid.SetRow(_empty, 1);
        grid.Children.Add(_empty);

        var script = BuildScript();
        Grid.SetRow(script, 2);
        grid.Children.Add(script);
        return grid;
    }

    private Grid BuildScript()
    {
        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*") };
        grid.Children.Add(_scriptStripe);
        var column = new Grid { RowDefinitions = new RowDefinitions("Auto,Auto,*") };
        var bar = new Grid { Margin = new Thickness(16, 8), ColumnSpacing = 8, ColumnDefinitions = new ColumnDefinitions("*,Auto,Auto,Auto,Auto,Auto") };
        bar.Children.Add(new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10, Children = { _scriptTitle, _scriptInfo } });
        var buttons = new Button[] { _regenerate, _copy, _save, _open, _execute };
        for (var i = 0; i < buttons.Length; i++)
        {
            Grid.SetColumn(buttons[i], 1 + i);
            bar.Children.Add(buttons[i]);
        }

        column.Children.Add(bar);
        Grid.SetRow(_message, 1);
        column.Children.Add(_message);
        Grid.SetRow(_script.Control, 2);
        column.Children.Add(_script.Control);
        Grid.SetColumn(column, 1);
        grid.Children.Add(column);
        return grid;
    }

    // ---- texts and colors ----

    private void Texts()
    {
        _sourceLabel.Text = UiText.Get("compare.source");
        _targetLabel.Text = UiText.Get("compare.target");
        _tabSchema.Text = UiText.Get("compare.tabSchema");
        _compare.Content = UiText.Get("compare.compareAgain");
        ToolTip.SetTip(_swap, UiText.Get("compare.swap"));
        _filter.PlaceholderText = UiText.Get("compare.filterObjects");
        _scriptTitle.Text = UiText.Get("compare.scriptTitle");
        _regenerate.Content = UiText.Get("compare.regenerate");
        _copy.Content = UiText.Get("compare.copy");
        _save.Content = UiText.Get("compare.saveSql");
        _open.Content = UiText.Get("compare.openEditor");
        _hint.Text = $"F7 {UiText.Get("compare.hint.next")} · Maj+F7 {UiText.Get("compare.hint.previous")}";
    }

    private void Paint()
    {
        _header.Background = Brush("SquillBgSurfaceBrush");
        _list.Background = Brush("SquillBgSurfaceBrush");
        _compared.Foreground = Brush("SquillFg2Brush");
        _tabSchema.Foreground = Brush("SquillAccentTextBrush");
        _sourceLabel.Foreground = _targetLabel.Foreground = _sourceCaption.Foreground = _targetCaption.Foreground = Brush("SquillFg2Brush");
        _itemSummary.Foreground = _scriptInfo.Foreground = _hint.Foreground = _empty.Foreground = _scriptTitle.Foreground = Brush("SquillFg2Brush");
        _itemName.Foreground = Brush("SquillFgBrush");
        _sourceChip.Background = _targetChip.Background = Brush("SquillBgRaisedBrush");
        SyncEndpoints();
        SyncChrome();
    }

    private void PaintEndpoint(Border badge, Border pill, CompareEndpoint? endpoint)
    {
        badge.Background = Brush("SquillAccentSoftBrush");
        var letters = (TextBlock)badge.Child!;
        letters.Foreground = Brush("SquillAccentTextBrush");
        letters.Text = endpoint is null ? "" : CompareFormat.EngineLetters(endpoint.Dialect);
        if (endpoint is not null && CompareFormat.Environment(endpoint.Environment) is var (suffix, key))
        {
            pill.IsVisible = true;
            pill.Background = Brush($"SquillEnv{suffix}SoftBrush");
            pill.Child = new TextBlock { Text = UiText.Get(key), FontSize = 11, FontWeight = FontWeight.Bold, Foreground = Brush($"SquillEnv{suffix}Brush") };
        }
        else
        {
            pill.IsVisible = false;
        }
    }

    private IBrush EnvironmentBrush(ConnectionEnvironment environment) =>
        Brush(CompareFormat.Environment(environment) is var (suffix, _) ? $"SquillEnv{suffix}Brush" : "SquillBorderBrush");

    private void PaintChip(ToggleButton chip, string text, string color, bool on)
    {
        chip.Content = text;
        chip.IsChecked = on;
        chip.Foreground = Brush(color);
        chip.BorderBrush = Brush(color);
        chip.BorderThickness = new Thickness(1);
        chip.CornerRadius = new CornerRadius(14);
        chip.Padding = new Thickness(10, 3);
    }

    // ---- syncing ----

    private void SyncAll()
    {
        Paint();
        RebuildList();
        SyncSelection();
        SyncPanes();
        SyncScript();
    }

    private void SyncEndpoints()
    {
        if (_vm is not { } vm)
            return;
        _syncing = true;
        Fill(_sources, vm.Sources, vm.Source);
        Fill(_targets, vm.Targets, vm.Target);
        _syncing = false;
        PaintEndpoint(_sourceBadge, _sourcePill, vm.Source);
        PaintEndpoint(_targetBadge, _targetPill, vm.Target);
        _sourceChip.BorderBrush = vm.Source is { } s ? EnvironmentBrush(s.Environment) : Brush("SquillBorderBrush");
        _targetChip.BorderBrush = vm.Target is { } t ? EnvironmentBrush(t.Environment) : Brush("SquillBorderBrush");
    }

    private static void Fill(ComboBox box, IReadOnlyList<CompareEndpoint> endpoints, CompareEndpoint? selected)
    {
        box.Items.Clear();
        foreach (var endpoint in endpoints)
            box.Items.Add(endpoint.Label);
        box.SelectedIndex = selected is null ? -1 : endpoints.ToList().IndexOf(selected);
    }

    private void SyncChrome()
    {
        if (_vm is not { } vm)
            return;
        _syncing = true;
        _compared.Text = vm.State == CompareState.Comparing ? UiText.Get("compare.comparing") : vm.ComparedText;
        _all.Content = vm.SelectionText;
        _scriptInfo.Text = UiText.Get(vm.IsScriptEdited ? "compare.scriptEdited" : "compare.scriptEditable");
        _sourceCaption.Text = vm.SourceCaption;
        _targetCaption.Text = vm.TargetCaption;
        var production = vm.Environment == ConnectionEnvironment.Production;
        _execute.Content = vm.IsExecuting ? UiText.Get("compare.running")
            : production ? UiText.Format("compare.executeIn", UiText.Get("status.production")) : UiText.Get("compare.execute");
        _execute.Classes.Set("primary", !production);
        _execute.Classes.Set("outline-danger", production);
        _execute.IsEnabled = vm.ExecuteScriptCommand.CanExecute(null);
        _compare.IsEnabled = vm.CompareCommand.CanExecute(null);
        _regenerate.IsVisible = vm.IsScriptEdited;
        _scriptStripe.Background = EnvironmentBrush(vm.Environment);
        PaintChip(_added, "+ " + UiText.Format("compare.chip.added", vm.AddedCount), "SquillSuccessBrush", vm.ShowAdded);
        PaintChip(_modified, "~ " + UiText.Format("compare.chip.modified", vm.ModifiedCount), "SquillAccentTextBrush", vm.ShowModified);
        PaintChip(_removed, "- " + UiText.Format("compare.chip.removed", vm.RemovedCount), "SquillDangerBrush", vm.ShowRemoved);
        var (text, color) = vm.ExecuteError.Length > 0 ? (vm.ExecuteError, "SquillDangerBrush")
            : vm.ExecuteSummary.Length > 0 ? (vm.ExecuteSummary, "SquillSuccessBrush")
            : vm.FailureText.Length > 0 ? (vm.FailureText, "SquillDangerBrush")
            : (string.Join("\n", vm.ScriptNotes), "SquillWarningBrush");
        _message.Text = text;
        _message.Foreground = Brush(color);
        _message.IsVisible = text.Length > 0;
        _empty.Text = vm.State switch
        {
            CompareState.Ready when vm.TotalCount == 0 => UiText.Get("compare.noDifferences"),
            CompareState.Ready => UiText.Get("compare.pickItem"),
            _ => UiText.Get("compare.pickEndpoints"),
        };
        _empty.IsVisible = vm.SelectedItem is null;
        _syncing = false;
    }

    private void RebuildList()
    {
        _list.Children.Clear();
        _rows.Clear();
        if (_vm is not { } vm)
            return;
        var muted = Brush("SquillFg2Brush");
        foreach (var group in vm.Groups)
        {
            var title = new Grid { Margin = new Thickness(16, 10, 16, 4) };
            title.Children.Add(new TextBlock { Text = group.Title, FontSize = 11, FontWeight = FontWeight.SemiBold, Foreground = muted });
            title.Children.Add(new TextBlock { Text = group.Count.ToString(CultureInfo.InvariantCulture), FontSize = 11, HorizontalAlignment = HorizontalAlignment.Right, Foreground = muted });
            _list.Children.Add(title);
            foreach (var item in group.Items)
                _list.Children.Add(Row(item));
        }

        SyncSelection();
    }

    private Border Row(CompareItemViewModel item)
    {
        var box = new CheckBox { IsChecked = item.IsIncluded, MinWidth = 0, Padding = new Thickness(0), VerticalAlignment = VerticalAlignment.Center };
        box.Click += (_, _) => item.IsIncluded = box.IsChecked == true;
        var glyphBrush = item.Difference.Kind switch
        {
            DifferenceKind.Added => "SquillSuccessBrush",
            DifferenceKind.Removed => "SquillDangerBrush",
            _ => "SquillAccentTextBrush",
        };
        var muted = Brush("SquillFg2Brush");
        var text = Brush("SquillFgBrush");
        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("28,Auto,*,Auto"), ColumnSpacing = 6 };
        var glyph = new TextBlock { Text = item.Glyph, Foreground = Brush(glyphBrush), FontWeight = FontWeight.Bold, VerticalAlignment = VerticalAlignment.Center };
        var name = new TextBlock { Text = item.Name, FontFamily = Mono, TextTrimming = TextTrimming.CharacterEllipsis, VerticalAlignment = VerticalAlignment.Center, Foreground = text };
        var summary = new TextBlock { Text = item.Summary, FontSize = 12, VerticalAlignment = VerticalAlignment.Center, Foreground = muted };
        Grid.SetColumn(glyph, 1);
        Grid.SetColumn(name, 2);
        Grid.SetColumn(summary, 3);
        foreach (var element in (Control[])[box, glyph, name, summary])
            grid.Children.Add(element);
        var border = new Border { Child = grid, Padding = new Thickness(16, 6), Background = Brushes.Transparent };
        border.PointerPressed += (_, e) =>
        {
            if (e.Source is not Visual visual || !ReferenceEquals(visual, box) && !visual.GetVisualAncestors().Contains(box))
                _vm!.SelectedItem = item;
        };
        _rows[item] = (box, border);
        return border;
    }

    private void SyncIncluded()
    {
        foreach (var (item, (box, _)) in _rows)
            box.IsChecked = item.IsIncluded;
        _all.IsChecked = _vm?.AllIncluded;
        SyncChrome();
    }

    private void SyncSelection()
    {
        var selected = _vm?.SelectedItem;
        foreach (var (item, (_, row)) in _rows)
            row.Background = ReferenceEquals(item, selected) ? Brush("SquillSelectionBrush") : Brushes.Transparent;
        _itemName.Text = selected?.Name ?? "";
        _kindBadge.IsVisible = _itemSummary.IsVisible = selected is not null;
        if (selected is not null)
        {
            _kindText.Text = selected.KindText;
            var (back, fore) = selected.Difference.Kind switch
            {
                DifferenceKind.Added => ("SquillCellAddedBrush", "SquillSuccessBrush"),
                DifferenceKind.Removed => ("SquillCellDeletedBrush", "SquillDangerBrush"),
                _ => ("SquillAccentSoftBrush", "SquillAccentTextBrush"),
            };
            _kindBadge.Background = Brush(back);
            _kindText.Foreground = Brush(fore);
            _itemSummary.Text = selected.Summary;
        }

        _all.IsChecked = _vm?.AllIncluded;
        SyncChrome();
    }

    private void SyncPanes()
    {
        var lines = _vm?.Lines ?? [];
        _sourcePane.Lines = lines;
        _targetPane.Lines = lines;
        _sourcePane.IsVisible = _targetPane.IsVisible = _sourceCaption.IsVisible = _targetCaption.IsVisible = lines.Count > 0;
    }

    private void SyncScript()
    {
        if (_vm is not { } vm)
            return;
        _syncing = true;
        if (_script.Text != vm.Script)
            _script.Text = vm.Script;
        if (vm.Source is { } source)
            _script.SetDialect(source.Dialect);
        _syncing = false;
        SyncChrome();
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
