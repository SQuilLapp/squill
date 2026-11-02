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
    private readonly TextBlock _message = new() { Margin = new Thickness(12), TextWrapping = TextWrapping.Wrap, Foreground = Brush("SquillFgBrush"), Visibility = Visibility.Collapsed };
    private readonly TextBlock _empty = new() { Margin = new Thickness(12), Text = "Run a query to see results here.", Foreground = Brush("SquillFg3Brush") };
    private readonly ProgressBar _progress = new() { IsIndeterminate = true, Height = 2, Visibility = Visibility.Collapsed };
    private readonly StackPanel _resultTabs = new() { Orientation = Orientation.Horizontal, Spacing = 2 };
    private readonly Button _exportCsv = new() { Content = "Export CSV" };
    private readonly Button _exportJson = new() { Content = "Export JSON" };
    private readonly Dictionary<ResultViewModel, ToggleButton> _resultButtons = [];
    private readonly Button _addRow = new() { Content = "Add row" };
    private readonly Button _deleteRows = new() { Content = "Delete rows" };
    private readonly Button _revertRows = new() { Content = "Revert rows" };
    private readonly Button _previewSql = new() { Content = "Preview SQL" };
    private readonly Button _commit = new() { Content = "Commit" };
    private readonly Button _discard = new() { Content = "Discard" };
    private readonly TextBlock _editStatus = new() { VerticalAlignment = VerticalAlignment.Center, Foreground = Brush("SquillFg2Brush") };
    private readonly Button _issuesButton = new() { Visibility = Visibility.Collapsed, Foreground = Brush("SquillDangerBrush") };
    private readonly ListView _issueList = new() { MaxHeight = 240, SelectionMode = ListViewSelectionMode.None, IsItemClickEnabled = true };
    private readonly StackPanel _editBar = new() { Orientation = Orientation.Horizontal, Spacing = 4, Margin = new Thickness(4), Visibility = Visibility.Collapsed };
    private readonly TextBlock _readOnlyLabel = new() { Margin = new Thickness(8, 4, 8, 4), Foreground = Brush("SquillFg3Brush"), Visibility = Visibility.Collapsed };
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
        editor.Children.Add(_envStripe);
        editor.Children.Add(_editorHost);
        Grid.SetRow(editor, 1);
        var results = new Grid { Background = Brush("SquillBgBaseBrush") };
        results.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        results.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        results.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        results.RowDefinitions.Add(new RowDefinition());
        var strip = new Grid { Background = Brush("SquillBgRaisedBrush"), ColumnDefinitions = { new ColumnDefinition(), new ColumnDefinition { Width = GridLength.Auto } } };
        var exports = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4, Margin = new Thickness(4), Children = { _exportCsv, _exportJson } };
        Grid.SetColumn(exports, 1);
        strip.Children.Add(new ScrollViewer { HorizontalScrollBarVisibility = ScrollBarVisibility.Auto, VerticalScrollBarVisibility = ScrollBarVisibility.Disabled, Content = _resultTabs });
        strip.Children.Add(exports);
        _exportCsv.Click += (_, _) => _tab?.SelectedResult?.ExportCommand.Execute(new CsvResultExporter());
        _exportJson.Click += (_, _) => _tab?.SelectedResult?.ExportCommand.Execute(new JsonResultExporter());
        _issuesButton.Flyout = new Flyout { Content = _issueList };
        _issueList.ItemClick += OnIssueClicked;
        _addRow.Click += (_, _) => _result?.EditController?.AddRow();
        _deleteRows.Click += (_, _) => _result?.EditController?.DeleteSelectedRows();
        _revertRows.Click += (_, _) => _result?.EditController?.RevertSelectedRows();
        foreach (var child in new UIElement[] { _addRow, _deleteRows, _revertRows, _previewSql, _commit, _discard, _editStatus, _issuesButton })
            _editBar.Children.Add(child);
        var save = new KeyboardAccelerator { Key = VirtualKey.S, Modifiers = VirtualKeyModifiers.Control };
        save.Invoked += (_, e) =>
        {
            _result?.Editor?.CommitCommand.Execute(null);
            e.Handled = true;
        };
        results.KeyboardAccelerators.Add(save);
        Grid.SetRow(_editBar, 1);
        Grid.SetRow(_readOnlyLabel, 1);
        Grid.SetRow(_progress, 2);
        results.Children.Add(strip);
        results.Children.Add(_editBar);
        results.Children.Add(_readOnlyLabel);
        results.Children.Add(_progress);
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
    }

    private Border BuildToolbar()
    {
        _run.Content = ButtonLabel(UiText.Get("editor.run"), "Ctrl+Entrée", chip: false);
        _script.Content = ButtonLabel(UiText.Get("editor.script"), "Ctrl+Maj+Entrée", chip: true);
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
        foreach (var (result, button) in _resultButtons)
            result.PropertyChanged -= OnResultPropertyChanged;
        _resultButtons.Clear();
        _resultTabs.Children.Clear();
        foreach (var result in _tab?.Results ?? [])
        {
            var button = new ToggleButton { Content = result.Header, IsChecked = result == _tab?.SelectedResult };
            button.Click += (_, _) => _tab!.SelectedResult = result;
            result.PropertyChanged += OnResultPropertyChanged;
            _resultButtons[result] = button;
            _resultTabs.Children.Add(button);
        }
    }

    private void OnResultPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ResultViewModel.Header) && sender is ResultViewModel result && _resultButtons.TryGetValue(result, out var button))
            button.Content = result.Header;
        else if (e.PropertyName == nameof(ResultViewModel.DisplayGrid) && sender == _result)
            ShowGrid();
        else if (e.PropertyName is nameof(ResultViewModel.Editor) or nameof(ResultViewModel.ReadOnlyReason) && sender == _result)
            ShowEditBar();
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
        foreach (var (candidate, button) in _resultButtons)
            button.IsChecked = candidate == result;
        _exportCsv.IsEnabled = _exportJson.IsEnabled = result?.Kind == ResultKind.Grid;
        _result = result;
        ShowGrid();
        ShowEditBar();
        _grid.Visibility = result?.Kind == ResultKind.Grid ? Visibility.Visible : Visibility.Collapsed;
        _message.Visibility = result?.Kind == ResultKind.Message ? Visibility.Visible : Visibility.Collapsed;
        _message.Text = result?.Text ?? "";
        _empty.Visibility = result is null ? Visibility.Visible : Visibility.Collapsed;
    }

    private void ShowEditBar()
    {
        if (_editor is not null)
        {
            _editor.PropertyChanged -= OnEditorChanged;
            _editor.Issues.CollectionChanged -= OnEditorChanged;
        }

        _editor = _result?.Editor;
        var reason = _editor is null ? _result?.ReadOnlyReason : null;
        _editBar.Visibility = _editor is null ? Visibility.Collapsed : Visibility.Visible;
        _readOnlyLabel.Visibility = reason is null ? Visibility.Collapsed : Visibility.Visible;
        _readOnlyLabel.Text = reason is null ? "" : $"Read-only: {reason}";
        _previewSql.Command = _editor?.PreviewScriptCommand;
        _commit.Command = _editor?.CommitCommand;
        _discard.Command = _editor?.DiscardCommand;
        if (_editor is null)
            return;
        _editor.PropertyChanged += OnEditorChanged;
        _editor.Issues.CollectionChanged += OnEditorChanged;
        RefreshEditBar();
    }

    private void OnEditorChanged(object? sender, EventArgs e) => RefreshEditBar();

    private void RefreshEditBar()
    {
        if (_editor is null)
            return;
        _editStatus.Text = _editor.StatusText;
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
