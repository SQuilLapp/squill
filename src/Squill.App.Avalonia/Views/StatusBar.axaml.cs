using Squill.Presentation;
using System.ComponentModel;
using System.Globalization;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Threading;
using Squill.Core;
using Squill.Core.Transactions;
using Squill.Presentation.Sessions;
using Squill.Presentation.Localization;
using Squill.Presentation.Activity;
using Squill.Presentation.Compare;
using Squill.Presentation.Plans;
using Squill.Presentation.Results;
using Squill.Presentation.Security;
using Squill.Presentation.Query;
using Squill.Presentation.Workspace;

namespace Squill.App.Avalonia.Views;

/// <summary>Environment block, connection, run status, cursor position, encoding and server version of the workspace.</summary>
public sealed partial class StatusBar : UserControl
{
    private WorkspaceViewModel? _workspace;
    private QueryTabViewModel? _tab;
    private PlanTabViewModel? _plan;
    private SchemaCompareViewModel? _compare;
    private SessionsMonitorViewModel? _monitor;
    private SecurityViewModel? _security;
    private SessionTransaction? _transaction;

    /// <summary>Creates the view.</summary>
    public StatusBar()
    {
        InitializeComponent();
        DataContextChanged += (_, _) => Attach(DataContext as WorkspaceViewModel);
        ActualThemeVariantChanged += (_, _) => Refresh();
    }

    private void Attach(WorkspaceViewModel? workspace)
    {
        if (_workspace is not null)
            _workspace.PropertyChanged -= OnWorkspaceChanged;
        _workspace = workspace;
        if (_workspace is not null)
            _workspace.PropertyChanged += OnWorkspaceChanged;
        AttachTab(_workspace?.ActiveTab);
        Refresh();
    }

    /// <summary>Shows the plan status line and figures (node count, time, server version) in place of the run status and cursor position while a plan tab is active.</summary>
    /// <param name="plan">The active plan tab, or <see langword="null"/> when another kind of tab is shown.</param>
    public void ShowPlan(PlanTabViewModel? plan) => Show(ref _plan, plan);

    /// <summary>Shows the compared environments (<c>STAGING → PRODUCTION</c>), the difference counts and both server versions while a schema compare tab is active.</summary>
    /// <param name="compare">The active compare tab, or <see langword="null"/> when another kind of tab is shown.</param>
    public void ShowCompare(SchemaCompareViewModel? compare) => Show(ref _compare, compare);

    /// <summary>Shows the last read of the sessions monitor while its tab is active.</summary>
    /// <param name="monitor">The active monitor tab, or <see langword="null"/> when another kind of tab is shown.</param>
    public void ShowMonitor(SessionsMonitorViewModel? monitor) => Show(ref _monitor, monitor);

    /// <summary>Shows the number of unapplied privilege changes while a security tab is active.</summary>
    /// <param name="security">The active security tab, or <see langword="null"/> when another kind of tab is shown.</param>
    public void ShowSecurity(SecurityViewModel? security) => Show(ref _security, security);

    private void Show<T>(ref T? field, T? value)

        where T : class, INotifyPropertyChanged

    {

        if (PropertyChangedSwap.Swap(ref field, value, OnTabChanged))

            Refresh();

    }


    private void RefreshCompare()
    {
        var source = _compare?.Source?.Environment ?? ConnectionEnvironment.None;
        var show = _compare is not null && CompareFormat.Environment(source) is not null;
        SourceBlock.IsVisible = FlowArrow.IsVisible = show;
        if (!show || CompareFormat.Environment(source) is not var (suffix, key))
            return;
        SourceBlock.Background = Brush($"SquillEnv{suffix}Brush");
        SourceLabel.Foreground = Brush($"SquillOnEnv{suffix}Brush");
        SourceLabel.Text = UiText.Get(key);
    }

    private void AttachTab(QueryTabViewModel? tab)
    {
        if (_tab is not null)
        {
            _tab.PropertyChanged -= OnTabChanged;
            _tab.Editor.CaretChanged -= OnCaretChanged;
        }

        _tab = tab;
        if (_tab is not null)
        {
            _tab.PropertyChanged += OnTabChanged;
            _tab.Editor.CaretChanged += OnCaretChanged;
        }

        if (_transaction is not null)
            _transaction.PropertyChanged -= OnTabChanged;
        _transaction = _tab?.Transaction;
        if (_transaction is not null)
            _transaction.PropertyChanged += OnTabChanged;
    }

    private void OnWorkspaceChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(WorkspaceViewModel.ActiveTab))
            AttachTab(_workspace?.ActiveTab);
        Refresh();
    }

    private void OnTabChanged(object? sender, PropertyChangedEventArgs e) => Refresh();

    private void OnCaretChanged(object? sender, EventArgs e)
    {
        if (!Dispatcher.UIThread.CheckAccess())
        {
            Dispatcher.UIThread.Post(RefreshPosition);
            return;
        }
        RefreshPosition();
    }

    // Some view-model events are raised on worker threads (a connection or monitor read that finishes there); the controls are UI-thread only.
    private void Refresh()
    {
        if (!Dispatcher.UIThread.CheckAccess())
        {
            Dispatcher.UIThread.Post(Refresh);
            return;
        }
        RefreshEnvironment();
        var profile = _workspace?.ActiveSession?.Profile;
        ConnectionText.Text = profile is null ? UiText.Get("home.noConnection") : ConnectionLabel(profile);
        ConnectionDot.IsVisible = profile is null;
        ServerVersionText.Text = _compare is { } c ? $"{c.SourceServerVersion} → {c.TargetServerVersion}" : _plan?.ServerVersion ?? _workspace?.ActiveSession?.Connection.ServerVersion;
        EncodingSegment.IsVisible = _plan is null && _compare is null && _monitor is null && _security is null;
        RefreshCompare();
        RefreshTransaction();
        RefreshRunStatus();
        RefreshPosition();
    }

    private static string ConnectionLabel(ConnectionProfile profile)
    {
        var name = string.IsNullOrEmpty(profile.UserName) ? profile.Name : $"{profile.UserName}@{profile.Name}";
        return string.IsNullOrEmpty(profile.Database) ? name : $"{name} / {profile.Database}";
    }

    private void RefreshEnvironment()
    {
        var environment = _workspace?.ActiveEnvironment ?? ConnectionEnvironment.None;
        EnvironmentBlock.IsVisible = environment != ConnectionEnvironment.None;
        if (!EnvironmentBlock.IsVisible)
            return;
        var (name, key) = environment switch
        {
            ConnectionEnvironment.Production => ("Prod", "status.production"),
            ConnectionEnvironment.Staging => ("Staging", "status.staging"),
            ConnectionEnvironment.Development => ("Dev", "status.dev"),
            _ => ("Local", "status.local"),
        };
        EnvironmentBlock.Background = Brush($"SquillEnv{name}Brush");
        var onEnvironment = Brush($"SquillOnEnv{name}Brush");
        EnvironmentLabel.Foreground = onEnvironment;
        EnvironmentLabel.Text = UiText.Get(key);
        EnvironmentWarning.Stroke = onEnvironment;
        EnvironmentWarning.IsVisible = environment == ConnectionEnvironment.Production;
    }

    private IBrush? Brush(string key) => this.TryFindResource(key, ActualThemeVariant, out var value) ? value as IBrush : null;

    // The transaction belongs to the session of the active query tab: nothing is shown for plans and other kinds of tab.
    private void RefreshTransaction()
    {
        var text = _plan is null && _compare is null && _monitor is null && _security is null ? _transaction?.StatusText ?? "" : "";
        TransactionSegment.IsVisible = text.Length > 0;
        TransactionText.Text = text;
        var brush = Brush(_transaction?.State == TransactionState.Failed ? "SquillDangerBrush" : "SquillWarningBrush");
        TransactionDot.Fill = brush;
        TransactionText.Foreground = brush;
    }

    private void RefreshRunStatus()
    {
        if (_security is { } security)
        {
            StatusSegment.Text = security.StatusText;
            StatusSegment.Foreground = Brush(security.ChangeCount > 0 ? "SquillWarningBrush" : "SquillFg2Brush");
            ElapsedSegment.Text = null;
            return;
        }

        if (_monitor is { } monitor)
        {
            StatusSegment.Text = monitor.StatusText;
            StatusSegment.Foreground = Brush("SquillFg2Brush");
            ElapsedSegment.Text = null;
            return;
        }

        if (_compare is { } compare)
        {
            StatusSegment.Text = compare.DifferencesText;
            StatusSegment.Foreground = Brush("SquillFg2Brush");
            ElapsedSegment.Text = null;
            return;
        }

        if (_plan is { } plan)
        {
            StatusSegment.Text = plan.Plan.StatusLine;
            StatusSegment.Foreground = Brush("SquillFg2Brush");
            ElapsedSegment.Text = null;
            return;
        }

        var tab = _tab;
        if (tab is null)
        {
            StatusSegment.Text = null;
            ElapsedSegment.Text = null;
            return;
        }

        if (!tab.IsRunning && tab.Elapsed > TimeSpan.Zero && tab.Results.LastOrDefault() is { Kind: ResultKind.Grid, Grid: { } grid })
        {
            StatusSegment.Text = UiText.Format("results.rowsAndTime", grid.RowCount, (long)tab.Elapsed.TotalMilliseconds);
            StatusSegment.Foreground = Brush("SquillSuccessBrush");
            ElapsedSegment.Text = null;
            return;
        }

        StatusSegment.Text = tab.StatusText;
        StatusSegment.Foreground = Brush("SquillFgBrush");
        ElapsedSegment.Text = tab.Elapsed > TimeSpan.Zero ? DisplayFormat.Clock(tab.Elapsed) : null;
    }

    private void RefreshPosition()
    {
        if (_compare is not null || _monitor is not null || _security is not null)
        {
            PositionText.Text = null;
            return;
        }

        if (_plan is { } plan)
        {
            PositionText.Text = plan.SummaryText;
            return;
        }

        if (_tab is not { } tab)
        {
            PositionText.Text = null;
            return;
        }

        var text = tab.Editor.Text.AsSpan(0, Math.Clamp(tab.Editor.CaretOffset, 0, tab.Editor.Text.Length));
        var lineStart = text.LastIndexOf('\n') + 1;
        PositionText.Text = UiText.Format("status.position", text.Count('\n') + 1, text.Length - lineStart + 1);
    }
}
