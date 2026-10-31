using System.ComponentModel;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Squill.App.WinUI.Controls;
using Squill.App.WinUI.Editors;
using Squill.Presentation.Query;

namespace Squill.App.WinUI.Views;

/// <summary>The editor of a query tab above its result.</summary>
public sealed partial class QueryTabView : UserControl
{
    private readonly Border _editorHost = new() { Background = Brush("SquillBgBaseBrush") };
    private readonly DataGridView _grid = new();
    private readonly TextBlock _message = new() { Margin = new Thickness(12), TextWrapping = TextWrapping.Wrap, Foreground = Brush("SquillFgBrush"), Visibility = Visibility.Collapsed };
    private readonly TextBlock _empty = new() { Margin = new Thickness(12), Text = "Run a query to see results here.", Foreground = Brush("SquillFg3Brush") };
    private readonly ProgressBar _progress = new() { IsIndeterminate = true, Height = 2, Visibility = Visibility.Collapsed };
    private QueryTabViewModel? _tab;

    /// <summary>Creates the view.</summary>
    public QueryTabView()
    {
        var splitter = new Splitter(false) { ResizeBefore = true };
        var results = new Grid { Background = Brush("SquillBgBaseBrush") };
        results.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        results.RowDefinitions.Add(new RowDefinition());
        results.Children.Add(_progress);
        var body = new Grid { Children = { _grid, _message, _empty } };
        Grid.SetRow(body, 1);
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
            _tab.PropertyChanged -= OnTabPropertyChanged;
        // A control has a single parent: release the previous editor before another tab shows its own.
        _editorHost.Child = null;
        _tab = tab;
        Visibility = tab is null ? Visibility.Collapsed : Visibility.Visible;
        if (tab is not null)
        {
            tab.PropertyChanged += OnTabPropertyChanged;
            _editorHost.Child = (tab.Editor as PlainSqlEditor)?.Control;
        }

        ShowResult();
        ShowRunning();
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
        _grid.DataSource = result?.Grid;
        _grid.Visibility = result?.Kind == ResultKind.Grid ? Visibility.Visible : Visibility.Collapsed;
        _message.Visibility = result?.Kind == ResultKind.Message ? Visibility.Visible : Visibility.Collapsed;
        _message.Text = result?.Text ?? "";
        _empty.Visibility = result is null ? Visibility.Visible : Visibility.Collapsed;
    }

    private static Microsoft.UI.Xaml.Media.Brush Brush(string key) => (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources[key];
}
