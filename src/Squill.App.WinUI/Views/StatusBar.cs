using System.ComponentModel;
using System.Globalization;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Squill.Presentation.Query;
using Squill.Presentation.Workspace;

namespace Squill.App.WinUI.Views;

/// <summary>Connection name, server version, status text and elapsed time.</summary>
public sealed partial class StatusBar : UserControl
{
    private readonly TextBlock _connection = new() { Margin = new Thickness(0, 0, 12, 0), Foreground = Brush("SquillFgBrush") };
    private readonly TextBlock _version = new() { Margin = new Thickness(0, 0, 12, 0), Foreground = Brush("SquillFgDisabledBrush") };
    private readonly TextBlock _status = new() { Margin = new Thickness(0, 0, 12, 0), Foreground = Brush("SquillFgBrush") };
    private readonly TextBlock _elapsed = new() { Foreground = Brush("SquillFgDisabledBrush") };
    private WorkspaceViewModel? _workspace;
    private QueryTabViewModel? _tab;

    /// <summary>Creates the view.</summary>
    public StatusBar()
    {
        HorizontalContentAlignment = HorizontalAlignment.Stretch;
        Content = new Border
        {
            Background = Brush("SquillBgSurfaceBrush"),
            BorderBrush = Brush("SquillBorderSubtleBrush"),
            BorderThickness = new Thickness(0, 1, 0, 0),
            Padding = new Thickness(8, 2, 8, 2),
            Child = new StackPanel { Orientation = Orientation.Horizontal, Children = { _connection, _version, _status, _elapsed } },
        };
    }

    /// <summary>Connects the bar to the workspace it describes.</summary>
    /// <param name="workspace">The workspace view-model.</param>
    public void Attach(WorkspaceViewModel workspace)
    {
        _workspace = workspace;
        workspace.PropertyChanged += OnWorkspaceChanged;
        Refresh();
    }

    private static Brush Brush(string key) => (Brush)Application.Current.Resources[key];

    private void OnWorkspaceChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(WorkspaceViewModel.ActiveTab) or nameof(WorkspaceViewModel.ActiveSession))
            Refresh();
    }

    private void OnTabChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(QueryTabViewModel.StatusText) or nameof(QueryTabViewModel.Elapsed))
            Refresh();
    }

    private void Refresh()
    {
        var workspace = _workspace!;
        if (!ReferenceEquals(_tab, workspace.ActiveTab))
        {
            if (_tab is not null)
                _tab.PropertyChanged -= OnTabChanged;
            _tab = workspace.ActiveTab;
            if (_tab is not null)
                _tab.PropertyChanged += OnTabChanged;
        }

        _connection.Text = workspace.ActiveSession?.Profile.Name ?? "Not connected";
        _version.Text = workspace.ActiveSession?.Connection.ServerVersion ?? "";
        _status.Text = _tab?.StatusText ?? "";
        _elapsed.Text = _tab is null ? "" : _tab.Elapsed.ToString(@"m\:ss\.fff", CultureInfo.InvariantCulture);
    }
}
