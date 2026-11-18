using Squill.Presentation.Home;
using Xunit;

namespace Squill.App.WinUI.Tests;

public class HomePageTests
{
    [Fact]
    public void Home_page_builds_its_elements_in_code_and_never_binds_a_list_of_objects()
    {
        var code = File.ReadAllText("Xaml/HomePage.cs");

        Assert.DoesNotContain("ItemsSource", code);
        Assert.DoesNotContain("ItemsRepeater", code);
    }

    [Fact]
    public void Main_window_hosts_the_home_page_in_the_main_area_and_binds_one_shortcut_per_card()
    {
        var xaml = File.ReadAllText("Xaml/MainWindow.xaml");
        var code = File.ReadAllText("Xaml/MainWindow.Home.cs");

        Assert.Contains("<v:HomePage x:Name=\"HomeArea\"", xaml);
        Assert.Contains("HomeViewModel.MaxCards", code);
        Assert.Contains("IsHomeVisible", code);
        Assert.Equal(6, HomeViewModel.MaxCards);
    }

    [Fact]
    public void Home_page_draws_every_card_part_from_the_view_model()
    {
        var code = File.ReadAllText("Xaml/HomePage.cs");

        foreach (var part in new[] { "card.Name", "card.Subtitle", "card.Details", "card.OpenTabsText", "card.Shortcut", "card.EnvironmentText", "card.EngineLetters", "home.TipSegments", "home.ShowAtStartup" })
            Assert.Contains(part, code);
    }
}
