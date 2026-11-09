using Avalonia;
using Velopack;

namespace Squill.App.Avalonia;

internal static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        // Same bootstrap as the WinUI head: hooks only, a downloaded update is never applied without the user's click.
        VelopackApp.Build().SetAutoApplyOnStartup(false).Run();
        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    private static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>().UsePlatformDetect();
}
