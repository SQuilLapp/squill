using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Velopack;
using WinRT;

namespace Squill.App.WinUI;

/// <summary>Process entry point: lets Velopack handle install/update hooks before the XAML application starts.</summary>
public static class Program
{
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
        ComWrappersSupport.InitializeComWrappers();
        Application.Start(callbackParams =>
        {
            SynchronizationContext.SetSynchronizationContext(
                new DispatcherQueueSynchronizationContext(DispatcherQueue.GetForCurrentThread()));
            GC.KeepAlive(new App());
        });
    }
}
