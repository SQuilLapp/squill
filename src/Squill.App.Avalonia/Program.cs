using Avalonia;
using Squill.Platform.Lifecycle;
using Velopack;

namespace Squill.App.Avalonia;

internal static class Program
{
    /// <summary>Gets or sets what shows the main window; set once the window exists, called when another start of the application asked for it.</summary>
    internal static Action? ShowMainWindow { get; set; }

    [STAThread]
    public static void Main(string[] args)
    {
        // Same bootstrap as the WinUI head: hooks only, a downloaded update is never applied without the user's click.
        VelopackApp.Build().SetAutoApplyOnStartup(false).Run();
        // One instance per user; a throwaway data directory (SQUILL_DATA_DIR) may run next to it.
        var throwaway = !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("SQUILL_DATA_DIR"));
        using var instance = throwaway ? null : SingleInstance.TryAcquire(SingleInstance.DefaultName, () => ShowMainWindow?.Invoke());
        if (instance is null && !throwaway)
            return;
        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    private static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>().UsePlatformDetect();
}
