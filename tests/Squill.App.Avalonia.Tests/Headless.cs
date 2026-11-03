using Avalonia;
using Avalonia.Headless;

namespace Squill.App.Avalonia.Tests;

/// <summary>The single headless Avalonia session shared by every test class (only one can exist per process).</summary>
internal static class Headless
{
    public static readonly HeadlessUnitTestSession Session = HeadlessUnitTestSession.StartNew(typeof(HeadlessApp));
}

/// <summary>Entry point of the headless session: real Skia rendering so tests can sample pixels.</summary>
internal static class HeadlessApp
{
    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>().UseSkia().UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false });
}
