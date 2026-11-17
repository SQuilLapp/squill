using System.ComponentModel;
using System.Globalization;
using System.Windows.Input;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Squill.App.WinUI.Controls;
using Squill.App.WinUI.Editors;
using Squill.Core;
using Squill.Core.Schema.Compare;
using Squill.Presentation.Compare;
using Squill.Presentation.Editor;
using Squill.Presentation.Localization;
using Windows.System;
using ColumnDefinition = Microsoft.UI.Xaml.Controls.ColumnDefinition;
using RowDefinition = Microsoft.UI.Xaml.Controls.RowDefinition;

namespace Squill.App.WinUI.Views;

/// <summary>The schema compare tab (mockup M09): endpoint chips, the difference list, the two DDL panes and the migration script, all built in code from <see cref="SchemaCompareViewModel"/>.</summary>
public sealed partial class SchemaCompareView : UserControl
{
    private readonly Grid _header = new() { ColumnSpacing = 12, Padding = new Thickness(16, 8, 16, 8) };
    private readonly Border _sourceChip = new() { BorderThickness = new Thickness(3, 1, 1, 1), CornerRadius = new CornerRadius(6) };
    private readonly Border _targetChip = new() { BorderThickness = new Thickness(3, 1, 1, 1), CornerRadius = new CornerRadius(6) };
    private readonly ComboBox _sources = new() { BorderThickness = new Thickness(0), Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent), MinWidth = 150 };
    private readonly ComboBox _targets = new() { BorderThickness = new Thickness(0), Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent), MinWidth = 150 };
    private readonly Button _swap = new() { Content = "⇄", Padding = new Thickness(10, 6, 10, 6) };
    private readonly TextBlock _tabSchema = new() { FontWeight = FontWeights.SemiBold, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(12, 0, 12, 0) };
    private readonly TextBlock _compared = new() { VerticalAlignment = VerticalAlignment.Center };
    private readonly Button _compare = new() { VerticalAlignment = VerticalAlignment.Center };
    private readonly TextBlock _sourceLabel = new() { FontSize = 11, FontWeight = FontWeights.SemiBold };
    private readonly TextBlock _targetLabel = new() { FontSize = 11, FontWeight = FontWeights.SemiBold };
    private readonly Border _sourcePill = new() { Padding = new Thickness(6, 1, 6, 1), CornerRadius = new CornerRadius(4), VerticalAlignment = VerticalAlignment.Center, Visibility = Visibility.Collapsed };
    private readonly Border _targetPill = new() { Padding = new Thickness(6, 1, 6, 1), CornerRadius = new CornerRadius(4), VerticalAlignment = VerticalAlignment.Center, Visibility = Visibility.Collapsed };
    private readonly Border _sourceBadge = new() { Padding = new Thickness(4, 2, 4, 2), CornerRadius = new CornerRadius(4), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(8, 0, 0, 0), Child = new TextBlock { FontSize = 11, FontWeight = FontWeights.Bold } };
    private readonly Border _targetBadge = new() { Padding = new Thickness(4, 2, 4, 2), CornerRadius = new CornerRadius(4), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(8, 0, 0, 0), Child = new TextBlock { FontSize = 11, FontWeight = FontWeights.Bold } };

    private readonly ToggleButton _added = new();
    private readonly ToggleButton _modified = new();
    private readonly ToggleButton _removed = new();
    private readonly TextBox _filter = new();
    private readonly CheckBox _all = new() { IsThreeState = true };
    private readonly StackPanel _list = new();
    private readonly Dictionary<CompareItemViewModel, (CheckBox Box, Border Row)> _rows = [];

    private readonly TextBlock _itemName = new() { FontFamily = (FontFamily)Application.Current.Resources["SquillFontMono"], FontWeight = FontWeights.SemiBold, VerticalAlignment = VerticalAlignment.Center };
    private readonly Border _kindBadge = new() { Padding = new Thickness(6, 1, 6, 1), CornerRadius = new CornerRadius(4), VerticalAlignment = VerticalAlignment.Center };
    private readonly TextBlock _kindText = new() { FontSize = 11, FontWeight = FontWeights.SemiBold };
    private readonly TextBlock _itemSummary = new() { VerticalAlignment = VerticalAlignment.Center };
    private readonly TextBlock _sourceCaption = new() { Margin = new Thickness(16, 6, 0, 6) };
    private readonly TextBlock _targetCaption = new() { Margin = new Thickness(16, 6, 0, 6) };
    private readonly DdlPane _sourcePane = new(sourceSide: true);
    private readonly DdlPane _targetPane = new(sourceSide: false);
    private readonly TextBlock _empty = new() { HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, TextWrapping = TextWrapping.Wrap, TextAlignment = TextAlignment.Center, MaxWidth = 420 };

    private readonly TextBlock _scriptTitle = new() { FontWeight = FontWeights.SemiBold, VerticalAlignment = VerticalAlignment.Center };
    private readonly TextBlock _scriptInfo = new() { VerticalAlignment = VerticalAlignment.Center };
    private readonly Button _regenerate = new();
    private readonly Button _copy = new();
    private readonly Button _save = new();
    private readonly Button _open = new();
    private readonly Button _execute = new();
    private readonly TextBlock _message = new() { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(16, 0, 16, 6), Visibility = Visibility.Collapsed };
    private readonly Border _scriptStripe = new() { Width = 3 };
    private readonly ISqlEditor _script = EditorFactory.Create();
    private readonly TextBlock _hint = new() { Margin = new Thickness(16, 8, 16, 8), TextWrapping = TextWrapping.Wrap };

    private SchemaCompareViewModel? _vm;
    private bool _syncing;

    /// <summary>Creates the view; set <see cref="ViewModel"/> to draw it.</summary>
    public SchemaCompareView()
    {
        HorizontalContentAlignment = HorizontalAlignment.Stretch;
        VerticalContentAlignment = VerticalAlignment.Stretch;
        Content = BuildLayout();
        ActualThemeChanged += (_, _) => Paint();
        AddAccelerator(VirtualKey.F7, VirtualKeyModifiers.None, vm => vm.NextDifferenceCommand);
        AddAccelerator(VirtualKey.F7, VirtualKeyModifiers.Shift, vm => vm.PreviousDifferenceCommand);
        AddAccelerator(VirtualKey.S, VirtualKeyModifiers.Control, vm => vm.SaveAsCommand);
        AddAccelerator(VirtualKey.Enter, VirtualKeyModifiers.Control, vm => vm.ExecuteScriptCommand);
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
        _added.Click += (_, _) => { if (_vm is { } vm) vm.ShowAdded = _added.IsChecked == true; };
        _modified.Click += (_, _) => { if (_vm is { } vm) vm.ShowModified = _modified.IsChecked == true; };
        _removed.Click += (_, _) => { if (_vm is { } vm) vm.ShowRemoved = _removed.IsChecked == true; };
        _filter.TextChanged += (_, _) => { if (!_syncing && _vm is { } vm) vm.FilterText = _filter.Text; };
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
    }

    /// <summary>Gets or sets the comparison drawn; the view redraws every part.</summary>
    public SchemaCompareViewModel? ViewModel
    {
        get => _vm;
        set
        {
            if (ReferenceEquals(_vm, value))
                return;
            if (_vm is not null)
            {
                _vm.PropertyChanged -= OnChanged;
                _vm.Groups.CollectionChanged -= OnGroupsChanged;
            }

            _vm = value;
            if (_vm is not null)
            {
                _vm.PropertyChanged += OnChanged;
                _vm.Groups.CollectionChanged += OnGroupsChanged;
            }

            Texts();
            SyncAll();
        }
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

    private void AddAccelerator(VirtualKey key, VirtualKeyModifiers modifiers, Func<SchemaCompareViewModel, ICommand> command)
    {
        var accelerator = new KeyboardAccelerator { Key = key, Modifiers = modifiers };
        accelerator.Invoked += (_, args) =>
        {
            if (_vm is { } vm)
            {
                Run(command(vm));
                args.Handled = true;
            }
        };
        KeyboardAccelerators.Add(accelerator);
    }

    private Brush Brush(string key) => Tokens.Brush(this, key);

    private void OnGroupsChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e) => DispatcherQueue.TryEnqueue(RebuildList);

    private void OnChanged(object? sender, PropertyChangedEventArgs e) =>
        DispatcherQueue.TryEnqueue(() =>
        {
            switch (e.PropertyName)
            {
                case nameof(SchemaCompareViewModel.Sources):
                case nameof(SchemaCompareViewModel.Targets):
                case nameof(SchemaCompareViewModel.Source):
                case nameof(SchemaCompareViewModel.Target):
                    SyncEndpoints();
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
        var root = new Grid();
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition());
        BuildHeader();
        root.Children.Add(_header);
        var body = new Grid();
        body.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(270) });
        body.ColumnDefinitions.Add(new ColumnDefinition());
        var left = BuildLeft();
        var center = BuildCenter();
        Grid.SetColumn(center, 1);
        body.Children.Add(left);
        body.Children.Add(center);
        Grid.SetRow(body, 1);
        root.Children.Add(body);
        return root;
    }

    private void BuildHeader()
    {
        foreach (var width in (GridLength[])[GridLength.Auto, GridLength.Auto, GridLength.Auto, GridLength.Auto, new GridLength(1, GridUnitType.Star), GridLength.Auto])
            _header.ColumnDefinitions.Add(new ColumnDefinition { Width = width });
        _sourceChip.Child = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, Children = { _sourceBadge, _sources, _sourcePill } };
        _targetChip.Child = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, Children = { _targetBadge, _targets, _targetPill } };
        _sourcePill.Margin = _targetPill.Margin = new Thickness(0, 0, 8, 0);
        var source = new StackPanel { Spacing = 4, Children = { _sourceLabel, _sourceChip } };
        var target = new StackPanel { Spacing = 4, Children = { _targetLabel, _targetChip } };
        var swap = new StackPanel { VerticalAlignment = VerticalAlignment.Bottom, Children = { _swap } };
        var tab = new StackPanel { VerticalAlignment = VerticalAlignment.Bottom, Margin = new Thickness(8, 0, 0, 8), Children = { _tabSchema } };
        var right = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12, VerticalAlignment = VerticalAlignment.Bottom, Children = { _compared, _compare } };
        Grid.SetColumn(swap, 1);
        Grid.SetColumn(target, 2);
        Grid.SetColumn(tab, 3);
        Grid.SetColumn(right, 5);
        foreach (var element in (UIElement[])[source, swap, target, tab, right])
            _header.Children.Add(element);
    }

    private Grid BuildLeft()
    {
        var chips = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, Margin = new Thickness(16, 12, 16, 0), Children = { _added, _modified, _removed } };
        _filter.Margin = new Thickness(16, 10, 16, 0);
        _all.Margin = new Thickness(16, 8, 16, 0);
        var scroll = new ScrollViewer { Content = _list, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled, Margin = new Thickness(0, 8, 0, 0) };
        var panel = new Grid();
        panel.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        panel.RowDefinitions.Add(new RowDefinition());
        panel.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        panel.Children.Add(new StackPanel { Children = { chips, _filter, _all } });
        Grid.SetRow(scroll, 1);
        Grid.SetRow(_hint, 2);
        panel.Children.Add(scroll);
        panel.Children.Add(_hint);
        return panel;
    }

    private Grid BuildCenter()
    {
        var grid = new Grid();
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        grid.RowDefinitions.Add(new RowDefinition());
        grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(250) });
        _kindBadge.Child = _kindText;
        grid.Children.Add(new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10, Margin = new Thickness(16, 10, 16, 10), Children = { _itemName, _kindBadge, _itemSummary } });

        var panes = new Grid();
        panes.ColumnDefinitions.Add(new ColumnDefinition());
        panes.ColumnDefinitions.Add(new ColumnDefinition());
        panes.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        panes.RowDefinitions.Add(new RowDefinition());
        Grid.SetColumn(_targetCaption, 1);
        Grid.SetRow(_sourcePane, 1);
        Grid.SetRow(_targetPane, 1);
        Grid.SetColumn(_targetPane, 1);
        foreach (var element in (UIElement[])[_sourceCaption, _targetCaption, _sourcePane, _targetPane])
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
        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition());
        grid.Children.Add(_scriptStripe);
        var column = new Grid();
        column.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        column.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        column.RowDefinitions.Add(new RowDefinition());
        var bar = new Grid { Margin = new Thickness(16, 8, 16, 8), ColumnSpacing = 8 };
        bar.ColumnDefinitions.Add(new ColumnDefinition());
        for (var i = 0; i < 5; i++)
            bar.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
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
        var editor = ((IEditorControl)_script).Control;
        Grid.SetRow(editor, 2);
        column.Children.Add(editor);
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
        ToolTipService.SetToolTip(_swap, UiText.Get("compare.swap"));
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
        _sourceLabel.Foreground = _targetLabel.Foreground = Brush("SquillFg2Brush");
        _sourceCaption.Foreground = _targetCaption.Foreground = Brush("SquillFg2Brush");
        _itemSummary.Foreground = _scriptInfo.Foreground = _hint.Foreground = _empty.Foreground = _scriptTitle.Foreground = Brush("SquillFg2Brush");
        _itemName.Foreground = Brush("SquillFgBrush");
        foreach (var chip in (Border[])[_sourceChip, _targetChip])
            chip.Background = Brush("SquillBgRaisedBrush");
        SyncEndpoints();
        SyncChrome();
    }

    private void PaintEndpoint(Border badge, Border pill, CompareEndpoint? endpoint)
    {
        badge.Background = Brush("SquillAccentSoftBrush");
        var letters = (TextBlock)badge.Child;
        letters.Foreground = Brush("SquillAccentTextBrush");
        letters.Text = endpoint is null ? "" : CompareFormat.EngineLetters(endpoint.Dialect);
        if (endpoint is not null && ChromeFormat.Environment(endpoint.Environment) is var (suffix, key, _))
        {
            pill.Visibility = Visibility.Visible;
            pill.Background = Brush($"SquillEnv{suffix}SoftBrush");
            pill.Child = new TextBlock { Text = UiText.Get(key), FontSize = 11, FontWeight = FontWeights.Bold, Foreground = Brush($"SquillEnv{suffix}Brush") };
        }
        else
        {
            pill.Visibility = Visibility.Collapsed;
        }
    }

    private Brush EnvironmentBrush(ConnectionEnvironment environment) =>
        Brush(ChromeFormat.Environment(environment) is var (suffix, _, _) ? $"SquillEnv{suffix}Brush" : "SquillBorderBrush");

    private void PaintChip(ToggleButton chip, string text, string color, bool on)
    {
        chip.Content = text;
        chip.IsChecked = on;
        chip.Foreground = Brush(color);
        chip.BorderBrush = Brush(color);
        chip.BorderThickness = new Thickness(1);
        chip.CornerRadius = new CornerRadius(14);
        chip.Padding = new Thickness(10, 3, 10, 3);
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

    // The combo boxes get plain strings: never a list of objects (the AOT list-binding trap).
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
        _compared.Text = vm.State == CompareState.Comparing ? UiText.Get("compare.comparing") : vm.ComparedText;
        _all.Content = vm.SelectionText;
        _scriptInfo.Text = UiText.Get(vm.IsScriptEdited ? "compare.scriptEdited" : "compare.scriptEditable");
        _sourceCaption.Text = vm.SourceCaption;
        _targetCaption.Text = vm.TargetCaption;
        var production = vm.Environment == ConnectionEnvironment.Production;
        _execute.Content = vm.IsExecuting ? UiText.Get("compare.running")
            : production ? UiText.Format("compare.executeIn", UiText.Get("status.production")) : UiText.Get("compare.execute");
        _execute.Background = Brush(production ? "SquillDangerBrush" : "SquillAccentBrush");
        _execute.Foreground = Brush(production ? "SquillOnEnvProdBrush" : "SquillOnAccentBrush");
        _execute.IsEnabled = vm.ExecuteScriptCommand.CanExecute(null);
        _compare.IsEnabled = vm.CompareCommand.CanExecute(null);
        _regenerate.Visibility = vm.IsScriptEdited ? Visibility.Visible : Visibility.Collapsed;
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
        _message.Visibility = text.Length == 0 ? Visibility.Collapsed : Visibility.Visible;
        _empty.Text = vm.State switch
        {
            CompareState.Ready when vm.TotalCount == 0 => UiText.Get("compare.noDifferences"),
            CompareState.Ready => UiText.Get("compare.pickItem"),
            _ => UiText.Get("compare.pickEndpoints"),
        };
        _empty.Visibility = vm.SelectedItem is null ? Visibility.Visible : Visibility.Collapsed;
    }

    private void RebuildList()
    {
        _list.Children.Clear();
        _rows.Clear();
        if (_vm is not { } vm)
            return;
        foreach (var group in vm.Groups)
        {
            var title = new Grid { Margin = new Thickness(16, 10, 16, 4) };
            title.Children.Add(new TextBlock { Text = group.Title, FontSize = 11, FontWeight = FontWeights.SemiBold, Foreground = Brush("SquillFg2Brush") });
            title.Children.Add(new TextBlock { Text = group.Count.ToString(CultureInfo.InvariantCulture), FontSize = 11, HorizontalAlignment = HorizontalAlignment.Right, Foreground = Brush("SquillFg2Brush") });
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
        var grid = new Grid { ColumnSpacing = 6 };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(28) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition());
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var glyph = new TextBlock { Text = item.Glyph, Foreground = Brush(glyphBrush), FontWeight = FontWeights.Bold, VerticalAlignment = VerticalAlignment.Center };
        var name = new TextBlock { Text = item.Name, FontFamily = (FontFamily)Application.Current.Resources["SquillFontMono"], TextTrimming = TextTrimming.CharacterEllipsis, VerticalAlignment = VerticalAlignment.Center, Foreground = Brush("SquillFgBrush") };
        var summary = new TextBlock { Text = item.Summary, FontSize = 12, VerticalAlignment = VerticalAlignment.Center, Foreground = Brush("SquillFg2Brush") };
        Grid.SetColumn(glyph, 1);
        Grid.SetColumn(name, 2);
        Grid.SetColumn(summary, 3);
        foreach (var element in (UIElement[])[box, glyph, name, summary])
            grid.Children.Add(element);
        var border = new Border { Child = grid, Padding = new Thickness(16, 6, 16, 6) };
        border.Tapped += (_, e) =>
        {
            if (!IsInside(e.OriginalSource as DependencyObject, box))
                _vm!.SelectedItem = item;
        };
        _rows[item] = (box, border);
        return border;
    }

    private static bool IsInside(DependencyObject? node, DependencyObject ancestor)
    {
        for (; node is not null; node = Microsoft.UI.Xaml.Media.VisualTreeHelper.GetParent(node))
        {
            if (ReferenceEquals(node, ancestor))
                return true;
        }

        return false;
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
            row.Background = ReferenceEquals(item, selected) ? Brush("SquillSelectionBrush") : null;
        _itemName.Text = selected?.Name ?? "";
        _kindBadge.Visibility = _itemSummary.Visibility = selected is null ? Visibility.Collapsed : Visibility.Visible;
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
        var show = lines.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        _sourcePane.Visibility = _targetPane.Visibility = _sourceCaption.Visibility = _targetCaption.Visibility = show;
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
}
