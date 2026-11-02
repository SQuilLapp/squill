using System.Windows.Input;
using Squill.App.WinUI.Views;
using Squill.Core;
using Squill.Presentation.Localization;
using Xunit;

namespace Squill.App.WinUI.Tests;

public class ChromeFormatTests
{
    private static ConnectionProfile Profile(string? user, string? database) =>
        new(Guid.NewGuid(), "prod-db", DatabaseDialect.PostgreSql, "h", 5432, database, user, null, null);

    [Fact]
    public void Connection_without_profile_says_not_connected() =>
        Assert.Equal(UiText.Get("status.notConnected"), ChromeFormat.Connection(null));

    [Theory]
    [InlineData("app", "sales", "app@prod-db / sales")]
    [InlineData("app", null, "app@prod-db")]
    [InlineData(null, "sales", "prod-db / sales")]
    [InlineData("", "", "prod-db")]
    public void Connection_joins_the_parts_the_profile_has(string? user, string? database, string expected) =>
        Assert.Equal(expected, ChromeFormat.Connection(Profile(user, database)));

    [Fact]
    public void Environment_none_has_no_block() => Assert.Null(ChromeFormat.Environment(ConnectionEnvironment.None));

    [Theory]
    [InlineData(ConnectionEnvironment.Production, "Prod", "status.production", true)]
    [InlineData(ConnectionEnvironment.Staging, "Staging", "status.staging", false)]
    [InlineData(ConnectionEnvironment.Development, "Dev", "status.dev", false)]
    [InlineData(ConnectionEnvironment.Local, "Local", "status.local", false)]
    public void Environment_picks_brush_label_and_warning(ConnectionEnvironment environment, string suffix, string key, bool warning) =>
        Assert.Equal((suffix, key, warning), ChromeFormat.Environment(environment));

    [Theory]
    [InlineData("", 0, 1, 1)]
    [InlineData("select 1", 3, 1, 4)]
    [InlineData("a\nbc", 4, 2, 3)]
    [InlineData("a\n", 2, 2, 1)]
    [InlineData("abc", 99, 1, 4)]
    [InlineData("abc", -5, 1, 1)]
    public void Position_counts_lines_and_columns_from_one(string text, int caret, int line, int column) =>
        Assert.Equal(UiText.Format("status.position", line, column), ChromeFormat.Position(text, caret));

    private sealed class Command(bool canExecute) : ICommand
    {
        public event EventHandler? CanExecuteChanged { add { } remove { } }

        public bool CanExecute(object? parameter) => canExecute;

        public void Execute(object? parameter)
        {
        }
    }

    [Fact]
    public void Menu_item_is_enabled_only_for_an_executable_command()
    {
        Assert.False(ChromeFormat.IsEnabled(null, null));
        Assert.False(ChromeFormat.IsEnabled(new Command(false), null));
        Assert.True(ChromeFormat.IsEnabled(new Command(true), null));
    }
}
