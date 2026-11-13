using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Squill.Platform.Lifecycle;
using Velopack;
using WinRT;

namespace Squill.App.WinUI;

/// <summary>Process entry point: lets Velopack handle install/update hooks before the XAML application starts.</summary>
public static class Program
{
    /// <summary>Gets or sets what shows the main window; set once the window exists, called when another start of the application asked for it.</summary>
    internal static Action? ShowMainWindow { get; set; }

    /// <summary>Runs the Velopack bootstrap, then starts the WinUI application.</summary>
    [STAThread]
    public static void Main()
    {
        // A downloaded update is never applied without the user's click (docs/poc/velopack-aot.md section 2).
        // Hooks run in a short-lived process with a few seconds to live: no UI, no file written. The first-run / update
        // classification happens in the normal startup (AppComposition), so OnFirstRun and OnAfterUpdate stay unregistered.
        // The uninstaller must leave %APPDATA%\SQuilL (the user's data) alone, hence the empty hook.
        VelopackApp.Build()
            .SetAutoApplyOnStartup(false)
            .OnBeforeUninstallFastCallback(static _ => { /* intentionally empty: user data is kept */ })
            .Run();
        // One instance per user; a throwaway data directory (SQUILL_DATA_DIR) may run next to it.
        using var instance = string.IsNullOrEmpty(Environment.GetEnvironmentVariable("SQUILL_DATA_DIR"))
            ? SingleInstance.TryAcquire(SingleInstance.DefaultName, () => ShowMainWindow?.Invoke())
            : null;
        if (instance is null && string.IsNullOrEmpty(Environment.GetEnvironmentVariable("SQUILL_DATA_DIR")))
            return;
        ComWrappersSupport.InitializeComWrappers();
        Application.Start(callbackParams =>
        {
            SynchronizationContext.SetSynchronizationContext(
                new DispatcherQueueSynchronizationContext(DispatcherQueue.GetForCurrentThread()));
            GC.KeepAlive(new App());
        });
    }
}
