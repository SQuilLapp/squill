using Squill.App.WinUI.Views;
using Squill.Presentation.Activity;
using Xunit;

namespace Squill.App.WinUI.Tests;

public class SessionsMonitorViewTests
{
    [Theory]
    [InlineData(StateDotKind.Success, "SquillSuccessBrush")]
    [InlineData(StateDotKind.Warning, "SquillWarningBrush")]
    [InlineData(StateDotKind.Danger, "SquillDangerBrush")]
    [InlineData(StateDotKind.Muted, "SquillFg3Brush")]
    public void State_dots_use_the_token_of_their_kind(StateDotKind kind, string brush) =>
        Assert.Equal(brush, SessionsFormat.StateBrush(kind));

    [Fact]
    public void Durations_turn_warning_then_danger_and_the_sort_arrow_follows_the_direction()
    {
        Assert.Equal("SquillFgBrush", SessionsFormat.DurationBrush(ActivitySeverity.Normal));
        Assert.Equal("SquillWarningBrush", SessionsFormat.DurationBrush(ActivitySeverity.Warning));
        Assert.Equal("SquillDangerBrush", SessionsFormat.DurationBrush(ActivitySeverity.Danger));
        Assert.Equal("↓", SessionsFormat.SortArrow(true));
        Assert.Equal("↑", SessionsFormat.SortArrow(false));
    }

    [Fact]
    public void Monitor_view_builds_its_table_in_code_selects_by_id_and_maps_the_keys()
    {
        var code = File.ReadAllText("Xaml/SessionsMonitorView.cs");

        Assert.DoesNotContain("ItemsSource", code);
        Assert.Contains("SelectSession(id)", code);
        Assert.Contains("VirtualKey.P", code);
        Assert.Contains("VirtualKey.Delete", code);
        Assert.Contains("TerminateSessionCommand", code);
        Assert.Contains("VirtualKey.F, VirtualKeyModifiers.Control", code);
    }

    [Fact]
    public void Main_window_hosts_the_monitor_tab_and_closes_it_through_its_command()
    {
        var xaml = File.ReadAllText("Xaml/MainWindow.xaml");
        var code = File.ReadAllText("Xaml/MainWindow.Diagram.cs");

        Assert.Contains("<v:SessionsMonitorView x:Name=\"MonitorArea\"", xaml);
        Assert.Contains("workspace.Monitors.CollectionChanged", code);
    }
}
