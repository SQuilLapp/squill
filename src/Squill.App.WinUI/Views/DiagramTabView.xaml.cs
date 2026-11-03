using System.ComponentModel;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Squill.App.WinUI.Controls;
using Squill.Presentation.Diagram;
using Squill.Presentation.Localization;

namespace Squill.App.WinUI.Views;

/// <summary>The ER diagram of a session: a toolbar, the table list and the diagram canvas.</summary>
public sealed partial class DiagramTabView : UserControl
{
    private const double ZoomStep = 1.1;

    private DiagramTabViewModel? _viewModel;
    private ErModel? _listed;
    private ErEntity[] _entities = [];
    private bool _syncing;

    /// <summary>Creates the view.</summary>
    public DiagramTabView()
    {
        InitializeComponent();
        TypesBox.Checked += OnTypesChanged;
        TypesBox.Unchecked += OnTypesChanged;
        ActualThemeChanged += (_, _) => SyncTheme();
    }

    /// <summary>Gets or sets the diagram shown; the view rebinds its toolbar, table list and canvas.</summary>
    public DiagramTabViewModel? ViewModel
    {
        get => _viewModel;
        set
        {
            if (_viewModel is not null)
                _viewModel.PropertyChanged -= OnDiagramChanged;
            _viewModel = value;
            if (_viewModel is not null)
                _viewModel.PropertyChanged += OnDiagramChanged;
            Canvas.Diagram = value;
            Bindings.Update();
            SyncTheme();
            SyncChrome();
        }
    }

    private void SyncTheme()
    {
        if (_viewModel is not null)
            _viewModel.IsDarkTheme = ActualTheme == ElementTheme.Dark;
    }

    private void OnDiagramChanged(object? sender, PropertyChangedEventArgs e) => SyncChrome();

    private void SyncChrome()
    {
        if (_viewModel is not { } d)
            return;
        ZoomText.Text = ErDiagramGeometry.ZoomText(d.Zoom);
        TablesHeader.Text = UiText.Format("er.tables", d.Model.Entities.Count, d.Session.Schema.Tables.Count);
        _syncing = true;
        if (!ReferenceEquals(_listed, d.Model))
        {
            _listed = d.Model;
            _entities = [.. d.Model.Entities];
            TableList.ItemsSource = _entities;
        }

        TableList.SelectedItem = d.SelectedKey is null ? null : _entities.FirstOrDefault(e => string.Equals(e.Key, d.SelectedKey, StringComparison.OrdinalIgnoreCase));
        _syncing = false;
    }

    private void OnTableSelected(object sender, SelectionChangedEventArgs e)
    {
        if (!_syncing && _viewModel is { } d && TableList.SelectedItem is ErEntity entity)
            d.SelectedKey = entity.Key;
    }

    private void OnFitClicked(object sender, RoutedEventArgs e) => Canvas.FitToScreen();

    private void OnTypesChanged(object sender, RoutedEventArgs e) => Canvas.ShowTypes = TypesBox.IsChecked == true;

    private void OnZoomInClicked(object sender, RoutedEventArgs e) => ZoomBy(ZoomStep);

    private void OnZoomOutClicked(object sender, RoutedEventArgs e) => ZoomBy(1 / ZoomStep);

    private void ZoomBy(double factor) =>
        _viewModel?.ZoomAt(new ErPoint(Canvas.CanvasWidth / 2, Canvas.CanvasHeight / 2), factor);

    private void OnDepthClicked(object sender, RoutedEventArgs e)
    {
        if (_viewModel is not null && sender is RadioButton { Content: string text } && int.TryParse(text, out var depth))
            _viewModel.FocusDepth = depth;
    }
}
