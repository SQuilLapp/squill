using Squill.Core;
using Squill.Presentation.Home;
using Squill.Presentation.Localization;
using Xunit;

namespace Squill.Presentation.Tests.Home;

public sealed class HomeRecentCardTests
{
    private static HomeRecentCardViewModel Card(DatabaseDialect dialect, Func<Task> open) =>
        new(new ConnectionProfile(Guid.NewGuid(), "db", dialect, "host", 1521, "svc", "u", null, null), "hier", "Ctrl+1", open);

    [Fact]
    public void Oracle_HasABadgeAndAName()
    {
        var card = Card(DatabaseDialect.Oracle, () => Task.CompletedTask);

        Assert.Equal("OR", card.EngineLetters);
        Assert.StartsWith("Oracle", card.Details, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ClickWhileConnecting_ShowsItUntilTheConnectionIsOpen()
    {
        using var scope = UiText.Override(UiLanguage.English);
        var gate = new TaskCompletionSource();
        var card = Card(DatabaseDialect.Oracle, () => gate.Task);
        var changed = new List<string?>();
        card.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        var running = card.OpenCommand.ExecuteAsync(null);

        Assert.True(card.IsConnecting);
        Assert.Equal("Connecting...", card.Details);
        Assert.Contains(nameof(HomeRecentCardViewModel.Details), changed);

        gate.SetResult();
        await running;

        Assert.False(card.IsConnecting);
        Assert.StartsWith("Oracle", card.Details, StringComparison.Ordinal);
    }
}
