using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Squill.Core.Plans;
using Squill.Presentation.Plans;

namespace Squill.App.Avalonia.Views;

/// <summary>The plan of a statement: a toolbar (view switch, command, summary figures, warning pills, zoom, re-run) above the body filled by the table, details and graph.</summary>
public sealed partial class PlanTabView : UserControl
{
    private PlanTabViewModel? _tab;
    private readonly PlanTable _table = new();
    private PlanViewModel? _plan;

    /// <summary>Creates the view.</summary>
    public PlanTabView()
    {
        InitializeComponent();
        Grid.SetRow(_table, 2);
        PlanBody.Children.Add(_table);
        ActualThemeVariantChanged += (_, _) => Refresh();
    }

    /// <inheritdoc />
    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        if (_tab is not null)
            _tab.PropertyChanged -= OnChanged;
        _tab = DataContext as PlanTabViewModel;
        if (_tab is not null)
            _tab.PropertyChanged += OnChanged;
        Refresh();
    }

    private void OnChanged(object? sender, PropertyChangedEventArgs e) => Dispatcher.UIThread.Post(Refresh);

    private void Refresh()
    {
        if (_plan is not null)
            _plan.PropertyChanged -= OnChanged;
        _plan = _tab?.Plan;
        if (_tab is not { } tab || _plan is not { } plan)
            return;
        plan.PropertyChanged += OnChanged;

        TreeButton.IsChecked = plan.ViewMode == PlanViewMode.Tree;
        GraphButton.IsChecked = plan.ViewMode == PlanViewMode.Graph;
        TextButton.IsChecked = plan.ViewMode == PlanViewMode.Text;
        CommandText.Text = plan.CommandText;
        ZoomText.Text = plan.ZoomText;
        Figures.ItemsSource = plan.Summary;
        CriticalText.Text = plan.CriticalCountText;
        CriticalPill.IsVisible = plan.HasCritical;
        WarningText.Text = plan.WarningCountText;
        WarningPill.IsVisible = plan.HasWarnings;
        // The chip is the Analyze shortcut: only an actual plan is re-run with it (Ctrl+E re-runs an estimated one).
        RerunChip.IsVisible = tab.Mode == PlanMode.Actual;
        RerunButton.IsEnabled = !tab.IsBusy;
        MessageText.Text = tab.MessageLine;
        MessageText.IsVisible = tab.MessageLine.Length > 0;
        if (RawText.Text != plan.SourceText)
            RawText.Text = plan.SourceText;

        var text = plan.ViewMode == PlanViewMode.Text;
        RawText.IsVisible = text;
        PlanBody.IsVisible = !text;
        var graph = plan.ViewMode == PlanViewMode.Graph;
        if (graph != PlanSplitter.IsVisible)
        {
            // Entering the graph view restores the 62 / 38 split; the table takes the whole body in the tree view.
            PlanBody.RowDefinitions[0].Height = graph ? new GridLength(62, GridUnitType.Star) : new GridLength(0);
            PlanBody.RowDefinitions[2].Height = new GridLength(38, GridUnitType.Star);
            PlanSplitter.IsVisible = graph;
            GraphHost.IsVisible = graph;
        }

        _table.Bind(tab, plan);
    }

    private void OnViewClicked(object? sender, RoutedEventArgs e)
    {
        if (_plan is not null)
            _plan.ViewMode = ReferenceEquals(sender, TreeButton) ? PlanViewMode.Tree : ReferenceEquals(sender, TextButton) ? PlanViewMode.Text : PlanViewMode.Graph;
    }

    private void OnZoomOutClicked(object? sender, RoutedEventArgs e) => _plan?.ZoomOutCommand.Execute(null);

    private void OnZoomInClicked(object? sender, RoutedEventArgs e) => _plan?.ZoomInCommand.Execute(null);

    private void OnRerunClicked(object? sender, RoutedEventArgs e) => _tab?.RerunCommand.Execute(null);
}
