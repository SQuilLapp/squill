using System.ComponentModel;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Squill.Core;
using Squill.Core.Design;
using Squill.Presentation.Designer;

namespace Squill.App.WinUI.Views;

/// <summary>The editing UI of a <see cref="TableDesignerViewModel"/>: columns, indexes, foreign keys, script preview and issues.</summary>
public sealed partial class TableDesignerView : UserControl
{
    private TableDesignerViewModel? _viewModel;

    /// <summary>Creates the view.</summary>
    public TableDesignerView() => InitializeComponent();

    /// <summary>The referential actions offered by the foreign key combo boxes.</summary>
    public static IReadOnlyList<ReferentialAction> Actions { get; } = Enum.GetValues<ReferentialAction>();

    /// <summary>Gets or sets the designer shown; the view rebinds and highlights the rows named by error issues.</summary>
    public TableDesignerViewModel? ViewModel
    {
        get => _viewModel;
        set
        {
            if (_viewModel is not null)
                _viewModel.PropertyChanged -= OnViewModelChanged;
            _viewModel = value;
            if (value is not null)
                value.PropertyChanged += OnViewModelChanged;
            Bindings.Update();
            RefreshRowErrors();
        }
    }

    private void OnViewModelChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(TableDesignerViewModel.Issues))
            RefreshRowErrors();
    }

    private void RefreshRowErrors()
    {
        foreach (var rows in new[] { ColumnRows, IndexRows, ForeignKeyRows })
        {
            foreach (var child in rows.ItemsPanelRoot?.Children.OfType<ContentPresenter>() ?? [])
            {
                if (VisualTreeHelper.GetChildrenCount(child) > 0 && VisualTreeHelper.GetChild(child, 0) is Grid row)
                    ShowRowError(row);
            }
        }
    }

    private void ShowRowError(Grid row)
    {
        var name = row.DataContext switch
        {
            ColumnRowViewModel c => c.Name,
            IndexRowViewModel i => i.Name,
            ForeignKeyRowViewModel f => f.Name,
            _ => null,
        };
        var failed = name is not null && _viewModel is not null
            && _viewModel.Issues.Any(i => i.Severity == DesignIssueSeverity.Error && string.Equals(i.Subject, name, StringComparison.OrdinalIgnoreCase));
        ((Border)row.Children[0]).Visibility = failed ? Visibility.Visible : Visibility.Collapsed;
    }

    private void OnRowLoaded(object sender, RoutedEventArgs e) => ShowRowError((Grid)sender);

    private void OnTypeGotFocus(object sender, RoutedEventArgs e) => FillSuggestions((AutoSuggestBox)sender);

    private void OnTypeTextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
    {
        if (args.Reason == AutoSuggestionBoxTextChangeReason.UserInput)
            FillSuggestions(sender);
    }

    private void FillSuggestions(AutoSuggestBox box)
    {
        if (_viewModel is not null)
            box.ItemsSource = _viewModel.TypeSuggestions.Where(t => t.Contains(box.Text, StringComparison.OrdinalIgnoreCase)).ToList();
    }

    private void OnMoveColumnUp(object sender, RoutedEventArgs e) => _viewModel?.MoveColumnUpCommand.Execute(Row(sender));

    private void OnMoveColumnDown(object sender, RoutedEventArgs e) => _viewModel?.MoveColumnDownCommand.Execute(Row(sender));

    private void OnRemoveColumn(object sender, RoutedEventArgs e) => _viewModel?.RemoveColumnCommand.Execute(Row(sender));

    private void OnRemoveIndex(object sender, RoutedEventArgs e) => _viewModel?.RemoveIndexCommand.Execute(Row(sender));

    private void OnRemoveForeignKey(object sender, RoutedEventArgs e) => _viewModel?.RemoveForeignKeyCommand.Execute(Row(sender));

    private static object? Row(object sender) => ((FrameworkElement)sender).DataContext;
}
