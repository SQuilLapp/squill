using Avalonia;
using Avalonia.Headless;

namespace Squill.App.Avalonia.Tests;

/// <summary>The single headless Avalonia session shared by every test class (only one can exist per process).</summary>
internal static class Headless
{
    public static readonly HeadlessUnitTestSession Session = HeadlessUnitTestSession.StartNew(typeof(HeadlessApp));

    /// <summary>Runs an async body on the UI thread and waits until it really finishes.</summary>
    /// <remarks>The <c>Dispatch(Func&lt;Task&gt;)</c> overload of the session returns at the first await that resumes off the UI thread (a database or file read on a slow machine), so tests that await real I/O went on without their verdict. The generic overload waits.</remarks>
    public static Task Dispatch(Func<Task> body, CancellationToken cancellationToken) =>
        Session.Dispatch<bool>(async () =>
        {
            await body();
            return true;
        }, cancellationToken);
}

/// <summary>Entry point of the headless session: real Skia rendering so tests can sample pixels.</summary>
internal static class HeadlessApp
{
    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>().UseSkia().UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false });
}
