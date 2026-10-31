using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;

namespace Squill.App.Avalonia.Views;

/// <summary>Connection name, server version, status text, elapsed time and a link to the data directory.</summary>
public sealed partial class StatusBar : UserControl
{
    /// <summary>Creates the view.</summary>
    public StatusBar() => InitializeComponent();

    /// <summary>Gets or sets the directory the "Data folder" link opens in the system file manager.</summary>
    public string? DataDirectory { get; set; }

    private async void OnOpenDataFolder(object? sender, RoutedEventArgs e)
    {
        if (DataDirectory is null || TopLevel.GetTopLevel(this) is not { } top)
            return;
        Directory.CreateDirectory(DataDirectory);
        await top.Launcher.LaunchDirectoryInfoAsync(new DirectoryInfo(DataDirectory));
    }
}
