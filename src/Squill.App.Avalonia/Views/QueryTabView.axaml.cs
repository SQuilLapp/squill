using System.ComponentModel;
using Avalonia.Controls;
using Squill.App.Avalonia.Editors;
using Squill.Presentation.Query;

namespace Squill.App.Avalonia.Views;

/// <summary>The editor of a query tab above its result.</summary>
public sealed partial class QueryTabView : UserControl
{
    private QueryTabViewModel? _tab;

    /// <summary>Creates the view.</summary>
    public QueryTabView()
    {
        InitializeComponent();
        IsVisible = false;
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
        ResultGrid.DataSource = result?.Grid;
        ResultGrid.IsVisible = result?.Kind == ResultKind.Grid;
        Message.IsVisible = result?.Kind == ResultKind.Message;
        Message.Text = result?.Text;
    }
}
