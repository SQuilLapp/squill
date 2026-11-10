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
    private PlanViewModel? _plan;

    /// <summary>Creates the view.</summary>
    public PlanTabView() => InitializeComponent();

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
