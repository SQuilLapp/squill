using System.Collections.Specialized;
using System.ComponentModel;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using Squill.App.WinUI.Controls;
using Squill.App.WinUI.Editors;
using ConnectionEnvironment = Squill.Core.ConnectionEnvironment;
using Squill.Core.Editing;
using Squill.Presentation.Editing;
using Windows.System;
using Squill.Presentation;
using Squill.Presentation.Localization;
using Squill.Presentation.Panels;
using Squill.Presentation.Query;
using Squill.Presentation.Results;

namespace Squill.App.WinUI.Views;

/// <summary>The editor of a query tab above its result.</summary>
public sealed partial class QueryTabView : UserControl
{
    private readonly Border _editorHost = new() { Background = Brush("SquillBgBaseBrush") };
    private readonly Border _envStripe = new() { Width = 3, Visibility = Visibility.Collapsed };
    private readonly Button _run = new() { Style = Res("PrimaryButtonStyle"), Height = 28, MinHeight = 28 };
    private readonly Button _script = new() { Height = 28, MinHeight = 28 };
    private readonly Button _stop = new() { Height = 28, MinHeight = 28, Content = UiText.Get("editor.stop") };
    private readonly Button _format = new() { Style = Res("IconButtonStyle") };
    private readonly Button _historyToggle = new() { Style = Res("IconButtonStyle") };
    private readonly Border _sidePanel = new() { Width = 264, Visibility = Visibility.Collapsed, BorderThickness = new Thickness(1, 0, 0, 0) };
    private readonly HistoryView _historyPanel = new();
    private readonly SnippetsView _snippetsPanel = new();
    private readonly Button _historyTab = new();
    private readonly Button _snippetsTab = new();
    private readonly Border _historyTabBar = new() { BorderThickness = new Thickness(0, 0, 0, 2) };
    private readonly Border _snippetsTabBar = new() { BorderThickness = new Thickness(0, 0, 0, 2) };
    private readonly DataGridView _grid = new();
    private readonly TextBlock _message = new() { Margin = new Thickness(12), TextWrapping = TextWrapping.Wrap, IsTextSelectionEnabled = true, Foreground = Brush("SquillFgBrush"), Visibility = Visibility.Collapsed };
    private readonly TextBlock _empty = new() { Margin = new Thickness(12), Text = UiText.Get("results.empty"), Foreground = Brush("SquillFg3Brush") };
    private readonly ProgressBar _progress = new() { IsIndeterminate = true, Height = 2, Visibility = Visibility.Collapsed };
    private readonly StackPanel _resultTabs = new() { Orientation = Orientation.Horizontal, Spacing = 2 };
    private readonly Button _exportButton = new() { Height = 26, MinHeight = 26, Padding = new Thickness(10, 0, 10, 0), VerticalAlignment = VerticalAlignment.Center };
    private readonly MenuFlyoutItem _exportTitle = new() { IsEnabled = false };
    private readonly MenuFlyoutItem _exportCsv = new() { Text = "CSV", KeyboardAcceleratorTextOverride = UiText.Get("results.exportCsvDetail") };
    private readonly MenuFlyoutItem _exportJson = new() { Text = "JSON", KeyboardAcceleratorTextOverride = UiText.Get("results.exportJsonDetail") };
    private readonly Dictionary<ResultViewModel, (Button Button, TextBlock Count)> _resultButtons = [];
    private readonly Button _addRow = new() { Style = Res("IconButtonStyle"), Width = 26, Height = 26 };
    private readonly Button _deleteRows = new() { Style = Res("IconButtonStyle"), Width = 26, Height = 26 };
    private readonly Button _revertRows = new() { Style = Res("IconButtonStyle"), Width = 26, Height = 26 };
    private readonly StackPanel _rowTools = new() { Orientation = Orientation.Horizontal, Spacing = 2, VerticalAlignment = VerticalAlignment.Center, Visibility = Visibility.Collapsed };
    private readonly Button _previewSql = new() { Height = 26, MinHeight = 26, Content = UiText.Get("results.previewSql") };
    private readonly Button _commit = new() { Style = Res("PrimaryButtonStyle"), Height = 26, MinHeight = 26, Content = UiText.Get("results.commit") };
    private readonly Button _discard = new() { Height = 26, MinHeight = 26, Content = UiText.Get("results.cancel") };
    private readonly TextBlock _editStatus = new() { VerticalAlignment = VerticalAlignment.Center, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold };
    private readonly TextBlock _editDetail = new() { VerticalAlignment = VerticalAlignment.Center, FontSize = 12, Foreground = Brush("SquillFg2Brush") };
    private readonly StackPanel _marker = new() { Orientation = Orientation.Horizontal, Spacing = 6, Margin = new Thickness(10, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center, Visibility = Visibility.Collapsed };
    private readonly TextBlock _markerText = new() { FontSize = 12, VerticalAlignment = VerticalAlignment.Center };
    private readonly Border _footer = new() { Height = 26, Padding = new Thickness(12, 0, 12, 0), Visibility = Visibility.Collapsed, BorderThickness = new Thickness(0, 1, 0, 0), Background = Brush("SquillBgSurfaceBrush"), BorderBrush = Brush("SquillBorderSubtleBrush") };
    private readonly TextBlock _footerText = new() { FontSize = 12, VerticalAlignment = VerticalAlignment.Center, Foreground = Brush("SquillFg2Brush") };
    private readonly Button _issuesButton = new() { Visibility = Visibility.Collapsed, Foreground = Brush("SquillDangerBrush") };
    private readonly ListView _issueList = new() { MaxHeight = 240, SelectionMode = ListViewSelectionMode.None, IsItemClickEnabled = true };
    private readonly Border _editBar = new() { Height = 36, Padding = new Thickness(12, 0, 12, 0), Visibility = Visibility.Collapsed, BorderThickness = new Thickness(0, 0, 0, 1), Background = Brush("SquillInfoSoftBrush"), BorderBrush = Brush("SquillBorderSubtleBrush") };
    private readonly List<(FrameworkElement Icon, string Brush)> _icons = [];
    private readonly Grid _results = new() { Background = Brush("SquillBgBaseBrush") };
    private Border? _resultsHeader;
    private (string Text, bool Editable)? _markerLook;
    private ResultEditorViewModel? _editor;
    private QueryTabViewModel? _tab;
    private ResultViewModel? _result;

    /// <summary>Creates the view.</summary>
    public QueryTabView()
    {
        var splitter = new Splitter(false) { ResizeBefore = true };
        var toolbar = BuildToolbar();
        var editor = new Grid { ColumnDefinitions = { new ColumnDefinition { Width = new GridLength(3) }, new ColumnDefinition() } };
        Grid.SetColumn(_editorHost, 1);
        _editorHost.PreviewKeyDown += OnEditorPreviewKeyDown;
        editor.Children.Add(_envStripe);
        editor.Children.Add(_editorHost);
        Grid.SetRow(editor, 1);
        var results = _results;
        results.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        results.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        results.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        results.RowDefinitions.Add(new RowDefinition());
        results.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        var strip = BuildResultsHeader();
        _exportCsv.Click += (_, _) => Export(new CsvResultExporter());
        _exportJson.Click += (_, _) => Export(new JsonResultExporter());
        _issuesButton.Flyout = new Flyout { Content = _issueList };
        _issueList.ItemClick += OnIssueClicked;
        _addRow.Click += (_, _) => _result?.EditController?.AddRow();
        _deleteRows.Click += (_, _) => _result?.EditController?.DeleteSelectedRows();
        _revertRows.Click += (_, _) => _result?.EditController?.RevertSelectedRows();
        _editBar.Child = BuildBanner();
        var save = new KeyboardAccelerator { Key = VirtualKey.S, Modifiers = VirtualKeyModifiers.Control };
        save.Invoked += (_, e) =>
        {
            _result?.Editor?.CommitCommand.Execute(null);
            e.Handled = true;
        };
        results.KeyboardAccelerators.Add(save);
        Grid.SetRow(_editBar, 1);
        Grid.SetRow(_progress, 2);
        Grid.SetRow(_footer, 4);
        _footer.Child = _footerText;
        results.Children.Add(strip);
        results.Children.Add(_editBar);
        results.Children.Add(_progress);
        results.Children.Add(_footer);
        var body = new Grid { Children = { _grid, _message, _empty } };
        Grid.SetRow(body, 3);
        results.Children.Add(body);

        var main = new Grid();
        main.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        main.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        main.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        main.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        Grid.SetRow(splitter, 2);
        Grid.SetRow(results, 3);
        main.Children.Add(toolbar);
        main.Children.Add(editor);
        main.Children.Add(splitter);
        main.Children.Add(results);

        var root = new Grid();
        root.ColumnDefinitions.Add(new ColumnDefinition());
        root.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var side = BuildSidePanel();
        Grid.SetColumn(side, 1);
        root.Children.Add(main);
        root.Children.Add(side);
        Content = root;
        Visibility = Visibility.Collapsed;
        Loaded += (_, _) => PaintResults();
        ActualThemeChanged += (_, _) => PaintResults();
    }

    private Border BuildResultsHeader()
    {
        SetIcon(_addRow, "SquillIconPlus", "results.addRow");
        SetIcon(_deleteRows, "SquillIconTrash", "results.deleteRows");
        SetIcon(_revertRows, "SquillIconUndo", "results.revertRows");
        foreach (var tool in new UIElement[] { _addRow, _deleteRows, _revertRows })
            _rowTools.Children.Add(tool);
        _exportButton.Content = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 6,
            Children =
            {
                new TextBlock { Text = UiText.Get("results.export"), VerticalAlignment = VerticalAlignment.Center },
                Track(Icons.Create("SquillIconCollapse", 10, Brush("SquillFg2Brush")), "SquillFg2Brush"),
            },
        };
        var menu = new MenuFlyout { Placement = FlyoutPlacementMode.BottomEdgeAlignedRight };
        menu.Items.Add(_exportTitle);
        menu.Items.Add(_exportCsv);
        menu.Items.Add(_exportJson);
        _exportButton.Flyout = menu;
        _marker.Children.Add(_markerText);
        var tools = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4, VerticalAlignment = VerticalAlignment.Center, Children = { _rowTools, _exportButton } };
        Grid.SetColumn(_marker, 1);
        Grid.SetColumn(tools, 2);
        var row = new Grid { ColumnDefinitions = { new ColumnDefinition(), new ColumnDefinition { Width = GridLength.Auto }, new ColumnDefinition { Width = GridLength.Auto } } };
        row.Children.Add(new ScrollViewer { HorizontalScrollBarVisibility = ScrollBarVisibility.Auto, VerticalScrollBarVisibility = ScrollBarVisibility.Disabled, VerticalAlignment = VerticalAlignment.Center, Content = _resultTabs });
        row.Children.Add(_marker);
        row.Children.Add(tools);
        return _resultsHeader = new Border { Height = 32, Padding = new Thickness(8, 0, 8, 0), BorderThickness = new Thickness(0, 0, 0, 1), BorderBrush = Brush("SquillBorderSubtleBrush"), Background = Brush("SquillBgBaseBrush"), Child = row };
    }

    private void SetIcon(Button button, string icon, string tooltipKey)
    {
        button.Content = Track(Icons.Create(icon, 14, Brush("SquillFg2Brush")), "SquillFg2Brush");
        ToolTipService.SetToolTip(button, UiText.Get(tooltipKey));
    }

    private Grid BuildBanner()
    {
        _issuesButton.Style = Res("GhostButtonStyle");
        _issuesButton.Height = 26;
        _issuesButton.MinHeight = 26;
        var left = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            VerticalAlignment = VerticalAlignment.Center,
            Children = { Track(Icons.Create("SquillIconPencil", 14, Brush("SquillInfoBrush")), "SquillInfoBrush"), _editStatus, _editDetail },
        };
        var right = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, VerticalAlignment = VerticalAlignment.Center, Children = { _issuesButton, _previewSql, _discard, _commit } };
        Grid.SetColumn(right, 1);
        return new Grid { ColumnDefinitions = { new ColumnDefinition(), new ColumnDefinition { Width = GridLength.Auto } }, Children = { left, right } };
    }

    private FrameworkElement Track(FrameworkElement icon, string brush)
    {
        _icons.Add((icon, brush));
        return icon;
    }

    /// <summary>Paints what code builds by hand with the brushes of the theme the view is shown in.</summary>
    private void PaintResults()
    {
        _results.Background = ThemeBrush("SquillBgBaseBrush");
        _resultsHeader!.BorderBrush = ThemeBrush("SquillBorderSubtleBrush");
        _resultsHeader.Background = ThemeBrush("SquillBgBaseBrush");
        _editBar.Background = ThemeBrush("SquillInfoSoftBrush");
        _editBar.BorderBrush = ThemeBrush("SquillBorderSubtleBrush");
        _editDetail.Foreground = ThemeBrush("SquillFg2Brush");
        _footer.Background = ThemeBrush("SquillBgSurfaceBrush");
        _footer.BorderBrush = ThemeBrush("SquillBorderSubtleBrush");
        _footerText.Foreground = ThemeBrush("SquillFg2Brush");
        foreach (var (icon, brush) in _icons)
            Icons.SetStroke(icon, ThemeBrush(brush));
        foreach (var (_, count) in _resultButtons.Values)
            count.Foreground = ThemeBrush("SquillFg3Brush");

        ShowActiveTab();
        ShowMarker();
    }

    private Microsoft.UI.Xaml.Media.Brush ThemeBrush(string key) => Tokens.Brush(this, key);

    private void ShowActiveTab()
    {
        foreach (var (candidate, (button, _)) in _resultButtons)
            button.Background = candidate == _result ? ThemeBrush("SquillBgActiveBrush") : new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Transparent);
    }

    private void ShowMarker()
    {
        _marker.Visibility = _markerLook is null ? Visibility.Collapsed : Visibility.Visible;
        if (_markerLook is not { } shown)
            return;
        _markerText.Text = shown.Text;
        var brush = ThemeBrush(shown.Editable ? "SquillSuccessBrush" : "SquillFg3Brush");
        _markerText.Foreground = brush;
        if (_marker.Children.Count > 1)
            _marker.Children.RemoveAt(0);
        _marker.Children.Insert(0, Icons.Create(shown.Editable ? "SquillIconPencil" : "SquillIconLock", 12, brush));
    }

    private void Export(IResultExporter exporter)
    {
        ((MenuFlyout)_exportButton.Flyout).Hide();
        _tab?.SelectedResult?.ExportCommand.Execute(exporter);
    }

    private Border BuildToolbar()
    {
        _run.Content = ButtonLabel(UiText.Get("editor.run"), UiText.Get("editor.runShortcut"), chip: false);
        _script.Content = ButtonLabel(UiText.Get("editor.script"), UiText.Get("editor.scriptShortcut"), chip: true);
        _format.Content = Icons.Create("SquillIconFormat", 14, Brush("SquillFg2Brush"));
        _historyToggle.Content = Icons.Create("SquillIconHistory", 14, Brush("SquillFg2Brush"));
        ToolTipService.SetToolTip(_format, UiText.Get("menu.format"));
        ToolTipService.SetToolTip(_historyToggle, UiText.Get("editor.history"));
        _historyToggle.Click += (_, _) => ToggleSidePanel();
        var left = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, VerticalAlignment = VerticalAlignment.Center, Children = { _run, _script, _stop } };
        var right = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, VerticalAlignment = VerticalAlignment.Center, Children = { _format, _historyToggle } };
        Grid.SetColumn(right, 1);
        var row = new Grid { ColumnDefinitions = { new ColumnDefinition(), new ColumnDefinition { Width = GridLength.Auto } }, Children = { left, right } };
        return new Border { Height = 38, Padding = new Thickness(8, 0, 8, 0), BorderThickness = new Thickness(0, 0, 0, 1), BorderBrush = Brush("SquillBorderSubtleBrush"), Background = Brush("SquillBgBaseBrush"), Child = row };
    }

    private static StackPanel ButtonLabel(string text, string hint, bool chip)
    {
        var panel = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, VerticalAlignment = VerticalAlignment.Center };
        panel.Children.Add(new TextBlock { Text = text, VerticalAlignment = VerticalAlignment.Center });
        if (chip)
            panel.Children.Add(new ContentControl { Content = hint, Style = Res("KeyboardChipStyle"), VerticalAlignment = VerticalAlignment.Center });
        else
            panel.Children.Add(new TextBlock { Text = hint, FontFamily = (Microsoft.UI.Xaml.Media.FontFamily)Application.Current.Resources["SquillFontMono"], FontSize = 11, Opacity = 0.7, VerticalAlignment = VerticalAlignment.Center });
        return panel;
    }

    private Border BuildSidePanel()
    {
        _sidePanel.Background = Brush("SquillBgSurfaceBrush");
        _sidePanel.BorderBrush = Brush("SquillBorderSubtleBrush");
        UnderlineTab(_historyTab, _historyTabBar, UiText.Get("editor.history"), () => SelectSideTab(history: true));
        UnderlineTab(_snippetsTab, _snippetsTabBar, UiText.Get("editor.snippets"), () => SelectSideTab(history: false));
        var tabs = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(8, 0, 8, 0), Height = 36, Children = { _historyTabBar, _snippetsTabBar } };
        Grid.SetRow(_historyPanel, 1);
        Grid.SetRow(_snippetsPanel, 1);
        _sidePanel.Child = new Grid { RowDefinitions = { new RowDefinition { Height = GridLength.Auto }, new RowDefinition() }, Children = { tabs, _historyPanel, _snippetsPanel } };
        SelectSideTab(history: true);
        return _sidePanel;
    }

    private static void UnderlineTab(Button button, Border bar, string text, Action select)
    {
        button.Style = Res("GhostButtonStyle");
        button.Content = text;
        button.Height = 34;
        button.CornerRadius = new CornerRadius(0);
        button.Padding = new Thickness(10, 0, 10, 0);
        button.Click += (_, _) => select();
        bar.Child = button;
    }

    private void SelectSideTab(bool history)
    {
        _historyPanel.Visibility = history ? Visibility.Visible : Visibility.Collapsed;
        _snippetsPanel.Visibility = history ? Visibility.Collapsed : Visibility.Visible;
        _historyTabBar.BorderBrush = Brush(history ? "SquillAccentBrush" : "SquillBgSurfaceBrush");
        _snippetsTabBar.BorderBrush = Brush(history ? "SquillBgSurfaceBrush" : "SquillAccentBrush");
        _historyTab.Foreground = Brush(history ? "SquillFgBrush" : "SquillFg3Brush");
        _snippetsTab.Foreground = Brush(history ? "SquillFg3Brush" : "SquillFgBrush");
    }

    private void ToggleSidePanel()
    {
        _sidePanel.Visibility = _sidePanel.Visibility == Visibility.Visible ? Visibility.Collapsed : Visibility.Visible;
        if (_sidePanel.Visibility != Visibility.Visible)
            return;
        _historyPanel.Reload();
        _snippetsPanel.Reload();
    }

    /// <summary>Gives the history and snippets of the right panel their view-models.</summary>
    /// <param name="history">The history view-model.</param>
    /// <param name="snippets">The snippets view-model.</param>
    public void AttachPanels(HistoryViewModel history, SnippetsViewModel snippets)
    {
        _historyPanel.Attach(history);
        _snippetsPanel.Attach(snippets);
    }

    /// <summary>Colors the 3 px stripe along the editor with the environment of the connection; hidden for <see cref="ConnectionEnvironment.None"/>.</summary>
    /// <param name="environment">The environment of the active connection.</param>
    public void SetEnvironment(ConnectionEnvironment environment)
    {
        var look = ChromeFormat.Environment(environment);
        _envStripe.Visibility = look is null ? Visibility.Collapsed : Visibility.Visible;
        if (look is { } value)
            _envStripe.Background = Brush($"SquillEnv{value.Suffix}Brush");
    }

    /// <summary>Gets or sets the service that reports grid paste errors.</summary>
    public IDialogService? Dialogs
    {
        get => _grid.Dialogs;
        set => _grid.Dialogs = value;
    }

    /// <summary>Shows another tab, or nothing when <see langword="null"/>.</summary>
    /// <param name="tab">The tab to show.</param>
    public void Show(QueryTabViewModel? tab)
    {
        if (_tab is not null)
        {
            _tab.PropertyChanged -= OnTabPropertyChanged;
            _tab.Results.CollectionChanged -= OnResultsChanged;
        }

        // A control has a single parent: release the previous editor before another tab shows its own.
        _editorHost.Child = null;
        _tab = tab;
        Visibility = tab is null ? Visibility.Collapsed : Visibility.Visible;
        if (tab is not null)
        {
            tab.PropertyChanged += OnTabPropertyChanged;
            tab.Results.CollectionChanged += OnResultsChanged;
            _editorHost.Child = (tab.Editor as IEditorControl)?.Control;
        }

        _run.Command = tab?.RunCommand;
        _script.Command = tab?.RunAllCommand;
        _stop.Command = tab?.CancelCommand;
        _format.Command = tab?.FormatDocumentCommand;
        RebuildResultTabs();
        ShowResult();
        ShowRunning();
    }

    private void OnResultsChanged(object? sender, NotifyCollectionChangedEventArgs e) => RebuildResultTabs();

    private void RebuildResultTabs()
    {
        foreach (var result in _resultButtons.Keys)
            result.PropertyChanged -= OnResultPropertyChanged;
        _resultButtons.Clear();
        _resultTabs.Children.Clear();
        foreach (var result in _tab?.Results ?? [])
        {
            var count = new TextBlock { Style = Res("MonoTextBlockStyle"), FontSize = 12, Foreground = Brush("SquillFg3Brush"), VerticalAlignment = VerticalAlignment.Center };
            var title = new TextBlock { Text = result.Title, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, VerticalAlignment = VerticalAlignment.Center };
            var button = new Button
            {
                Style = Res("GhostButtonStyle"),
                Height = 24,
                MinHeight = 24,
                Padding = new Thickness(10, 0, 10, 0),
                Content = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, Children = { title, count } },
            };
            button.Click += (_, _) => _tab!.SelectedResult = result;
            result.PropertyChanged += OnResultPropertyChanged;
            _resultButtons[result] = (button, count);
            _resultTabs.Children.Add(button);
            ShowCount(result);
        }
    }

    private void ShowCount(ResultViewModel result)
    {
        var (_, count) = _resultButtons[result];
        count.Text = result.CountText;
        count.Foreground = ThemeBrush("SquillFg3Brush");
        count.Visibility = result.CountText.Length == 0 ? Visibility.Collapsed : Visibility.Visible;
    }

    private void OnResultPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ResultViewModel.CountText) && sender is ResultViewModel counted && _resultButtons.ContainsKey(counted))
        {
            ShowCount(counted);
            if (sender == _result)
                RefreshFooter();
        }
        else if (e.PropertyName == nameof(ResultViewModel.DisplayGrid) && sender == _result)
            ShowGrid();
        else if (e.PropertyName is nameof(ResultViewModel.Editor) or nameof(ResultViewModel.ReadOnlyReason) && sender == _result)
            ShowEditBar();
    }

    // The editor consumes Ctrl+Enter as a new line before the window accelerators run, so the run keys are taken on the way down.
    private void OnEditorPreviewKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (_tab is null || e.Key is not (VirtualKey.Enter or VirtualKey.F5))
            return;
        var down = Windows.UI.Core.CoreVirtualKeyStates.Down;
        var ctrl = Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Control).HasFlag(down);
        var shift = Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Shift).HasFlag(down);
        if (e.Key == VirtualKey.F5 || ctrl && !shift)
            _tab.RunCommand.Execute(null);
        else if (ctrl)
            _tab.RunAllCommand.Execute(null);
        else
            return;
        e.Handled = true;
    }

    private void OnTabPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(QueryTabViewModel.SelectedResult))
            ShowResult();
        else if (e.PropertyName == nameof(QueryTabViewModel.IsRunning))
            ShowRunning();
    }

    private void ShowRunning()
    {
        _progress.Visibility = _tab?.IsRunning == true ? Visibility.Visible : Visibility.Collapsed;
        _stop.IsEnabled = _tab?.IsRunning == true;
    }

    private void ShowResult()
    {
        var result = _tab?.SelectedResult;
        _result = result;
        ShowActiveTab();
        _exportButton.IsEnabled = result?.Kind == ResultKind.Grid;
        ShowGrid();
        ShowEditBar();
        _grid.Visibility = result?.Kind == ResultKind.Grid ? Visibility.Visible : Visibility.Collapsed;
        var isText = result?.Kind is ResultKind.Message or ResultKind.Messages;
        _message.Visibility = isText ? Visibility.Visible : Visibility.Collapsed;
        _message.Style = result?.Kind == ResultKind.Messages ? Res("MonoTextBlockStyle") : null;
        _message.Text = result?.Text ?? "";
        _empty.Visibility = result is null ? Visibility.Visible : Visibility.Collapsed;
        RefreshFooter();
    }

    private void RefreshFooter()
    {
        var grid = _result?.Kind == ResultKind.Grid ? _result.DisplayGrid : null;
        _footer.Visibility = grid is null ? Visibility.Collapsed : Visibility.Visible;
        if (grid is null)
            return;
        _footerText.Text = EditBarFormat.Footer(grid.RowCount, grid.IsTruncated);
        _exportTitle.Text = UiText.Format("results.exportRows", EditBarFormat.Rows(grid.RowCount));
    }

    private void ShowEditBar()
    {
        if (_editor is not null)
        {
            _editor.PropertyChanged -= OnEditorChanged;
            _editor.Issues.CollectionChanged -= OnEditorChanged;
        }

        _editor = _result?.Editor;
        _rowTools.Visibility = _editor is null ? Visibility.Collapsed : Visibility.Visible;
        _markerLook = EditBarFormat.Marker(_editor?.Target.KeyColumns, _editor is null ? _result?.ReadOnlyReason : null);
        ShowMarker();
        _previewSql.Command = _editor?.PreviewScriptCommand;
        _commit.Command = _editor?.CommitCommand;
        _discard.Command = _editor?.DiscardCommand;
        RefreshEditBar();
        if (_editor is null)
            return;
        _editor.PropertyChanged += OnEditorChanged;
        _editor.Issues.CollectionChanged += OnEditorChanged;
    }

    private void OnEditorChanged(object? sender, EventArgs e) => RefreshEditBar();

    private void RefreshEditBar()
    {
        _editBar.Visibility = _editor?.HasPendingChanges == true ? Visibility.Visible : Visibility.Collapsed;
        if (_editor is null)
            return;
        _editStatus.Text = EditBarFormat.Pending(_editor.PendingCount);
        var edits = ChangeSetBuilder.Build(_editor.Grid, _editor.Target).Edits;
        _editDetail.Text = EditBarFormat.Changes(edits.Count(e => e.Edit is UpdateRow), edits.Count(e => e.Edit is InsertRow), edits.Count(e => e.Edit is DeleteRow));
        _issuesButton.Visibility = _editor.Issues.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        _issuesButton.Content = $"⚠ {_editor.Issues.Count}";
        _issueList.ItemsSource = _editor.Issues.Select(i => new IssueItem(i, EditBarFormat.Describe(i))).ToList();
    }

    private void OnIssueClicked(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is not IssueItem { Issue: var issue } || _editor is null || _result?.Selection is not { } selection)
            return;
        var column = EditBarFormat.ColumnIndex(issue, _editor.Grid.Columns.Select(c => c.Name).ToArray());
        selection.SelectCell(issue.Row, column, false);
        _grid.Canvas.Redraw();
        _issuesButton.Flyout?.Hide();
    }

    private void ShowGrid()
    {
        var result = _result;
        // Selection and controller first: assigning the data source clears the selection.
        _grid.Selection = result?.Selection ?? new GridSelection();
        _grid.EditController = result?.EditController;
        _grid.DataSource = result?.DisplayGrid;
    }

    private sealed record IssueItem(EditIssue Issue, string Text)
    {
        public override string ToString() => Text;
    }

    private static Style Res(string key) => (Style)Application.Current.Resources[key];

    private static Microsoft.UI.Xaml.Media.Brush Brush(string key) => (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources[key];
}
