using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Input;
using Avalonia.Interactivity;
using CommunityToolkit.Mvvm.Input;
using Squill.Core.Editing;
using Squill.Presentation.Editing;
using Squill.Presentation.Results;
using Squill.App.Avalonia.Editors;
using Squill.App.Avalonia.Services;
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
        ExportCsv.IsEnabled = ExportJson.IsEnabled = result?.Kind == ResultKind.Grid;
    }

    private void OnResultPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ResultViewModel.DisplayGrid))
            ShowGrid();
        else if (e.PropertyName is nameof(ResultViewModel.Editor) or nameof(ResultViewModel.ReadOnlyReason))
            ShowEditBar();
    }

    private void ShowEditBar()
    {
        if (_editor is not null)
        {
            _editor.PropertyChanged -= OnEditorPropertyChanged;
            _editor.Issues.CollectionChanged -= OnIssuesChanged;
        }

        _editor = _result?.Editor;
        var reason = _editor is null ? _result?.ReadOnlyReason : null;
        EditBar.IsVisible = _editor is not null;
        ReadOnlyLabel.IsVisible = reason is not null;
        ReadOnlyLabel.Text = reason is null ? null : $"Read-only: {reason}";
        PreviewSql.Command = _editor?.PreviewScriptCommand;
        Commit.Command = _editor?.CommitCommand;
        Discard.Command = _editor?.DiscardCommand;
        if (_editor is null)
            return;
        _editor.PropertyChanged += OnEditorPropertyChanged;
        _editor.Issues.CollectionChanged += OnIssuesChanged;
        RefreshEditBar();
    }

    private void OnEditorPropertyChanged(object? sender, PropertyChangedEventArgs e) => RefreshEditBar();

    private void OnIssuesChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e) => RefreshEditBar();

    private void RefreshEditBar()
    {
        if (_editor is null)
            return;
        EditStatus.Text = _editor.StatusText;
        var count = _editor.Issues.Count;
        IssuesButton.IsVisible = count > 0;
        IssuesButton.Content = $"⚠ {count}";
        IssueList.ItemsSource = _editor.Issues.Select(i => new IssueItem(i, Describe(i))).ToList();
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

    private void OnExportCsv(object? sender, RoutedEventArgs e) => _tab?.SelectedResult?.ExportCommand.Execute(new CsvResultExporter());

    private void OnExportJson(object? sender, RoutedEventArgs e) => _tab?.SelectedResult?.ExportCommand.Execute(new JsonResultExporter());
}
