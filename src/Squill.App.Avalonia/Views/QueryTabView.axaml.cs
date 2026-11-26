using System.ComponentModel;
using System.Globalization;
using Avalonia.Controls;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Avalonia.Controls.Documents;
using Avalonia.Input;
using Avalonia;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Controls.Shapes;
using Squill.Presentation.Localization;
using CommunityToolkit.Mvvm.Input;
using Squill.Core;
using Squill.Core.Editing;
using Squill.Core.Transactions;
using Squill.Presentation.Sessions;
using Squill.Presentation.Editing;
using Squill.Presentation.Results;
using Squill.App.Avalonia.Controls;
using Squill.App.Avalonia.Editors;
using Squill.App.Avalonia.Services;
using Squill.Presentation.Panels;
using Squill.Presentation.Query;

namespace Squill.App.Avalonia.Views;

/// <summary>The editor of a query tab above its result.</summary>
public sealed partial class QueryTabView : UserControl
{
    private QueryTabViewModel? _tab;
    private ResultViewModel? _result;
    private ResultEditorViewModel? _editor;
    private SessionTransaction? _transaction;
    private ResultViewState? _viewState;
    private readonly ColumnMenu _columnMenu = new();
    private GridViewMemo? _viewMemo;
    private bool _gridHadFocus;

    /// <summary>Creates the view.</summary>
    public QueryTabView()
    {
        InitializeComponent();
        Preview.OpenChanged += (_, open) => SyncPreviewColumn(open);
        ResultGrid.BusyChanged += OnBusyChanged;
        Preview.BusyChanged += OnBusyChanged;
        SyncPreviewColumn(false);
        Empty.Text = UiText.Get("results.empty");
        IsVisible = false;
        Toolbar.SizeChanged += (_, _) => LayoutToolbar();
        ResultGrid.Dialogs = new AvaloniaDialogService();
        ResultGrid.Canvas.ColumnMenuRequested += OnColumnMenuRequested;
        ResultGrid.Canvas.ColumnSortRequested += (_, e) => _viewState?.ToggleSort(e.Column);
        ResultsPanel.KeyBindings.Add(new KeyBinding
        {
            Gesture = new KeyGesture(Key.S, KeyModifiers.Control),
            Command = new RelayCommand(() => _result?.Editor?.CommitCommand.Execute(null)),
        });
        ResultsPanel.KeyBindings.Add(new KeyBinding
        {
            Gesture = new KeyGesture(Key.F, KeyModifiers.Control),
            Command = new RelayCommand(FilterBar.FocusSearch),
        });
    }

    /// <summary>Gets the content of the open column menu, or <see langword="null"/> when none is open (for tests).</summary>
    internal Control? ColumnMenuContent => _columnMenu.Content;

    /// <summary>Gets or sets the font size of the editor in points.</summary>
    public double EditorFontSize
    {
        get => TextElement.GetFontSize(EditorHost);
        set => TextElement.SetFontSize(EditorHost, value);
    }

    /// <summary>Gives the history and snippets of the right panel their view-models.</summary>
    /// <param name="history">The history view-model.</param>
    /// <param name="snippets">The snippets view-model.</param>
    public void AttachPanels(HistoryViewModel history, SnippetsViewModel snippets)
    {
        HistoryPanel.DataContext = history;
        SnippetsPanel.DataContext = snippets;
    }

    private void OnHistoryClicked(object? sender, RoutedEventArgs e)
    {
        SidePanel.IsVisible = !SidePanel.IsVisible;
        if (!SidePanel.IsVisible)
            return;
        (HistoryPanel.DataContext as HistoryViewModel)?.LoadCommand.Execute(null);
        (SnippetsPanel.DataContext as SnippetsViewModel)?.LoadCommand.Execute(null);
    }

    /// <summary>Colors the 3 px stripe along the editor with the environment of the connection; hidden for <see cref="ConnectionEnvironment.None"/>.</summary>
    /// <param name="environment">The environment of the active connection.</param>
    public void SetEnvironment(ConnectionEnvironment environment)
    {
        var key = environment switch
        {
            ConnectionEnvironment.Production => "SquillEnvProdBrush",
            ConnectionEnvironment.Staging => "SquillEnvStagingBrush",
            ConnectionEnvironment.Development => "SquillEnvDevBrush",
            ConnectionEnvironment.Local => "SquillEnvLocalBrush",
            _ => null,
        };
        EnvStripe.IsVisible = key is not null;
        if (key is not null)
            EnvStripe.Bind(Border.BackgroundProperty, EnvStripe.GetResourceObservable(key));
    }

    /// <inheritdoc />
    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        if (_tab is not null)
        {
            _tab.PropertyChanged -= OnTabPropertyChanged;
            _tab.ResultsReplacing -= OnResultsReplacing;
            _tab.ResultsReplaced -= OnResultsReplaced;
        }

        // A control has a single parent: release the previous editor before another tab shows its own.
        EditorHost.Child = null;
        _tab = DataContext as QueryTabViewModel;
        IsVisible = _tab is not null;
        if (_tab is not null)
        {
            _tab.PropertyChanged += OnTabPropertyChanged;
            _tab.ResultsReplacing += OnResultsReplacing;
            _tab.ResultsReplaced += OnResultsReplaced;
            EditorHost.Child = (_tab.Editor as AvaloniaSqlEditor)?.Control;
        }
        if (_transaction is not null)
            _transaction.PropertyChanged -= OnTransactionChanged;
        _transaction = _tab?.Transaction;
        if (_transaction is not null)
            _transaction.PropertyChanged += OnTransactionChanged;
        ShowResult();
        ShowPlanButtons();
        ShowAutoRefresh();
        ShowTransaction();
    }

    // Keeps what the user arranged in the grid across a refresh: the view-model swaps the results between these two events.
    private void OnResultsReplacing()
    {
        _columnMenu.Hide();
        _viewMemo = _result?.Kind == ResultKind.Grid ? ResultGrid.Canvas.SaveView() : null;
        _gridHadFocus = ResultGrid.Canvas.IsFocused;
    }

    private void OnResultsReplaced()
    {
        if (_viewMemo is { } memo && _result?.Kind == ResultKind.Grid && ResultGrid.DataSource?.Columns.Count == memo.ColumnCount)
        {
            ResultGrid.Canvas.RestoreView(memo);
            if (_gridHadFocus)
                ResultGrid.Canvas.Focus();
        }

        _viewMemo = null;
    }

    private void OnAutoRefreshClicked(object? sender, RoutedEventArgs e)
    {
        if (_tab is { } tab)
            AutoRefreshMenu.Build(tab).ShowAt(AutoRefreshButton);
    }

    // The clock shows the interval next to the icon in the accent colour while auto-refresh is on.
    private void ShowAutoRefresh()
    {
        var tab = _tab;
        var label = tab?.AutoRefreshLabel ?? "";
        AutoRefreshLabel.Text = label;
        AutoRefreshLabel.IsVisible = label.Length > 0;
        var brush = this.GetResourceObservable(label.Length > 0 ? "SquillAccentTextBrush" : "SquillFg2Brush");
        AutoRefreshIcon.Bind(Shape.StrokeProperty, brush);
        AutoRefreshLabel.Bind(TextBlock.ForegroundProperty, brush);
        AutoRefreshButton.IsEnabled = tab is not null && (tab.CanRefresh || tab.AutoRefreshInterval is not null);
        ToolTip.SetTip(AutoRefreshButton, tab?.AutoRefreshToolTip);
        global::Avalonia.Automation.AutomationProperties.SetName(AutoRefreshButton, (UiText.Get("query.autoRefresh.menu") + " " + label).TrimEnd());
        LayoutToolbar();
    }

    private void OnTransactionChanged(object? sender, PropertyChangedEventArgs e) => ShowTransaction();

    private async void OnModeClicked(object? sender, RoutedEventArgs e)
    {
        if (_tab is not { } tab)
            return;
        await tab.SetModeCommand.ExecuteAsync(ReferenceEquals(sender, TxManualButton) ? TransactionMode.Manual : TransactionMode.AutoCommit);
        ShowTransaction();
    }

    // The toggle and the buttons mirror the session transaction shared by every tab of the connection.
    private void ShowTransaction()
    {
        if (_transaction is not { } tx)
            return;
        TxAutoButton.IsChecked = tx.Mode == TransactionMode.AutoCommit;
        TxManualButton.IsChecked = tx.Mode == TransactionMode.Manual;
        TxMode.IsEnabled = tx.IsSupported;
        ToolTip.SetShowOnDisabled(TxMode, true);
        ToolTip.SetTip(TxMode, UiText.Get(tx.IsSupported ? "tx.tooltip.mode" : "tx.unsupported"));
        var failed = tx.State == TransactionState.Failed;
        TxPill.IsVisible = tx.PendingCount > 0 || failed;
        TxPill.Classes.Set("warning", !failed);
        TxPill.Classes.Set("danger", failed);
        TxRollbackButton.IsVisible = TxCommitButton.IsVisible = tx.Mode == TransactionMode.Manual || tx.State != TransactionState.None;
        LayoutToolbar();
    }

    // The pill and the transaction buttons widen the left group: shrink the optional parts one level at a time until it fits.
    private void LayoutToolbar()
    {
        var tx = _transaction;
        var available = Toolbar.Bounds.Width - Toolbar.Padding.Left - Toolbar.Padding.Right;
        if (tx is null || available <= 0)
            return;
        var full = tx.State == TransactionState.Failed ? UiText.Get("tx.status.failed") : UiText.FormatPlural("tx.pill", tx.PendingCount);
        var count = tx.PendingCount.ToString(CultureInfo.CurrentCulture);
        var right = FormatButton.Bounds.Width + HistoryButton.Bounds.Width + 12;
        for (var level = 0; level <= 4; level++)
        {
            TxCommitChip.IsVisible = TxRollbackChip.IsVisible = level < 1;
            TxPillText.Text = level >= 2 ? count : full;
            TxCommitIcon.IsVisible = TxRollbackIcon.IsVisible = level < 3;
            RunChip.IsVisible = ScriptChip.IsVisible = AnalyzeChip.IsVisible = level < 4;
            ToolbarLeft.Measure(Size.Infinity);
            if (ToolbarLeft.DesiredSize.Width <= available - right)
                break;
        }
    }

    private void OnTabPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(QueryTabViewModel.SelectedResult))
            ShowResult();
        else if (e.PropertyName == nameof(QueryTabViewModel.IsParametersPanelOpen) && _tab?.IsParametersPanelOpen == true)
            Dispatcher.UIThread.Post(FocusFirstEmptyParameter, DispatcherPriority.Loaded);
        else if (e.PropertyName == nameof(QueryTabViewModel.IsRunning) && _tab?.IsRunning == true && !_tab.IsRefreshing)
            (_tab.Editor as AvaloniaSqlEditor)?.HideSuggest();
        else if (e.PropertyName is nameof(QueryTabViewModel.CanExplain) or nameof(QueryTabViewModel.CanAnalyze) or nameof(QueryTabViewModel.ExplainDisabledReason) or nameof(QueryTabViewModel.AnalyzeDisabledReason))
            ShowPlanButtons();
        else if (e.PropertyName is nameof(QueryTabViewModel.AutoRefreshInterval) or nameof(QueryTabViewModel.AutoRefreshLabel) or nameof(QueryTabViewModel.AutoRefreshToolTip) or nameof(QueryTabViewModel.AutoRefreshPausedReason) or nameof(QueryTabViewModel.CanRefresh))
            ShowAutoRefresh();
    }

    private void FocusFirstEmptyParameter()
    {
        var boxes = ParameterRows.GetVisualDescendants().OfType<TextBox>().Where(b => b.IsEffectivelyEnabled).ToList();
        (boxes.FirstOrDefault(b => string.IsNullOrEmpty(b.Text)) ?? boxes.FirstOrDefault())?.Focus();
    }

    // The commands stay executable; the view-model says through CanExplain / CanAnalyze whether the statement qualifies and why not.
    private void ShowPlanButtons()
    {
        ExplainButton.IsEnabled = _tab?.CanExplain == true;
        AnalyzeButton.IsEnabled = _tab?.CanAnalyze == true;
        ToolTip.SetTip(ExplainButton, ExplainButton.IsEnabled ? null : _tab?.ExplainDisabledReason);
        ToolTip.SetTip(AnalyzeButton, AnalyzeButton.IsEnabled ? null : _tab?.AnalyzeDisabledReason);
    }

    private void ShowResult()
    {
        var result = _tab?.SelectedResult;
        if (_result is not null)
            _result.PropertyChanged -= OnResultPropertyChanged;
        _result = result;
        if (result is not null)
            result.PropertyChanged += OnResultPropertyChanged;
        ShowGrid();
        ShowFilterBar();
        ShowEditBar();
        ResultGrid.IsVisible = result?.Kind == ResultKind.Grid;
        Message.IsVisible = result?.Kind is ResultKind.Message or ResultKind.Messages;
        Message.Classes.Set("mono", result?.Kind == ResultKind.Messages);
        Message.Text = result?.Text;
        Empty.IsVisible = result is null;
        ExportButton.IsEnabled = result?.Kind == ResultKind.Grid;
        RefreshFooter();
    }

    private void OnResultPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ResultViewModel.DisplayGrid))
            ShowGrid();
        else if (e.PropertyName is nameof(ResultViewModel.Editor) or nameof(ResultViewModel.ReadOnlyReason))
            ShowEditBar();
        else if (e.PropertyName == nameof(ResultViewModel.CountText))
            RefreshFooter();
    }

    private void RefreshFooter()
    {
        var grid = _result?.Kind == ResultKind.Grid ? _result.DisplayGrid : null;
        Footer.IsVisible = grid is not null;
        if (grid is null)
            return;
        var loaded = DisplayFormat.Count(grid.RowCount);
        FooterText.Text = _result?.View?.FooterText ?? UiText.Format("results.footer", loaded, grid.IsTruncated ? loaded + "+" : loaded);
        ExportTitle.Text = UiText.Format("results.exportRows", DisplayFormat.Count((_result?.Editor?.Grid ?? (IGridDataSource?)_result?.Grid)?.RowCount ?? grid.RowCount)); // export writes every loaded row, filtered or not
    }

    private void ShowFilterBar()
    {
        var state = _result?.Kind == ResultKind.Grid ? _result.View : null;
        if (!ReferenceEquals(state, _viewState))
        {
            if (_viewState is not null)
            {
                _viewState.PropertyChanged -= OnViewStateChanged;
                _viewState.Filters.CollectionChanged -= OnViewFiltersChanged;
            }

            _viewState = state;
            if (state is not null)
            {
                state.PropertyChanged += OnViewStateChanged;
                state.Filters.CollectionChanged += OnViewFiltersChanged;
            }
        }

        FilterBar.Attach(state);
        ShowHeaderMarks();
    }

    private void OnViewStateChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ResultViewState.FooterText))
            RefreshFooter();
        else if (e.PropertyName == nameof(ResultViewState.IsActive))
            ShowEditBar();
        else if (e.PropertyName == nameof(ResultViewState.Sort))
            ShowHeaderMarks();
    }

    private void OnViewFiltersChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e) => ShowHeaderMarks();

    // The grid control only gets column numbers; it never sees the filter model.
    private void ShowHeaderMarks()
    {
        var canvas = ResultGrid.Canvas;
        canvas.SortColumn = _viewState?.Sort?.Column ?? -1;
        canvas.SortDescending = _viewState?.Sort?.Descending ?? false;
        canvas.FilteredColumns = _viewState is null ? new HashSet<int>() : _viewState.Filters.Select(c => c.Filter.Column).ToHashSet();
    }

    private void OnColumnMenuRequested(object? sender, ColumnMenuRequestedEventArgs e)
    {
        if (_viewState?.OpenColumnMenu(e.Column) is not { } menu)
            return;
        var hasFilter = _viewState.Filters.Any(c => c.Filter.Column == e.Column);
        _columnMenu.Show(ResultGrid, menu, hasFilter, e.Anchor);
    }

    private void ShowEditBar()
    {
        if (_editor is not null)
        {
            _editor.PropertyChanged -= OnEditorPropertyChanged;
            _editor.Issues.CollectionChanged -= OnIssuesChanged;
        }

        _editor = _result?.Editor;
        var filtered = _editor?.Grid.IsViewActive == true; // a filter, search or sort makes the grid read-only (ADR 0014)
        var reason = (_editor is null || filtered) && _result?.ReadOnlyReason != "loading" ? _result?.ReadOnlyReason : null;
        RowTools.IsVisible = _editor is not null;
        Marker.IsVisible = _editor is not null || reason is not null;
        MarkerText.Text = _editor is not null && !filtered
            ? UiText.Format("results.editable", string.Join(", ", _editor.Target.KeyColumns))
            : reason is null ? null : UiText.Format("results.readOnly", reason);
        MarkerIcon.Bind(global::Avalonia.Controls.Shapes.Path.DataProperty, this.GetResourceObservable(_editor is not null && !filtered ? "SquillIconPencil" : "SquillIconLock"));
        var brush = this.GetResourceObservable(_editor is not null && !filtered ? "SquillSuccessBrush" : "SquillFg3Brush");
        MarkerIcon.Bind(Shape.StrokeProperty, brush);
        MarkerText.Bind(TextBlock.ForegroundProperty, brush);
        PreviewSql.Command = _editor?.PreviewScriptCommand;
        Commit.Command = _editor?.CommitCommand;
        Discard.Command = _editor?.DiscardCommand;
        RefreshEditBar();
        if (_editor is null)
            return;
        _editor.PropertyChanged += OnEditorPropertyChanged;
        _editor.Issues.CollectionChanged += OnIssuesChanged;
    }

    private void OnEditorPropertyChanged(object? sender, PropertyChangedEventArgs e) => RefreshEditBar();

    private void OnIssuesChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e) => RefreshEditBar();

    private void RefreshEditBar()
    {
        EditBar.IsVisible = _editor?.HasPendingChanges == true;
        if (_editor is null)
            return;
        var count = _editor.PendingCount;
        EditStatus.Text = UiText.FormatPlural("results.pendingChanges", count);
        EditDetail.Text = DescribeChanges(_editor);
        var issues = _editor.Issues.Count;
        IssuesButton.IsVisible = issues > 0;
        IssuesButton.Content = $"⚠ {issues}";
        IssueList.ItemsSource = _editor.Issues.Select(i => new IssueItem(i, Describe(i))).ToList();
    }

    private static string DescribeChanges(ResultEditorViewModel editor)
    {
        var edits = ChangeSetBuilder.Build(editor.Grid, editor.Target).Edits;
        var parts = new List<string>(3);
        Add(parts, edits.Count(e => e.Edit is UpdateRow), "results.updates");
        Add(parts, edits.Count(e => e.Edit is InsertRow), "results.inserts");
        Add(parts, edits.Count(e => e.Edit is DeleteRow), "results.deletes");
        return string.Join(" · ", parts);

        static void Add(List<string> parts, int n, string key)
        {
            if (n > 0)
                parts.Add(UiText.FormatPlural(key, n));
        }
    }

    private static string Describe(EditIssue issue) =>
        IssueText.Describe(issue);

    private void OnIssueSelected(object? sender, SelectionChangedEventArgs e)
    {
        if (e.AddedItems.Count == 0 || e.AddedItems[0] is not IssueItem { Issue: var issue } || _editor is null || _result?.Selection is not { } selection)
            return;
        var columns = _editor.Grid.Columns;
        var column = issue.Column is null ? -1 : columns.ToList().FindIndex(c => c.Name == issue.Column);
        selection.SelectCell(issue.Row, Math.Max(column, 0), false);
        ResultGrid.Canvas.InvalidateVisual();
        IssuesButton.Flyout?.Hide();
    }

    private void OnAddRow(object? sender, RoutedEventArgs e) => _result?.EditController?.AddRow();

    private void OnDeleteRows(object? sender, RoutedEventArgs e) => _result?.EditController?.DeleteSelectedRows();

    private void OnRevertRows(object? sender, RoutedEventArgs e) => _result?.EditController?.RevertSelectedRows();

    private sealed record IssueItem(EditIssue Issue, string Text)
    {
        public override string ToString() => Text;
    }

    private void ShowGrid()
    {
        var result = _result;
        // Selection and controller first: assigning the data source clears the selection.
        ResultGrid.Selection = result?.Selection ?? new GridSelection();
        ResultGrid.EditController = result?.EditController;
        ResultGrid.DataSource = result?.DisplayGrid;
        Preview.Attach(result?.Preview);
    }

    // A closed pane gives its whole column back (the width the user dragged stays in Width, only the limits change).
    private void OnBusyChanged(object? sender, bool busy)
    {
        if (busy)
        {
            Busy.Show();
        }
        else
        {
            Busy.Hide();
        }
    }

    private void SyncPreviewColumn(bool open)
    {
        PreviewSplitter.IsVisible = open;
        var column = ResultBody.ColumnDefinitions[2];
        column.MinWidth = open ? 160 : 0;
        column.MaxWidth = open ? double.PositiveInfinity : 0;
    }

    private void OnExportCsv(object? sender, RoutedEventArgs e) => Export(new CsvResultExporter());

    private void OnExportJson(object? sender, RoutedEventArgs e) => Export(new JsonResultExporter());

    private void Export(IResultExporter exporter)
    {
        ExportButton.Flyout?.Hide();
        _tab?.SelectedResult?.ExportCommand.Execute(exporter);
    }
}
