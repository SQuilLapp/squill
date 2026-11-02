using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Styling;
using Squill.Presentation.Diagram;

namespace Squill.App.Avalonia.Views;

/// <summary>The ER diagram of a session: a toolbar above the diagram canvas.</summary>
public sealed partial class DiagramTabView : UserControl
{
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
        SyncTheme();
    }

    private void SyncTheme()
    {
        if (DataContext is DiagramTabViewModel diagram)
            diagram.IsDarkTheme = ActualThemeVariant == ThemeVariant.Dark;
    }

    private void OnFitClicked(object? sender, RoutedEventArgs e) => Canvas.FitToScreen();

    private void OnDepthChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (DataContext is DiagramTabViewModel diagram && DepthBox.SelectedIndex >= 0)
            diagram.FocusDepth = DepthBox.SelectedIndex + 1;
    }
}
