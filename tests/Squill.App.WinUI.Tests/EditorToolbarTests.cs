using Squill.App.WinUI.Services;
using Squill.App.WinUI.Views;
using Squill.Core;
using Squill.Platform.Storage;
using Squill.Presentation.Localization;
using Xunit;

namespace Squill.App.WinUI.Tests;

public class EditorToolbarTests
{
    [Fact]
    public void ParameterRow_needs_a_value_unless_null_is_passed()
    {
        var row = new ParameterPromptRow("id");
        Assert.Equal(UiText.Get("editor.valueRequired"), row.Error);
        Assert.False(ParameterPromptRow.AllValid([row]));

        row.Text = "7";
        Assert.Null(row.Error);

        row.Text = "";
        row.IsNull = true;
        Assert.Null(row.Error);
        Assert.True(ParameterPromptRow.AllValid([row, new ParameterPromptRow("x") { Text = "a" }]));
    }

    [Fact]
    public void HistoryDetail_shows_time_duration_and_known_row_count()
    {
        using var language = UiText.Override(UiLanguage.French);
        var now = new DateTimeOffset(2026, 11, 2, 12, 0, 0, TimeSpan.Zero);
        var today = new HistoryEntry(now, Guid.Empty, "select 1", true, 1208, TimeSpan.FromMilliseconds(42));
        var older = new HistoryEntry(now.AddDays(-3), Guid.Empty, "select 1", false, null, TimeSpan.FromSeconds(2.5));

        var first = HistoryFormat.Detail(today, now);
        var second = HistoryFormat.Detail(older, now);

        Assert.StartsWith(now.ToLocalTime().ToString("t", UiText.Culture), first);
        Assert.Contains("42 ms", first);
        Assert.EndsWith("lignes", first);
        Assert.StartsWith(now.AddDays(-3).ToLocalTime().ToString("g", UiText.Culture), second);
        Assert.EndsWith("2,5 s", second);
    }

    [Theory]
    [InlineData(ConnectionEnvironment.None, null)]
    [InlineData(ConnectionEnvironment.Production, "Prod")]
    [InlineData(ConnectionEnvironment.Local, "Local")]
    public void EnvironmentStripe_uses_the_environment_brush_or_nothing(ConnectionEnvironment environment, string? suffix) =>
        Assert.Equal(suffix, ChromeFormat.Environment(environment)?.Suffix);
}
