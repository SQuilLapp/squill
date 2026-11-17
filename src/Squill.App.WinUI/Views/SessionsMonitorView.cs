using System.ComponentModel;
using System.Globalization;
using System.Windows.Input;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using Squill.Presentation.Activity;
using Squill.Presentation.Localization;
using Windows.Foundation;
using Windows.System;
using ColumnDefinition = Microsoft.UI.Xaml.Controls.ColumnDefinition;
using RowDefinition = Microsoft.UI.Xaml.Controls.RowDefinition;

namespace Squill.App.WinUI.Views;

/// <summary>The sessions and locks tab (mockup M12), built in code from <see cref="SessionsMonitorViewModel"/>: header, four tiles, filter tabs, the session table, the detail pane and the blocking chains.</summary>
public sealed partial class SessionsMonitorView : UserControl
{
    // Column widths of the session table; the last one (query) takes the rest.
    private static readonly double[] ColumnWidths = [72, 120, 160, 130, 180, 100, 150];

    private static readonly SessionSortColumn[] Columns =
    [
        SessionSortColumn.Pid, SessionSortColumn.User, SessionSortColumn.Application, SessionSortColumn.Client,
        SessionSortColumn.State, SessionSortColumn.Duration, SessionSortColumn.Wait, SessionSortColumn.Query,
    ];

    private static readonly string[] ColumnKeys =
        ["activity.col.pid", "activity.col.user", "activity.col.application", "activity.col.client", "activity.col.state", "activity.col.duration", "activity.col.wait", "activity.col.query"];

    // shortcut: only the first rows are built as elements (no virtualization); the filter box narrows the rest, upgrade to a Win2D canvas if a server routinely lists more.
    private const int MaxRows = 400;

    private readonly Border _topLine = new() { Height = 2 };
    private readonly Border _header = new() { Padding = new Thickness(24, 14, 24, 12), BorderThickness = new Thickness(0, 0, 0, 1) };
    private readonly TextBlock _title = new() { FontSize = 20, FontWeight = FontWeights.SemiBold, VerticalAlignment = VerticalAlignment.Center };
    private readonly TextBlock _pulse = new() { Text = "∿", FontSize = 22, VerticalAlignment = VerticalAlignment.Center };
    private readonly Border _engine = new() { Padding = new Thickness(5, 2, 5, 2), CornerRadius = new CornerRadius(4), VerticalAlignment = VerticalAlignment.Center, Child = new TextBlock { FontSize = 11, FontWeight = FontWeights.Bold } };
    private readonly TextBlock _where = new() { VerticalAlignment = VerticalAlignment.Center };
    private readonly Border _envPill = new() { Padding = new Thickness(6, 1, 6, 1), CornerRadius = new CornerRadius(4), VerticalAlignment = VerticalAlignment.Center, Visibility = Visibility.Collapsed };
    private readonly Ellipse _refreshDot = new() { Width = 8, Height = 8, VerticalAlignment = VerticalAlignment.Center };
    private readonly TextBlock _refresh = new() { VerticalAlignment = VerticalAlignment.Center };
    private readonly Button _pause = new() { VerticalAlignment = VerticalAlignment.Center };
    private readonly TextBox _filter = new() { Width = 260, VerticalAlignment = VerticalAlignment.Center };

    private readonly Border _connectionsTile = new();
    private readonly Border _transactionsTile = new();
    private readonly Border _longTile = new();
    private readonly Border _locksTile = new();
    private readonly TextBlock _connectionsValue = new();
    private readonly ProgressBar _connectionsBar = new() { Minimum = 0, Maximum = 1, Height = 4, Margin = new Thickness(0, 10, 0, 0) };
    private readonly TextBlock _tps = new();
    private readonly Polyline _spark = new() { StrokeThickness = 1.5, Stretch = Stretch.Fill, Height = 36, Margin = new Thickness(0, 6, 0, 0) };
    private readonly TextBlock _longValue = new();
    private readonly TextBlock _longSub = new();
    private readonly TextBlock _locksValue = new();
    private readonly TextBlock _locksSub = new();

    private readonly ToggleButton[] _filters = [new(), new(), new(), new()];
    private readonly CheckBox _hideSystem = new() { MinWidth = 0, HorizontalAlignment = HorizontalAlignment.Right };
    private readonly Grid _tableHeader = new();
    private readonly StackPanel _rows = new();
    private readonly Dictionary<string, Border> _rowElements = [];
    private readonly TextBlock _error = new() { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(16, 8, 16, 8), Visibility = Visibility.Collapsed };

    private readonly TextBlock _sessionLabel = new() { FontSize = 11, FontWeight = FontWeights.SemiBold, VerticalAlignment = VerticalAlignment.Center };
    private readonly TextBlock _sessionId = new() { FontSize = 18, FontWeight = FontWeights.SemiBold, VerticalAlignment = VerticalAlignment.Center };
    private readonly Border _stateBadge = new() { Padding = new Thickness(6, 2, 6, 2), CornerRadius = new CornerRadius(4), HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Center, Child = new TextBlock { FontSize = 12 } };
    private readonly StackPanel _fields = new() { Spacing = 8 };
    private readonly TextBlock _lastQueryLabel = new() { FontWeight = FontWeights.SemiBold, VerticalAlignment = VerticalAlignment.Center };
    private readonly Button _copy = new() { Padding = new Thickness(6, 2, 6, 2), Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent), BorderThickness = new Thickness(0) };
    private readonly Border _queryBox = new() { Padding = new Thickness(12), CornerRadius = new CornerRadius(6), BorderThickness = new Thickness(1) };
    private readonly TextBlock _query = new() { TextWrapping = TextWrapping.Wrap, IsTextSelectionEnabled = true };
    private readonly Border _noteBox = new() { Padding = new Thickness(12), CornerRadius = new CornerRadius(6), Visibility = Visibility.Collapsed };
    private readonly TextBlock _note = new() { TextWrapping = TextWrapping.Wrap };
    private readonly TextBlock _gone = new() { TextWrapping = TextWrapping.Wrap, Visibility = Visibility.Collapsed };
    private readonly Button _cancel = new() { HorizontalAlignment = HorizontalAlignment.Stretch };
    private readonly Button _terminate = new() { HorizontalAlignment = HorizontalAlignment.Stretch };
    private readonly TextBlock _productionHint = new() { TextAlignment = TextAlignment.Center, FontSize = 12, TextWrapping = TextWrapping.Wrap };
    private readonly StackPanel _detailBody = new() { Spacing = 14 };

    private readonly TextBlock _chainsTitle = new() { FontWeight = FontWeights.SemiBold, VerticalAlignment = VerticalAlignment.Center };
    private readonly TextBlock _chainsSummary = new() { VerticalAlignment = VerticalAlignment.Center };
    private readonly StackPanel _chains = new() { Spacing = 2, Margin = new Thickness(12, 8, 12, 8) };
    private readonly HashSet<string> _collapsed = [];

    private readonly List<Border> _panels = [];
    private SessionsMonitorViewModel? _vm;
    private bool _syncQueued;

    /// <summary>Creates the view; set <see cref="ViewModel"/> to draw it.</summary>
    public SessionsMonitorView()
    {
        HorizontalContentAlignment = HorizontalAlignment.Stretch;
        VerticalContentAlignment = VerticalAlignment.Stretch;
        Content = BuildLayout();
        ActualThemeChanged += (_, _) => Paint();
        Loaded += (_, _) => Paint();
        AddAccelerator(VirtualKey.P, VirtualKeyModifiers.None, TogglePause, typing: false);
        AddAccelerator(VirtualKey.F, VirtualKeyModifiers.Control, () => _filter.Focus(FocusState.Keyboard), typing: true);
        AddAccelerator(VirtualKey.Delete, VirtualKeyModifiers.None, () => Run(_vm?.TerminateSessionCommand), typing: false);
        _pause.Click += (_, _) => TogglePause();
        _filter.TextChanged += (_, _) => { if (_vm is { } vm && vm.FilterText != _filter.Text) vm.FilterText = _filter.Text; };
        _hideSystem.Click += (_, _) => { if (_vm is { } vm) vm.HideSystem = _hideSystem.IsChecked == true; };
        for (var i = 0; i < _filters.Length; i++)
        {
            var filter = (SessionFilter)i;
            _filters[i].Click += (_, _) => { if (_vm is { } vm) vm.Filter = filter; };
        }

        _copy.Click += (_, _) => Run(_vm?.CopyQueryCommand);
        _cancel.Click += (_, _) => Run(_vm?.CancelQueryCommand);
        _terminate.Click += (_, _) => Run(_vm?.TerminateSessionCommand);
    }

    /// <summary>Gets or sets the monitor drawn; the view redraws every part.</summary>
    public SessionsMonitorViewModel? ViewModel
    {
        get => _vm;
        set
        {
            if (ReferenceEquals(_vm, value))
                return;
            if (_vm is not null)
                _vm.PropertyChanged -= OnChanged;
            _vm = value;
            if (_vm is not null)
                _vm.PropertyChanged += OnChanged;
            Texts();
            Paint();
        }
    }

    private static void Run(ICommand? command)
    {
        if (command?.CanExecute(null) == true)
            command.Execute(null);
    }

    private void TogglePause()
    {
        if (_vm is { } vm)
            vm.IsPaused = !vm.IsPaused;
    }

    // A letter or Delete accelerator must not steal the keys of a text box that has the focus.
    private void AddAccelerator(VirtualKey key, VirtualKeyModifiers modifiers, Action action, bool typing)
    {
        var accelerator = new KeyboardAccelerator { Key = key, Modifiers = modifiers };
        accelerator.Invoked += (_, args) =>
        {
            if (_vm is null || (!typing && FocusManager.GetFocusedElement(XamlRoot) is TextBox))
                return;
            action();
            args.Handled = true;
        };
        KeyboardAccelerators.Add(accelerator);
    }

    private Brush Brush(string key) => Tokens.Brush(this, key);

    private static FontFamily Mono => (FontFamily)Application.Current.Resources["SquillFontMono"];

    private void OnChanged(object? sender, PropertyChangedEventArgs e)
    {
        // One poll raises many properties: redraw once, on the UI thread.
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
        var root = new Grid();
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition());
        BuildHeader();
        var content = new Grid { Padding = new Thickness(24, 16, 24, 16), RowSpacing = 16 };
        content.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        content.RowDefinitions.Add(new RowDefinition());
        content.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        var tiles = BuildTiles();
        var main = new Grid { ColumnSpacing = 16 };
        main.ColumnDefinitions.Add(new ColumnDefinition());
        main.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(320) });
        var table = BuildTable();
        var detail = BuildDetail();
        Grid.SetColumn(detail, 1);
        main.Children.Add(table);
        main.Children.Add(detail);
        var chains = BuildChains();
        Grid.SetRow(main, 1);
        Grid.SetRow(chains, 2);
        content.Children.Add(tiles);
        content.Children.Add(main);
        content.Children.Add(chains);
        Grid.SetRow(_header, 1);
        Grid.SetRow(content, 2);
        root.Children.Add(_topLine);
        root.Children.Add(_header);
        root.Children.Add(content);
        return root;
    }

    private void BuildHeader()
    {
        var grid = new Grid { ColumnSpacing = 12 };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition());
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var left = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12, Children = { _pulse, _title, _engine, _where, _envPill } };
        var right = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12, Children = { _refreshDot, _refresh, _pause, _filter } };
        Grid.SetColumn(right, 2);
        grid.Children.Add(left);
        grid.Children.Add(right);
        _header.Child = grid;
    }

    private Border Tile(Border tile, params UIElement[] children)
    {
        var stack = new StackPanel { Spacing = 4 };
        foreach (var child in children)
            stack.Children.Add(child);
        tile.Padding = new Thickness(24, 18, 24, 18);
        tile.CornerRadius = new CornerRadius(10);
        tile.BorderThickness = new Thickness(1);
        tile.Child = stack;
        _panels.Add(tile);
        return tile;
    }

    private Grid BuildTiles()
    {
        var grid = new Grid { ColumnSpacing = 16 };
        for (var i = 0; i < 4; i++)
            grid.ColumnDefinitions.Add(new ColumnDefinition());
        _tileLabels = [new(), new(), new(), new()];
        var connections = Tile(_connectionsTile, _tileLabels[0], _connectionsValue, _connectionsBar);
        var transactions = Tile(_transactionsTile, _tileLabels[1], _tps, _spark);
        var longQueries = Tile(_longTile, _tileLabels[2], new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10, Children = { _longValue, _longSub } });
        var locks = Tile(_locksTile, _tileLabels[3], new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10, Children = { _locksValue, _locksSub } });
        for (var i = 0; i < 4; i++)
        {
            var tile = new[] { connections, transactions, longQueries, locks }[i];
            Grid.SetColumn(tile, i);
            grid.Children.Add(tile);
        }

        foreach (var value in new[] { _connectionsValue, _tps, _longValue, _locksValue })
        {
            value.FontFamily = Mono;
            value.FontSize = 26;
            value.FontWeight = FontWeights.SemiBold;
        }

        _longSub.VerticalAlignment = _locksSub.VerticalAlignment = VerticalAlignment.Bottom;
        _longSub.Margin = _locksSub.Margin = new Thickness(0, 0, 0, 4);
        return grid;
    }

    private TextBlock[] _tileLabels = [];

    private Border BuildTable()
    {
        var tabs = new Grid { Padding = new Thickness(16, 10, 16, 10) };
        tabs.ColumnDefinitions.Add(new ColumnDefinition());
        tabs.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var strip = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
        foreach (var filter in _filters)
            strip.Children.Add(filter);
        Grid.SetColumn(_hideSystem, 1);
        tabs.Children.Add(strip);
        tabs.Children.Add(_hideSystem);

        BuildTableHeader();
        var scroll = new ScrollViewer { HorizontalScrollBarVisibility = ScrollBarVisibility.Auto, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Content = new StackPanel { MinWidth = 960, Children = { _tableHeader, _rows } } };
        var body = new Grid();
        body.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        body.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        body.RowDefinitions.Add(new RowDefinition());
        Grid.SetRow(_error, 1);
        Grid.SetRow(scroll, 2);
        body.Children.Add(tabs);
        body.Children.Add(_error);
        body.Children.Add(scroll);
        var panel = new Border { CornerRadius = new CornerRadius(10), BorderThickness = new Thickness(1), Child = body };
        _panels.Add(panel);
        return panel;
    }

    private void BuildTableHeader()
    {
        foreach (var width in ColumnWidths)
            _tableHeader.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(width) });
        _tableHeader.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        _tableHeader.Padding = new Thickness(0, 0, 0, 0);
    }

    private static Grid RowGrid()
    {
        var grid = new Grid { Height = 36 };
        foreach (var width in ColumnWidths)
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(width) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        return grid;
    }

    private Border BuildDetail()
    {
        var title = new Grid { ColumnSpacing = 10 };
        title.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        title.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        title.ColumnDefinitions.Add(new ColumnDefinition());
        Grid.SetColumn(_sessionId, 1);
        Grid.SetColumn(_stateBadge, 2);
        title.Children.Add(_sessionLabel);
        title.Children.Add(_sessionId);
        title.Children.Add(_stateBadge);

        var queryHeader = new Grid();
        queryHeader.ColumnDefinitions.Add(new ColumnDefinition());
        queryHeader.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        Grid.SetColumn(_copy, 1);
        queryHeader.Children.Add(_lastQueryLabel);
        queryHeader.Children.Add(_copy);
        _queryBox.Child = _query;
        _noteBox.Child = _note;
        _detailBody.Children.Add(title);
        _detailBody.Children.Add(_fields);
        _detailBody.Children.Add(queryHeader);
        _detailBody.Children.Add(_queryBox);
        _detailBody.Children.Add(_noteBox);
        _detailBody.Children.Add(_gone);

        var actions = new StackPanel { Spacing = 8, Margin = new Thickness(0, 12, 0, 0), Children = { _cancel, _terminate, _productionHint } };
        var grid = new Grid { Padding = new Thickness(20) };
        grid.RowDefinitions.Add(new RowDefinition());
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        Grid.SetRow(actions, 1);
        grid.Children.Add(new ScrollViewer { Content = _detailBody, VerticalScrollBarVisibility = ScrollBarVisibility.Auto });
        grid.Children.Add(actions);
        var panel = new Border { CornerRadius = new CornerRadius(10), BorderThickness = new Thickness(1), Child = grid };
        _panels.Add(panel);
        return panel;
    }

    private Border BuildChains()
    {
        var title = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10, Padding = new Thickness(16, 10, 16, 10), Children = { new TextBlock { Text = "🔒", VerticalAlignment = VerticalAlignment.Center }, _chainsTitle, _chainsSummary } };
        var body = new StackPanel { Children = { title, new ScrollViewer { MaxHeight = 150, Content = _chains, VerticalScrollBarVisibility = ScrollBarVisibility.Auto } } };
        var panel = new Border { CornerRadius = new CornerRadius(10), BorderThickness = new Thickness(1), Child = body };
        _panels.Add(panel);
        return panel;
    }

    // ---- content ----

    private void Texts()
    {
        _title.Text = UiText.Get("activity.title");
        _sessionLabel.Text = UiText.Get("activity.session");
        _filter.PlaceholderText = UiText.Get("activity.filterPlaceholder");
        _hideSystem.Content = UiText.Get("activity.hideSystem");
        _lastQueryLabel.Text = UiText.Get("activity.lastQuery");
        _copy.Content = UiText.Get("activity.copy");
        _cancel.Content = UiText.Get("activity.cancelQuery");
        _terminate.Content = UiText.Get("activity.terminate");
        _chainsTitle.Text = UiText.Get("activity.chains.title");
        _tileLabels[0].Text = UiText.Get("activity.tile.connections");
        _tileLabels[1].Text = UiText.Get("activity.tile.transactions");
        _tileLabels[2].Text = UiText.Get("activity.tile.longQueries");
        _tileLabels[3].Text = UiText.Get("activity.tile.locks");
        ToolTipService.SetToolTip(_filter, UiText.Get("activity.filterPlaceholder"));
        AutomationName(_filter, UiText.Get("activity.filterPlaceholder"));
    }

    private static void AutomationName(UIElement element, string name) =>
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(element, name);

    private void Paint()
    {
        _header.Background = Brush("SquillBgSurfaceBrush");
        _header.BorderBrush = Brush("SquillBorderBrush");
        foreach (var panel in _panels)
        {
            panel.Background = Brush("SquillBgSurfaceBrush");
            panel.BorderBrush = Brush("SquillBorderBrush");
        }

        _pulse.Foreground = Brush("SquillFg2Brush");
        _where.Foreground = Brush("SquillFg2Brush");
        _refresh.Foreground = Brush("SquillFg2Brush");
        foreach (var label in _tileLabels)
            label.Foreground = Brush("SquillFg2Brush");
        _longSub.Foreground = _locksSub.Foreground = _tps.Foreground = Brush("SquillFg2Brush");
        _connectionsValue.Foreground = Brush("SquillFgBrush");
        _connectionsBar.Foreground = Brush("SquillAccentBrush");
        _spark.Stroke = Brush("SquillFg2Brush");
        _sessionLabel.Foreground = Brush("SquillFg2Brush");
        _sessionId.Foreground = Brush("SquillFgBrush");
        _lastQueryLabel.Foreground = Brush("SquillFgBrush");
        _copy.Foreground = Brush("SquillFg2Brush");
        _queryBox.Background = Brush("SquillBgBaseBrush");
        _queryBox.BorderBrush = Brush("SquillBorderBrush");
        _query.Foreground = Brush("SquillFgBrush");
        _noteBox.Background = Brush("SquillWarningSoftBrush");
        _note.Foreground = Brush("SquillFgBrush");
        _gone.Foreground = Brush("SquillFg2Brush");
        _productionHint.Foreground = Brush("SquillFg2Brush");
        _chainsTitle.Foreground = Brush("SquillFgBrush");
        _chainsSummary.Foreground = Brush("SquillFg2Brush");
        _error.Foreground = Brush("SquillDangerBrush");
        _terminate.Background = Brush("SquillDangerBrush");
        _terminate.Foreground = Brush("SquillOnEnvProdBrush");
        _engine.Background = Brush("SquillAccentSoftBrush");
        ((TextBlock)_engine.Child).Foreground = Brush("SquillAccentTextBrush");
        SyncAll();
    }

    private void SyncAll()
    {
        if (_vm is not { } vm)
            return;
        Texts();
        SyncHeader(vm);
        SyncTiles(vm);
        SyncTable(vm);
        SyncDetail(vm);
        SyncChains(vm);
    }

    private void SyncHeader(SessionsMonitorViewModel vm)
    {
        var profile = vm.Session?.Profile;
        _engine.Visibility = profile is null ? Visibility.Collapsed : Visibility.Visible;
        if (profile is not null)
        {
            ((TextBlock)_engine.Child).Text = CompareFormat.EngineLetters(profile.Dialect);
            _where.Text = string.IsNullOrEmpty(profile.Database) ? profile.Name : $"{profile.Name} / {profile.Database}";
        }

        var production = vm.IsProduction;
        _topLine.Background = production ? Brush("SquillDangerBrush") : null;
        if (ChromeFormat.Environment(vm.Environment) is var (suffix, key, _))
        {
            _envPill.Visibility = Visibility.Visible;
            _envPill.Background = Brush($"SquillEnv{suffix}SoftBrush");
            _envPill.Child = new TextBlock { Text = UiText.Get(key), FontSize = 11, FontWeight = FontWeights.Bold, Foreground = Brush($"SquillEnv{suffix}Brush") };
        }
        else
            _envPill.Visibility = Visibility.Collapsed;

        _refresh.Text = vm.RefreshText;
        _refreshDot.Fill = Brush(vm.Error is not null ? "SquillDangerBrush" : vm.IsPaused ? "SquillFg3Brush" : "SquillSuccessBrush");
        _pause.Content = $"{vm.PauseLabel}  P";
        _error.Visibility = vm.Error is null ? Visibility.Collapsed : Visibility.Visible;
        _error.Text = vm.Error is null ? "" : UiText.Format("activity.error", vm.Error);
        if (_filter.Text != vm.FilterText)
            _filter.Text = vm.FilterText;
    }

    private void SyncTiles(SessionsMonitorViewModel vm)
    {
        _connectionsValue.Text = vm.ConnectionsText;
        _connectionsBar.Value = vm.ConnectionsRatio;
        _connectionsBar.Visibility = vm.ConnectionsRatio > 0 ? Visibility.Visible : Visibility.Collapsed;
        _transactionsTile.Visibility = vm.HasTransactionsTile ? Visibility.Visible : Visibility.Collapsed;
        _tps.Text = vm.TransactionsPerSecond is { } tps ? tps.ToString("N0", CultureInfo.CurrentCulture) : "";
        _spark.Points = Sparkline(vm.TransactionsHistory);
        _longValue.Text = vm.LongQueryCount.ToString(CultureInfo.CurrentCulture);
        _longValue.Foreground = Brush(vm.LongQueryCount > 0 ? "SquillWarningBrush" : "SquillFgBrush");
        _longSub.Text = vm.LongQueryCount > 0 ? UiText.Format("activity.tile.longQueriesSub", vm.VeryLongQueryCount) : "";
        _locksValue.Text = vm.WaitingLockCount.ToString(CultureInfo.CurrentCulture);
        _locksValue.Foreground = Brush(vm.LocksAreDanger ? "SquillDangerBrush" : "SquillFgBrush");
        _locksSub.Text = vm.TopBlockerId is { } top ? UiText.Format("activity.tile.locksSub", top) : "";
        var danger = vm.LocksAreDanger;
        _locksTile.Background = Brush(danger ? "SquillDangerSoftBrush" : "SquillBgSurfaceBrush");
        _locksTile.BorderBrush = Brush(danger ? "SquillDangerBrush" : "SquillBorderBrush");
    }

    private static PointCollection Sparkline(IReadOnlyList<double> values)
    {
        var points = new PointCollection();
        if (values.Count < 2)
            return points;
        var min = values.Min();
        var range = Math.Max(values.Max() - min, 1e-9);
        for (var i = 0; i < values.Count; i++)
            points.Add(new Point(i * 100.0 / (values.Count - 1), 100 - (values[i] - min) / range * 100));
        return points;
    }

    private void SyncTable(SessionsMonitorViewModel vm)
    {
        var counts = new[] { vm.AllTabText, vm.ActiveTabText, vm.IdleInTransactionTabText, vm.WaitingTabText };
        for (var i = 0; i < _filters.Length; i++)
        {
            var on = vm.Filter == (SessionFilter)i;
            _filters[i].IsChecked = on;
            _filters[i].Content = counts[i];
            _filters[i].Background = on ? Brush("SquillBgActiveBrush") : new SolidColorBrush(Microsoft.UI.Colors.Transparent);
            _filters[i].Foreground = Brush(on ? "SquillFgBrush" : "SquillFg2Brush");
            _filters[i].BorderThickness = new Thickness(0);
        }

        _hideSystem.IsChecked = vm.HideSystem;
        BuildTableHeaderCells(vm);
        _rows.Children.Clear();
        _rowElements.Clear();
        var selected = vm.SelectedSession?.Id;
        foreach (var row in vm.Rows.Take(MaxRows))
        {
            var element = BuildRow(row, row.Id == selected);
            _rowElements[row.Id] = element;
            _rows.Children.Add(element);
        }
    }

    private void BuildTableHeaderCells(SessionsMonitorViewModel vm)
    {
        _tableHeader.Children.Clear();
        _tableHeader.Height = 36;
        for (var i = 0; i < Columns.Length; i++)
        {
            var column = Columns[i];
            var sorted = vm.SortColumn == column;
            var text = UiText.Get(ColumnKeys[i]) + (sorted ? " " + SessionsFormat.SortArrow(vm.SortDescending) : "");
            var cell = new Border
            {
                Padding = new Thickness(12, 0, 12, 0),
                Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent),
                Child = new TextBlock { Text = text, FontWeight = sorted ? FontWeights.SemiBold : FontWeights.Normal, Foreground = Brush(sorted ? "SquillFgBrush" : "SquillFg2Brush"), VerticalAlignment = VerticalAlignment.Center, HorizontalTextAlignment = column == SessionSortColumn.Duration || column == SessionSortColumn.Pid ? TextAlignment.Right : TextAlignment.Left },
            };
            cell.Tapped += (_, _) => _vm?.SortBy(column);
            Grid.SetColumn(cell, i);
            _tableHeader.Children.Add(cell);
        }
    }

    private Border BuildRow(SessionRowViewModel row, bool selected)
    {
        var grid = RowGrid();
        void Cell(int column, FrameworkElement element)
        {
            Grid.SetColumn(element, column);
            grid.Children.Add(element);
        }

        TextBlock Text(string text, bool mono, string brush = "SquillFgBrush", TextAlignment alignment = TextAlignment.Left) => new()
        {
            Text = text,
            FontFamily = mono ? Mono : FontFamily,
            Foreground = Brush(brush),
            TextTrimming = TextTrimming.CharacterEllipsis,
            TextWrapping = TextWrapping.NoWrap,
            HorizontalTextAlignment = alignment,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(12, 0, 12, 0),
        };

        Cell(0, Text(row.Id, true, alignment: TextAlignment.Right));
        Cell(1, Text(row.User, true));
        Cell(2, Text(row.Application, false));
        Cell(3, Text(row.Client, true));
        var state = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Margin = new Thickness(12, 0, 12, 0), VerticalAlignment = VerticalAlignment.Center };
        state.Children.Add(new Ellipse { Width = 8, Height = 8, Fill = Brush(SessionsFormat.StateBrush(row.StateDot)), VerticalAlignment = VerticalAlignment.Center });
        state.Children.Add(new TextBlock { Text = row.StateText, Foreground = Brush("SquillFgBrush"), TextTrimming = TextTrimming.CharacterEllipsis, VerticalAlignment = VerticalAlignment.Center });
        Cell(4, state);
        Cell(5, Text(row.DurationText, true, SessionsFormat.DurationBrush(row.DurationSeverity), TextAlignment.Right));
        Cell(6, Text(row.WaitText.Length == 0 ? "—" : row.WaitText, true, row.WaitText.Length == 0 ? "SquillFg3Brush" : "SquillDangerBrush"));
        Cell(7, Text(row.QueryLine, true));

        var border = new Border
        {
            Child = grid,
            Background = selected ? Brush("SquillAccentSoftBrush") : new SolidColorBrush(Microsoft.UI.Colors.Transparent),
            BorderBrush = Brush("SquillBorderSubtleBrush"),
            BorderThickness = new Thickness(0, 0, 0, 1),
            IsTabStop = true,
        };
        var id = row.Id;
        border.Tapped += (_, _) => _vm?.SelectSession(id);
        border.KeyDown += (_, e) =>
        {
            if (e.Key != VirtualKey.Enter)
                return;
            _vm?.SelectSession(id);
            _copy.Focus(FocusState.Keyboard); // Enter moves the focus to the detail pane
            e.Handled = true;
        };
        return border;
    }

    private void SyncDetail(SessionsMonitorViewModel vm)
    {
        var detail = vm.Detail;
        _detailBody.Visibility = detail is null && vm.SelectionGoneText is null ? Visibility.Collapsed : Visibility.Visible;
        _gone.Visibility = vm.SelectionGoneText is null ? Visibility.Collapsed : Visibility.Visible;
        _gone.Text = vm.SelectionGoneText ?? "";
        var has = detail is not null;
        foreach (var element in new UIElement[] { _sessionLabel, _sessionId, _stateBadge, _fields, _lastQueryLabel, _copy, _queryBox })
            element.Visibility = has ? Visibility.Visible : Visibility.Collapsed;
        _cancel.Visibility = _terminate.Visibility = _productionHint.Visibility = has ? Visibility.Visible : Visibility.Collapsed;
        _cancel.Visibility = has && vm.SupportsCancelQuery ? Visibility.Visible : Visibility.Collapsed;
        _cancel.IsEnabled = vm.CancelQueryCommand.CanExecute(null);
        _terminate.IsEnabled = vm.TerminateSessionCommand.CanExecute(null);
        _productionHint.Text = vm.ProductionHint;
        _productionHint.Visibility = vm.ProductionHint.Length == 0 || !has ? Visibility.Collapsed : Visibility.Visible;
        _fields.Children.Clear();
        if (detail is null)
        {
            _noteBox.Visibility = Visibility.Collapsed;
            return;
        }

        _sessionId.Text = detail.Row.Id;
        _sessionId.FontFamily = Mono;
        var badge = (TextBlock)_stateBadge.Child;
        badge.Text = detail.Row.StateText;
        var brush = SessionsFormat.StateBrush(detail.Row.StateDot);
        badge.Foreground = Brush(brush);
        _stateBadge.Background = Brush(detail.Row.StateDot == StateDotKind.Warning ? "SquillWarningSoftBrush" : detail.Row.StateDot == StateDotKind.Success ? "SquillSuccessSoftBrush" : "SquillBgActiveBrush");
        foreach (var field in detail.Fields)
        {
            var line = new Grid { ColumnSpacing = 12 };
            line.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(120) });
            line.ColumnDefinitions.Add(new ColumnDefinition());
            var value = new TextBlock { Text = field.Value, FontFamily = Mono, Foreground = Brush("SquillFgBrush"), TextWrapping = TextWrapping.Wrap };
            Grid.SetColumn(value, 1);
            line.Children.Add(new TextBlock { Text = field.Label, Foreground = Brush("SquillFg2Brush"), TextWrapping = TextWrapping.Wrap });
            line.Children.Add(value);
            _fields.Children.Add(line);
        }

        // shortcut: the statement is plain monospace text, no syntax highlighting; upgrade to the SQL editor control in read-only mode.
        _query.FontFamily = Mono;
        _query.Text = detail.Query;
        _noteBox.Visibility = detail.Note is null ? Visibility.Collapsed : Visibility.Visible;
        _note.Text = detail.Note ?? "";
    }

    private void SyncChains(SessionsMonitorViewModel vm)
    {
        _chainsSummary.Text = vm.BlockingUnavailable ? UiText.Get("activity.chains.unavailable") : vm.ChainsSummary;
        _chains.Children.Clear();
        var byId = vm.Rows.ToDictionary(r => r.Id, r => r);
        foreach (var chain in vm.Chains)
            AddChain(chain, 0, byId);
    }

    private void AddChain(BlockingChainNode node, int depth, Dictionary<string, SessionRowViewModel> byId)
    {
        byId.TryGetValue(node.SessionId, out var row);
        var open = !_collapsed.Contains(node.SessionId);
        var line = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Padding = new Thickness(8 + depth * 24, 5, 8, 5) };
        var selected = _vm?.SelectedSession?.Id == node.SessionId;
        if (depth == 0)
        {
            line.Children.Add(new TextBlock { Text = open ? "⌄" : "›", Foreground = Brush("SquillFg2Brush"), VerticalAlignment = VerticalAlignment.Center });
            line.Children.Add(new Border { Padding = new Thickness(6, 1, 6, 1), CornerRadius = new CornerRadius(4), Background = Brush("SquillDangerSoftBrush"), Child = new TextBlock { Text = UiText.Format("activity.chains.blocks", node.BlockedCount), FontSize = 11, FontWeight = FontWeights.Bold, Foreground = Brush("SquillDangerBrush") } });
        }
        else
            line.Children.Add(new TextBlock { Text = "└", Foreground = Brush("SquillFg3Brush"), VerticalAlignment = VerticalAlignment.Center });

        line.Children.Add(new TextBlock { Text = $"{UiText.Get("activity.col.pid")} {node.SessionId}", FontFamily = Mono, FontWeight = FontWeights.SemiBold, Foreground = Brush("SquillFgBrush"), VerticalAlignment = VerticalAlignment.Center });
        if (row is not null)
        {
            line.Children.Add(new TextBlock { Text = row.Application.Length == 0 ? row.User : row.Application, FontFamily = Mono, Foreground = Brush("SquillFg2Brush"), VerticalAlignment = VerticalAlignment.Center });
            line.Children.Add(new TextBlock { Text = depth == 0 ? $"{row.StateText} · {row.DurationText}" : row.DurationText, FontFamily = Mono, Foreground = Brush(SessionsFormat.DurationBrush(row.DurationSeverity)), VerticalAlignment = VerticalAlignment.Center });
        }

        var border = new Border { Child = line, CornerRadius = new CornerRadius(6), Background = selected ? Brush("SquillAccentSoftBrush") : new SolidColorBrush(Microsoft.UI.Colors.Transparent) };
        border.Tapped += (_, _) =>
        {
            if (depth == 0 && !_collapsed.Remove(node.SessionId))
                _collapsed.Add(node.SessionId);
            _vm?.SelectSession(node.SessionId);
            if (_vm is { } vm)
                SyncChains(vm);
        };
        _chains.Children.Add(border);
        if (!open)
            return;
        foreach (var child in node.Children)
            AddChain(child, depth + 1, byId);
    }
}
