using System.Collections.Specialized;
using System.ComponentModel;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Squill.App.WinUI.Controls;
using Squill.App.WinUI.Editors;
using Squill.Presentation.Query;
using Squill.Presentation.Results;

namespace Squill.App.WinUI.Views;

/// <summary>The editor of a query tab above its result.</summary>
public sealed partial class QueryTabView : UserControl
{
    private readonly Border _editorHost = new() { Background = Brush("SquillBgBaseBrush") };
    private readonly DataGridView _grid = new();
    private readonly TextBlock _message = new() { Margin = new Thickness(12), TextWrapping = TextWrapping.Wrap, Foreground = Brush("SquillFgBrush"), Visibility = Visibility.Collapsed };
    private readonly TextBlock _empty = new() { Margin = new Thickness(12), Text = "Run a query to see results here.", Foreground = Brush("SquillFg3Brush") };
    private readonly ProgressBar _progress = new() { IsIndeterminate = true, Height = 2, Visibility = Visibility.Collapsed };
    private readonly StackPanel _resultTabs = new() { Orientation = Orientation.Horizontal, Spacing = 2 };
    private readonly Button _exportCsv = new() { Content = "Export CSV" };
    private readonly Button _exportJson = new() { Content = "Export JSON" };
    private readonly Dictionary<ResultViewModel, ToggleButton> _resultButtons = [];
    private QueryTabViewModel? _tab;

    /// <summary>Creates the view.</summary>
    public QueryTabView()
    {
        var splitter = new Splitter(false) { ResizeBefore = true };
        var results = new Grid { Background = Brush("SquillBgBaseBrush") };
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
        Grid.SetRow(_progress, 1);
        results.Children.Add(strip);
        results.Children.Add(_progress);
        var body = new Grid { Children = { _grid, _message, _empty } };
        Grid.SetRow(body, 2);
        results.Children.Add(body);

        var root = new Grid();
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        Grid.SetRow(splitter, 1);
        Grid.SetRow(results, 2);
        root.Children.Add(_editorHost);
        root.Children.Add(splitter);
        root.Children.Add(results);
        Content = root;
        Visibility = Visibility.Collapsed;
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
            _editorHost.Child = (tab.Editor as PlainSqlEditor)?.Control;
        }

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
    }

    private void OnTabPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(QueryTabViewModel.SelectedResult))
            ShowResult();
        else if (e.PropertyName == nameof(QueryTabViewModel.IsRunning))
            ShowRunning();
    }

    private void ShowRunning() => _progress.Visibility = _tab?.IsRunning == true ? Visibility.Visible : Visibility.Collapsed;

    private void ShowResult()
    {
        var result = _tab?.SelectedResult;
        foreach (var (candidate, button) in _resultButtons)
            button.IsChecked = candidate == result;
        _exportCsv.IsEnabled = _exportJson.IsEnabled = result?.Kind == ResultKind.Grid;
        _grid.DataSource = result?.Grid;
        _grid.Visibility = result?.Kind == ResultKind.Grid ? Visibility.Visible : Visibility.Collapsed;
        _message.Visibility = result?.Kind == ResultKind.Message ? Visibility.Visible : Visibility.Collapsed;
        _message.Text = result?.Text ?? "";
        _empty.Visibility = result is null ? Visibility.Visible : Visibility.Collapsed;
    }

    private static Microsoft.UI.Xaml.Media.Brush Brush(string key) => (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources[key];
}
