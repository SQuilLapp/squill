using Avalonia.Headless;

namespace Squill.App.Avalonia.Tests;

/// <summary>The single headless Avalonia session shared by every test class (only one can exist per process).</summary>
internal static class Headless
{
    public static readonly HeadlessUnitTestSession Session = HeadlessUnitTestSession.StartNew(typeof(App));
}
