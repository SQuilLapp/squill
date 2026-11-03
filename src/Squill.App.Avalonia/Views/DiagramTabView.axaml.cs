using System.ComponentModel;
using System.Globalization;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Styling;
using Squill.Presentation.Diagram;
using Squill.Presentation.Localization;

namespace Squill.App.Avalonia.Views;

/// <summary>The ER diagram of a session: a toolbar, the table list and the diagram canvas.</summary>
public sealed partial class DiagramTabView : UserControl
{
    private const double ZoomStep = 1.1;

    private DiagramTabViewModel? _bound;
    private bool _syncing;

    /// <summary>Creates the view.</summary>
    public DiagramTabView()
    {
        InitializeComponent();
        ActualThemeVariantChanged += (_, _) => SyncTheme();
    }

    /// <summary>Gets the canvas drawing the diagram.</summary>
    public Controls.ErDiagramCanvas DiagramCanvas => Canvas;

    /// <inheritdoc />
    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        if (_bound is not null)
            _bound.PropertyChanged -= OnDiagramChanged;
        _bound = DataContext as DiagramTabViewModel;
        if (_bound is not null)
            _bound.PropertyChanged += OnDiagramChanged;
        SyncTheme();
        SyncChrome();
    }

    private void SyncTheme()
    {
        if (DataContext is DiagramTabViewModel diagram)
            diagram.IsDarkTheme = ActualThemeVariant == ThemeVariant.Dark;
    }

    private void OnDiagramChanged(object? sender, PropertyChangedEventArgs e) => SyncChrome();

    private void SyncChrome()
    {
        if (_bound is not { } d)
            return;
        ZoomText.Text = Math.Round(d.Zoom * 100).ToString(CultureInfo.InvariantCulture) + " %";
        TablesHeader.Text = UiText.Format("er.tables", d.Model.Entities.Count, d.Session.Schema.Tables.Count);
        _syncing = true;
        TableList.SelectedItem = d.SelectedKey is null ? null : d.Model.Find(d.SelectedKey);
        _syncing = false;
    }

    private void OnTableSelected(object? sender, SelectionChangedEventArgs e)
    {
        if (!_syncing && _bound is { } d && TableList.SelectedItem is ErEntity entity)
            d.SelectedKey = entity.Key;
    }

    private void OnFitClicked(object? sender, RoutedEventArgs e) => Canvas.FitToScreen();

    private void OnTypesChanged(object? sender, RoutedEventArgs e) => Canvas.ShowTypes = TypesBox.IsChecked == true;

    private void OnZoomInClicked(object? sender, RoutedEventArgs e) => ZoomBy(ZoomStep);

    private void OnZoomOutClicked(object? sender, RoutedEventArgs e) => ZoomBy(1 / ZoomStep);

    private void ZoomBy(double factor) =>
        _bound?.ZoomAt(new ErPoint(Canvas.Bounds.Width / 2, Canvas.Bounds.Height / 2), factor);

    private void OnDepthClicked(object? sender, RoutedEventArgs e)
    {
        if (_bound is not null && sender is RadioButton { Content: string text } && int.TryParse(text, CultureInfo.InvariantCulture, out var depth))
            _bound.FocusDepth = depth;
    }
}
