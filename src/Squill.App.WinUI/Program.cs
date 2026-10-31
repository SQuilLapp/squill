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
        VelopackApp.Build().Run();
        ComWrappersSupport.InitializeComWrappers();
        Application.Start(callbackParams =>
        {
            SynchronizationContext.SetSynchronizationContext(
                new DispatcherQueueSynchronizationContext(DispatcherQueue.GetForCurrentThread()));
            GC.KeepAlive(new App());
        });
    }
}
