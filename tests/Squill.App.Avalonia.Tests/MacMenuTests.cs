using Avalonia.Controls;
using Avalonia.Input;
using Xunit;

namespace Squill.App.Avalonia.Tests;

[Collection("Headless")]
public sealed class MacMenuTests
{
    [Fact]
    public Task Mirror_LeavesAppMenuItems_TurnsCtrlIntoCmd_AndTrimsSeparators() => Headless.Session.Dispatch(() =>
    {
        var query = new MenuItem { Header = "Query" };
        query.Items.Add(new MenuItem { Header = "About", Classes = { "appmenu" } });
        query.Items.Add(new Separator());
        query.Items.Add(new MenuItem { Header = "Run", InputGesture = new KeyGesture(Key.Enter, KeyModifiers.Control | KeyModifiers.Shift) });
        query.Items.Add(new Separator());
        query.Items.Add(new Separator());
        query.Items.Add(new MenuItem { Header = "Stop", InputGesture = new KeyGesture(Key.Escape) });
        query.Items.Add(new Separator());

        var items = MainWindow.Mirror(query)!.Menu!.Items;

        Assert.Equal(3, items.Count);
        var run = Assert.IsType<NativeMenuItem>(items[0]);
        Assert.Equal("Run", run.Header);
        Assert.Equal(new KeyGesture(Key.Enter, KeyModifiers.Meta | KeyModifiers.Shift), run.Gesture);
        Assert.IsType<NativeMenuItemSeparator>(items[1]);
        Assert.Null(Assert.IsType<NativeMenuItem>(items[2]).Gesture);
    }, TestContext.Current.CancellationToken);

    [Fact]
    public void LocalizedDefault_TranslatesAvaloniaItems_AndLeavesOthers()
    {
        Assert.Equal("Masquer SQuilL", MainWindow.LocalizedDefault("Hide SQuilL", "SQuilL"));
        Assert.Equal("Quitter SQuilL", MainWindow.LocalizedDefault("Quit", "SQuilL"));
        Assert.Equal("Tout afficher", MainWindow.LocalizedDefault("Show All", "SQuilL"));
        Assert.Null(MainWindow.LocalizedDefault("Paramètres", "SQuilL"));
    }

    [Fact]
    public Task Mirror_DropsASubmenuWithOnlyAppMenuItems() => Headless.Session.Dispatch(() =>
    {
        var tools = new MenuItem { Header = "Tools" };
        tools.Items.Add(new MenuItem { Header = "Settings", Classes = { "appmenu" } });

        Assert.Null(MainWindow.Mirror(tools));
        Assert.NotNull(MainWindow.Mirror(new MenuItem { Header = "Settings", Classes = { "appmenu" } }, appMenu: true));
    }, TestContext.Current.CancellationToken);
}
