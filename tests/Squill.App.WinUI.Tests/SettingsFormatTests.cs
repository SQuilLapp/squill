using Squill.App.WinUI.Views;
using Squill.Platform.Settings;
using Xunit;

namespace Squill.App.WinUI.Tests;

public class SettingsFormatTests
{
    [Theory]
    [InlineData(ThemeMode.System, 0)]
    [InlineData(ThemeMode.Light, 1)]
    [InlineData(ThemeMode.Dark, 2)]
    public void Theme_cards_are_drawn_system_light_dark(ThemeMode theme, int index) =>
        Assert.Equal(index, SettingsFormat.ThemeIndex(theme));
}
