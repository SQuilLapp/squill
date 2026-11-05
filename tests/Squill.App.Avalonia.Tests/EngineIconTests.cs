using Avalonia;
using Avalonia.Media.Imaging;
using Squill.App.Avalonia.Controls;
using Squill.Core;
using Xunit;

namespace Squill.App.Avalonia.Tests;

[Collection("Headless")]
public sealed class EngineIconTests
{
    public static TheoryData<DatabaseDialect> Engines => new() { DatabaseDialect.PostgreSql, DatabaseDialect.MySql, DatabaseDialect.SqlServer, DatabaseDialect.Sqlite, DatabaseDialect.Oracle };

    [Theory]
    [MemberData(nameof(Engines))]
    public async Task Render_Engine_DrawsItsLogo(DatabaseDialect dialect) => await Headless.Session.Dispatch(() =>
    {
        var icon = new EngineIcon { Dialect = dialect, Width = 40, Height = 40 };
        icon.Measure(new Size(40, 40));
        icon.Arrange(new Rect(0, 0, 40, 40));
        var bitmap = new RenderTargetBitmap(new PixelSize(40, 40));
        bitmap.Render(icon);

        var pixels = new byte[40 * 40 * 4];
        var handle = System.Runtime.InteropServices.GCHandle.Alloc(pixels, System.Runtime.InteropServices.GCHandleType.Pinned);
        try
        {
            bitmap.CopyPixels(new PixelRect(0, 0, 40, 40), handle.AddrOfPinnedObject(), pixels.Length, 40 * 4);
        }
        finally
        {
            handle.Free();
        }

        var painted = Enumerable.Range(0, 40 * 40).Count(i => pixels[(i * 4) + 3] > 0);
        Assert.InRange(painted, 160, 40 * 40);
    }, TestContext.Current.CancellationToken);
}
