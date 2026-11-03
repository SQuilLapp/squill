using System.ComponentModel;
using System.Globalization;
using Avalonia.Controls;
using Avalonia.Media;
using Squill.Core;
using Squill.Presentation.Localization;
using Squill.Presentation.Query;
using Squill.Presentation.Workspace;

namespace Squill.App.Avalonia.Views;

/// <summary>Environment block, connection, run status, cursor position, encoding and server version of the workspace.</summary>
public sealed partial class StatusBar : UserControl
{
    private WorkspaceViewModel? _workspace;
    private QueryTabViewModel? _tab;

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
    }

    private void OnWorkspaceChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(WorkspaceViewModel.ActiveTab))
            AttachTab(_workspace?.ActiveTab);
        Refresh();
    }

    private void OnTabChanged(object? sender, PropertyChangedEventArgs e) => Refresh();

    private void OnCaretChanged(object? sender, EventArgs e) => RefreshPosition();

    private void Refresh()
    {
        RefreshEnvironment();
        var profile = _workspace?.ActiveSession?.Profile;
        ConnectionText.Text = profile is null ? UiText.Get("status.notConnected") : ConnectionLabel(profile);
        ServerVersionText.Text = _workspace?.ActiveSession?.Connection.ServerVersion;
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

    private void RefreshRunStatus()
    {
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
        ElapsedSegment.Text = tab.Elapsed > TimeSpan.Zero ? tab.Elapsed.ToString(@"m\:ss\.fff", CultureInfo.InvariantCulture) : null;
    }

    private void RefreshPosition()
    {
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
