using Microsoft.UI.Xaml;

namespace Squill.App.WinUI;

/// <summary>Main window of the WinUI head.</summary>
public sealed partial class MainWindow : Window
{
    /// <summary>Initializes the window and sets its icon.</summary>
    public MainWindow()
    {
        InitializeComponent();
        AppWindow.SetIcon(Path.Combine(AppContext.BaseDirectory, "Assets", "squill.ico"));
    }
}
