using System.ComponentModel;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Squill.App.WinUI.Controls;
using Squill.Core.Plans;
using Squill.Presentation.Localization;
using Squill.Presentation.Plans;

namespace Squill.App.WinUI.Views;

/// <summary>The plan of a statement: a toolbar (view switch, command, summary figures, warning pills, zoom, re-run) above the body filled by the table, details and graph.</summary>
public sealed partial class PlanTabView : UserControl
{
    private PlanTabViewModel? _tab;
    private readonly PlanTable _table = new();
    private readonly Splitter _splitter = new(false) { ResizeBefore = true };
    private PlanViewModel? _plan;

    /// <summary>Creates the view.</summary>
    public PlanTabView()
    {
        InitializeComponent();
        TreeButton.Content = UiText.Get("plan.view.tree");
        GraphButton.Content = UiText.Get("plan.view.graph");
        TextButton.Content = UiText.Get("plan.view.text");
        GraphHost.Children.Insert(0, new PlanGraphCanvas());
        LegendThickness.Text = UiText.Get("plan.legend.thickness");
        LegendAlert.Text = UiText.Get("plan.legend.alert");
        LegendCritical.Text = UiText.Get("plan.legend.critical");
        Grid.SetRow(_splitter, 1);
        Grid.SetRow(_table, 2);
        PlanBody.Children.Add(_splitter);
        PlanBody.Children.Add(_table);
        ActualThemeChanged += (_, _) => Refresh();
    }

    /// <summary>Gets or sets the plan tab shown; the view rebinds its toolbar and body.</summary>
    public PlanTabViewModel? ViewModel
    {
        get => _tab;
        set
        {
            if (_tab is not null)
                _tab.PropertyChanged -= OnTabChanged;
            _tab = value;
            if (_tab is not null)
                _tab.PropertyChanged += OnTabChanged;
            Refresh();
        }
    }

    private void OnTabChanged(object? sender, PropertyChangedEventArgs e) => DispatcherQueue.TryEnqueue(Refresh);

    private void OnPlanChanged(object? sender, PropertyChangedEventArgs e) => DispatcherQueue.TryEnqueue(Refresh);

    private void Refresh()
    {
        if (_plan is not null)
            _plan.PropertyChanged -= OnPlanChanged;
        _plan = _tab?.Plan;
        if (_tab is not { } tab || _plan is not { } plan)
            return;
        plan.PropertyChanged += OnPlanChanged;

        TreeButton.IsChecked = plan.ViewMode == PlanViewMode.Tree;
        GraphButton.IsChecked = plan.ViewMode == PlanViewMode.Graph;
        TextButton.IsChecked = plan.ViewMode == PlanViewMode.Text;
        CommandText.Text = plan.CommandText;
        ZoomText.Text = plan.ZoomText;

        var muted = Tokens.Brush(this, "SquillFg2Brush");
        Figures.Children.Clear();
        foreach (var figure in plan.Summary)
        {
            Figures.Children.Add(new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 5,
                Children =
                {
                    new TextBlock { Text = figure.Label, Foreground = muted, VerticalAlignment = VerticalAlignment.Center },
                    new TextBlock { Text = figure.Value, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, VerticalAlignment = VerticalAlignment.Center },
                },
            });
        }

        CriticalPill.Content = plan.CriticalCountText;
        CriticalPill.Visibility = plan.HasCritical ? Visibility.Visible : Visibility.Collapsed;
        WarningPill.Content = plan.WarningCountText;
        WarningPill.Visibility = plan.HasWarnings ? Visibility.Visible : Visibility.Collapsed;

        // The chip is the Analyze shortcut: only an actual plan is re-run with it (Ctrl+E re-runs an estimated one).
        var label = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, VerticalAlignment = VerticalAlignment.Center };
        label.Children.Add(new TextBlock { Text = UiText.Get("plan.action.rerun"), VerticalAlignment = VerticalAlignment.Center });
        if (tab.Mode == PlanMode.Actual)
            label.Children.Add(new ContentControl { Content = UiText.Get("plan.analyzeShortcut"), Style = (Style)Application.Current.Resources["KeyboardChipStyle"], VerticalAlignment = VerticalAlignment.Center });
        RerunButton.Content = label;
        RerunButton.IsEnabled = !tab.IsBusy;

        MessageText.Text = tab.MessageLine;
        MessageText.Visibility = tab.MessageLine.Length == 0 ? Visibility.Collapsed : Visibility.Visible;
        if (RawText.Text != plan.SourceText)
            RawText.Text = plan.SourceText;

        var text = plan.ViewMode == PlanViewMode.Text;
        RawText.Visibility = text ? Visibility.Visible : Visibility.Collapsed;
        PlanBody.Visibility = text ? Visibility.Collapsed : Visibility.Visible;
        var graph = plan.ViewMode == PlanViewMode.Graph;
        if (graph != (_splitter.Visibility == Visibility.Visible))
        {
            // Entering the graph view restores the 62 / 38 split; the table takes the whole body in the tree view.
            GraphRow.Height = graph ? new GridLength(62, GridUnitType.Star) : new GridLength(0);
            TableRow.Height = new GridLength(38, GridUnitType.Star);
            _splitter.Visibility = graph ? Visibility.Visible : Visibility.Collapsed;
            GraphHost.Visibility = _splitter.Visibility;
        }

        ((PlanGraphCanvas)GraphHost.Children[0]).Plan = graph ? plan : null;
        _table.Bind(tab, plan);
    }

    private void OnViewClicked(object sender, RoutedEventArgs e)
    {
        if (_plan is null)
            return;
        _plan.ViewMode = ReferenceEquals(sender, TreeButton) ? PlanViewMode.Tree : ReferenceEquals(sender, TextButton) ? PlanViewMode.Text : PlanViewMode.Graph;
    }

    private void OnZoomOutClicked(object sender, RoutedEventArgs e) => _plan?.ZoomOutCommand.Execute(null);

    private void OnZoomInClicked(object sender, RoutedEventArgs e) => _plan?.ZoomInCommand.Execute(null);

    private void OnRerunClicked(object sender, RoutedEventArgs e) => _tab?.RerunCommand.Execute(null);
}
