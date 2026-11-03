using System.ComponentModel;
using System.Globalization;
using Avalonia.Controls;
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
using Squill.Presentation.Editing;
using Squill.Presentation.Results;
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

    /// <summary>Creates the view.</summary>
    public QueryTabView()
    {
        InitializeComponent();
        IsVisible = false;
        ResultGrid.Dialogs = new AvaloniaDialogService();
        ResultsPanel.KeyBindings.Add(new KeyBinding
        {
            Gesture = new KeyGesture(Key.S, KeyModifiers.Control),
            Command = new RelayCommand(() => _result?.Editor?.CommitCommand.Execute(null)),
        });
    }

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
            _tab.PropertyChanged -= OnTabPropertyChanged;
        // A control has a single parent: release the previous editor before another tab shows its own.
        EditorHost.Child = null;
        _tab = DataContext as QueryTabViewModel;
        IsVisible = _tab is not null;
        if (_tab is not null)
        {
            _tab.PropertyChanged += OnTabPropertyChanged;
            EditorHost.Child = (_tab.Editor as AvaloniaSqlEditor)?.Control;
        }
        ShowResult();
    }

    private void OnTabPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(QueryTabViewModel.SelectedResult))
            ShowResult();
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
        ShowEditBar();
        ResultGrid.IsVisible = result?.Kind == ResultKind.Grid;
        Message.IsVisible = result?.Kind == ResultKind.Message;
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
        var loaded = grid.RowCount.ToString("N0", CultureInfo.GetCultureInfo("fr-FR"));
        FooterText.Text = UiText.Format("results.footer", loaded, grid.IsTruncated ? loaded + "+" : loaded);
        ExportTitle.Text = UiText.Format("results.exportRows", loaded);
    }

    private void ShowEditBar()
    {
        if (_editor is not null)
        {
            _editor.PropertyChanged -= OnEditorPropertyChanged;
            _editor.Issues.CollectionChanged -= OnIssuesChanged;
        }

        _editor = _result?.Editor;
        var reason = _editor is null && _result?.ReadOnlyReason != "loading" ? _result?.ReadOnlyReason : null;
        RowTools.IsVisible = _editor is not null;
        Marker.IsVisible = _editor is not null || reason is not null;
        MarkerText.Text = _editor is not null
            ? UiText.Format("results.editable", string.Join(", ", _editor.Target.KeyColumns))
            : reason is null ? null : UiText.Format("results.readOnly", reason);
        MarkerIcon.Bind(global::Avalonia.Controls.Shapes.Path.DataProperty, this.GetResourceObservable(_editor is not null ? "SquillIconPencil" : "SquillIconLock"));
        var brush = this.GetResourceObservable(_editor is not null ? "SquillSuccessBrush" : "SquillFg3Brush");
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
        EditStatus.Text = UiText.Format(count == 1 ? "results.pendingChanges.one" : "results.pendingChanges.other", count);
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
                parts.Add(UiText.Format(n == 1 ? key + ".one" : key + ".other", n));
        }
    }

    private static string Describe(EditIssue issue) =>
        $"Row {issue.Row + 1}{(issue.Column is null ? "" : ", " + issue.Column)}: {issue.Message}";

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
    }

    private void OnExportCsv(object? sender, RoutedEventArgs e) => Export(new CsvResultExporter());

    private void OnExportJson(object? sender, RoutedEventArgs e) => Export(new JsonResultExporter());

    private void Export(IResultExporter exporter)
    {
        ExportButton.Flyout?.Hide();
        _tab?.SelectedResult?.ExportCommand.Execute(exporter);
    }
}
