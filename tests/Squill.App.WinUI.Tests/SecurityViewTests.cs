using Squill.App.WinUI.Views;
using Squill.Presentation.Security;
using Xunit;

namespace Squill.App.WinUI.Tests;

public class SecurityViewTests
{
    [Theory]
    [InlineData(PrivilegeCellDisplay.Granted, "SquillAccentBrush", "✓")]
    [InlineData(PrivilegeCellDisplay.Inherited, "SquillFg3Brush", "✓")]
    [InlineData(PrivilegeCellDisplay.PendingGrant, null, "")]
    [InlineData(PrivilegeCellDisplay.PendingRevoke, null, "−")]
    [InlineData(PrivilegeCellDisplay.Empty, null, "")]
    public void Cells_are_filled_only_when_held_and_pending_revoke_shows_a_minus(PrivilegeCellDisplay display, string? fill, string glyph)
    {
        Assert.Equal(fill, SecurityFormat.FillBrush(display));
        Assert.Equal(glyph, SecurityFormat.Glyph(display));
    }

    [Fact]
    public void Pending_cells_are_outlined_in_accent_or_danger()
    {
        Assert.Equal("SquillAccentBrush", SecurityFormat.OutlineBrush(PrivilegeCellDisplay.PendingGrant));
        Assert.Equal("SquillDangerBrush", SecurityFormat.OutlineBrush(PrivilegeCellDisplay.PendingRevoke));
    }

    [Fact]
    public void Hit_test_finds_the_cell_and_ignores_the_name_column_and_the_outside()
    {
        Assert.Equal((1, 2), SecurityFormat.HitTest(280 + 92 * 2 + 5, 28 + 3, 280, 92, 28, 3, 7));
        Assert.Null(SecurityFormat.HitTest(100, 10, 280, 92, 28, 3, 7));
        Assert.Null(SecurityFormat.HitTest(280 + 92 * 7 + 1, 10, 280, 92, 28, 3, 7));
        Assert.Null(SecurityFormat.HitTest(300, 28 * 3, 280, 92, 28, 3, 7));
    }

    [Fact]
    public void Arrow_moves_stay_inside_the_matrix()
    {
        Assert.Equal((0, 0), SecurityFormat.Move(-1, 0, 0, 0, 3, 7));
        Assert.Equal((2, 6), SecurityFormat.Move(1, 1, 2, 6, 3, 7));
        Assert.Equal((1, 4), SecurityFormat.Move(0, 1, 1, 3, 3, 7));
        Assert.Equal((0, 0), SecurityFormat.Move(1, 1, 0, 0, 0, 0));
    }

    [Fact]
    public void Security_view_builds_in_code_toggles_through_the_view_model_and_maps_the_keys()
    {
        var code = File.ReadAllText("Xaml/SecurityView.cs");

        Assert.DoesNotContain("ItemsSource", code);
        Assert.Contains("vm.ToggleCell(", code);
        Assert.Contains("vm.ToggleRow(", code);
        Assert.Contains("VirtualKey.Space", code);
        Assert.Contains("VirtualKey.S, Modifiers = VirtualKeyModifiers.Control", code);
        Assert.Contains("ApplyCommand", code);
        Assert.Contains("AutomationProperties.SetName(wrap", code);
    }

    [Fact]
    public void Main_window_hosts_the_security_tab_and_closes_it_through_its_command()
    {
        var xaml = File.ReadAllText("Xaml/MainWindow.xaml");
        var code = File.ReadAllText("Xaml/MainWindow.Diagram.cs");

        Assert.Contains("<v:SecurityView x:Name=\"SecurityArea\"", xaml);
        Assert.Contains("workspace.SecurityTabs.CollectionChanged", code);
    }
}
