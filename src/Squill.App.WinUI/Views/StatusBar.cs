using System.ComponentModel;
using System.Globalization;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using ConnectionEnvironment = Squill.Core.ConnectionEnvironment;
using Squill.Presentation.Localization;
using Squill.Presentation.Results;
using Squill.Presentation.Plans;
using Squill.Core.Transactions;
using Squill.Presentation.Query;
using Squill.Presentation.Sessions;
using Squill.Presentation.Workspace;

namespace Squill.App.WinUI.Views;

/// <summary>Environment block, connection, run status, cursor position, encoding and server version of the workspace.</summary>
public sealed partial class StatusBar : UserControl
{
    private readonly Border _environmentBlock = new() { Padding = new Thickness(14, 0, 14, 0), Visibility = Visibility.Collapsed };
    private readonly TextBlock _environmentLabel = new() { FontWeight = Microsoft.UI.Text.FontWeights.Bold, VerticalAlignment = VerticalAlignment.Center };
    private FrameworkElement? _environmentWarning;
    private readonly StackPanel _environmentContent = new() { Orientation = Orientation.Horizontal, Spacing = 6 };
    private readonly TextBlock _connection = Text(true);
    private readonly TextBlock _transactionText = Text(false);
    private readonly Microsoft.UI.Xaml.Shapes.Ellipse _transactionDot = new() { Width = 8, Height = 8, VerticalAlignment = VerticalAlignment.Center };
    private readonly TextBlock _status = Text(false);
    private readonly TextBlock _elapsed = Text(true);
    private readonly TextBlock _position = Text(true);
    private readonly TextBlock _encoding = Text(true);
    private readonly TextBlock _version = Text(true);
    private readonly Border _root;
    private readonly List<Border> _segments = [];
    private WorkspaceViewModel? _workspace;
    private QueryTabViewModel? _tab;
    private SessionTransaction? _transaction;
    private PlanTabViewModel? _plan;

    /// <summary>Creates the view.</summary>
    public StatusBar()
    {
        HorizontalContentAlignment = HorizontalAlignment.Stretch;
        _encoding.Text = UiText.Get("status.encoding");
        _environmentContent.Children.Add(_environmentLabel);
        _environmentBlock.Child = _environmentContent;
        var grid = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition { Width = GridLength.Auto },
                new ColumnDefinition { Width = GridLength.Auto },
                new ColumnDefinition { Width = GridLength.Auto },
                new ColumnDefinition(),
                new ColumnDefinition { Width = GridLength.Auto },
                new ColumnDefinition { Width = GridLength.Auto },
                new ColumnDefinition { Width = GridLength.Auto },
            },
            Children =
            {
                _environmentBlock,
                Segment(1, _connection, right: true),
                Segment(2, new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, Children = { _transactionDot, _transactionText } }, right: true),
                Segment(3, new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12, Children = { _status, _elapsed } }, right: false),
                Segment(4, _position, left: true),
                Segment(5, _encoding, left: true),
                Segment(6, _version, left: false),
            },
        };
        Content = new Border
        {
            Height = 26,
            BorderThickness = new Thickness(0, 1, 0, 0),
            Child = grid,
        };
        _root = (Border)Content;
        ActualThemeChanged += (_, _) => Refresh();
    }

    /// <summary>Connects the bar to the workspace it describes.</summary>
    /// <param name="workspace">The workspace view-model.</param>
    public void Attach(WorkspaceViewModel workspace)
    {
        _workspace = workspace;
        workspace.PropertyChanged += OnWorkspaceChanged;
        Refresh();
    }

    /// <summary>Shows the plan status line and the plan figures (node count, time, server version) in place of the run status and cursor position while a plan tab is active.</summary>
    /// <param name="plan">The active plan tab, or <see langword="null"/> when another kind of tab is shown.</param>
    public void ShowPlan(PlanTabViewModel? plan)
    {
        if (ReferenceEquals(_plan, plan))
            return;
        if (_plan is not null)
            _plan.PropertyChanged -= OnTabChanged;
        _plan = plan;
        if (_plan is not null)
            _plan.PropertyChanged += OnTabChanged;
        Refresh();
    }

    private static TextBlock Text(bool mono)
    {
        var text = new TextBlock
        {
            FontSize = (double)Application.Current.Resources["SquillFontSizeBodySm"],
            VerticalAlignment = VerticalAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis,
        };
        if (mono)
            text.FontFamily = (FontFamily)Application.Current.Resources["SquillFontMono"];
        return text;
    }

    private Border Segment(int column, UIElement child, bool? left = null, bool? right = null)
    {
        var border = new Border
        {
            Padding = new Thickness(12, 0, 12, 0),
            BorderThickness = new Thickness(left == true ? 1 : 0, 0, right == true ? 1 : 0, 0),
            Child = child,
        };
        Grid.SetColumn(border, column);
        _segments.Add(border);
        return border;
    }

    // Tokens are read for the theme of the bar, which the settings can override per window.
    private Brush Brush(string key) => Tokens.Brush(this, key);

    private void OnWorkspaceChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(WorkspaceViewModel.ActiveTab) or nameof(WorkspaceViewModel.ActiveSession) or nameof(WorkspaceViewModel.ActiveEnvironment))
            Refresh();
    }

    private void OnTabChanged(object? sender, PropertyChangedEventArgs e) => Refresh();

    private void OnCaretChanged(object? sender, EventArgs e) => RefreshPosition();

    private void Refresh()
    {
        var workspace = _workspace;
        if (workspace is null)
            return;
        if (!ReferenceEquals(_tab, workspace.ActiveTab))
        {
            if (_tab is not null)
            {
                _tab.PropertyChanged -= OnTabChanged;
                _tab.Editor.CaretChanged -= OnCaretChanged;
            }

            _tab = workspace.ActiveTab;
            if (_tab is not null)
            {
                _tab.PropertyChanged += OnTabChanged;
                _tab.Editor.CaretChanged += OnCaretChanged;
            }
        }

        if (!ReferenceEquals(_transaction, _tab?.Transaction))
        {
            if (_transaction is not null)
                _transaction.PropertyChanged -= OnTabChanged;
            _transaction = _tab?.Transaction;
            if (_transaction is not null)
                _transaction.PropertyChanged += OnTabChanged;
        }

        Paint();
        RefreshTransaction();
        RefreshEnvironment(workspace.ActiveEnvironment);
        _connection.Text = ChromeFormat.Connection(workspace.ActiveSession?.Profile);
        _version.Text = _plan?.ServerVersion ?? workspace.ActiveSession?.Connection.ServerVersion ?? "";
        _segments[4].Visibility = _plan is null ? Visibility.Visible : Visibility.Collapsed;
        RefreshRunStatus();
        RefreshPosition();
    }

    private void Paint()
    {
        _root.Background = Brush("SquillBgSurfaceBrush");
        _root.BorderBrush = Brush("SquillBorderSubtleBrush");
        foreach (var segment in _segments)
            segment.BorderBrush = Brush("SquillBorderSubtleBrush");
        _connection.Foreground = Brush("SquillFgBrush");
        foreach (var text in (TextBlock[])[_elapsed, _position, _encoding, _version])
            text.Foreground = Brush("SquillFg2Brush");
    }

    private void RefreshEnvironment(ConnectionEnvironment environment)
    {
        if (ChromeFormat.Environment(environment) is not var (suffix, labelKey, warning))
        {
            _environmentBlock.Visibility = Visibility.Collapsed;
            return;
        }

        var onEnvironment = Brush($"SquillOnEnv{suffix}Brush");
        _environmentBlock.Visibility = Visibility.Visible;
        _environmentBlock.Background = Brush($"SquillEnv{suffix}Brush");
        _environmentLabel.Foreground = onEnvironment;
        _environmentLabel.Text = UiText.Get(labelKey);
        _environmentLabel.FontSize = (double)Application.Current.Resources["SquillFontSizeBodySm"];
        if (_environmentWarning is not null)
            _environmentContent.Children.Remove(_environmentWarning);
        _environmentWarning = warning ? Icons.Create("SquillIconWarning", 13, onEnvironment) : null;
        if (_environmentWarning is not null)
            _environmentContent.Children.Insert(0, _environmentWarning);
    }

    // The transaction belongs to the session of the active query tab: nothing is shown for plans and other kinds of tab.
    private void RefreshTransaction()
    {
        var text = _plan is null ? _transaction?.StatusText ?? "" : "";
        _segments[1].Visibility = text.Length == 0 ? Visibility.Collapsed : Visibility.Visible;
        _transactionText.Text = text;
        var brush = Brush(_transaction?.State == TransactionState.Failed ? "SquillDangerBrush" : "SquillWarningBrush");
        _transactionDot.Fill = brush;
        _transactionText.Foreground = brush;
    }

    private void RefreshRunStatus()
    {
        if (_plan is { } plan)
        {
            _status.Text = plan.Plan.StatusLine;
            _status.Foreground = Brush("SquillFg2Brush");
            _elapsed.Text = "";
            return;
        }

        var tab = _tab;
        if (tab is null)
        {
            _status.Text = "";
            _elapsed.Text = "";
            return;
        }

        if (!tab.IsRunning && tab.Elapsed > TimeSpan.Zero && tab.Results.LastOrDefault() is { Kind: ResultKind.Grid, Grid: { } grid })
        {
            _status.Text = UiText.Format("results.rowsAndTime", grid.RowCount, (long)tab.Elapsed.TotalMilliseconds);
            _status.Foreground = Brush("SquillSuccessBrush");
            _elapsed.Text = "";
            return;
        }

        _status.Text = tab.StatusText;
        _status.Foreground = Brush("SquillFg2Brush");
        _elapsed.Text = tab.Elapsed > TimeSpan.Zero ? DisplayFormat.Clock(tab.Elapsed) : "";
    }

    private void RefreshPosition() =>
        _position.Text = _plan is { } plan ? plan.SummaryText : _tab is { } tab ? ChromeFormat.Position(tab.Editor.Text, tab.Editor.CaretOffset) : "";
}
