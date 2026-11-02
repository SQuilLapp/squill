using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Squill.Presentation.Diagram;

namespace Squill.App.WinUI.Views;

/// <summary>The ER diagram of a session: a toolbar above the diagram canvas.</summary>
public sealed partial class DiagramTabView : UserControl
{
    private DiagramTabViewModel? _viewModel;

    /// <summary>Creates the view.</summary>
    public DiagramTabView()
    {
        InitializeComponent();
        ActualThemeChanged += (_, _) => SyncTheme();
    }

    /// <summary>Gets or sets the diagram shown; the view rebinds its toolbar and canvas.</summary>
    public DiagramTabViewModel? ViewModel
    {
        get => _viewModel;
        set
        {
            _viewModel = value;
            Canvas.Diagram = value;
            Bindings.Update();
            SyncTheme();
        }
    }

    private void SyncTheme()
    {
        if (_viewModel is not null)
            _viewModel.IsDarkTheme = ActualTheme == ElementTheme.Dark;
    }

    private void OnFitClicked(object sender, RoutedEventArgs e) => Canvas.FitToScreen();

    private void OnDepthChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_viewModel is not null && DepthBox.SelectedIndex >= 0)
            _viewModel.FocusDepth = DepthBox.SelectedIndex + 1;
    }
}
