using Squill.Core;
using Squill.Data;
using Squill.Platform.Profiles;
using Squill.Platform.Ssh;
using Squill.Presentation.Localization;
using Squill.Presentation.Sessions;
using Xunit;

namespace Squill.Presentation.Tests.Sessions;

/// <summary>How <see cref="SessionManager"/> turns the three host key outcomes into a session, a cancellation or an error.</summary>
public sealed class SshHostKeyPromptTests
{
    private static readonly SshHostKeyRequest Key = new("jump", 22, "ssh-ed25519", "SHA256:presented");
    private static readonly SshTunnelSettings Ssh = new("jump", 22, "me", SshAuthMethod.Password, null);

    private static ConnectionProfile Profile(SshTunnelSettings? ssh) =>
        new(Guid.NewGuid(), "Prod", DatabaseDialect.PostgreSql, "db.internal", 5432, "app", "me", null, ssh);

    private static SessionManager Manager(Func<SshHostKeyPrompt?, ValueTask>? onOpen, SshHostKeyPrompt? prompt, out List<string> log)
    {
        log = [];
        var registry = new ProviderRegistry();
        registry.Register(new SessionManagerTests.FakeProvider(log));
        return new SessionManager(registry, new SessionManagerTests.FakeStore(), new SessionManagerTests.FakeTunnels(log) { OnOpen = onOpen }, prompt);
    }

    [Fact]
    public async Task First_use_accepted_opens_the_session()
    {
        var asked = new List<SshHostKeyRequest>();
        var manager = Manager(async p => Assert.True(await p!(Key, CancellationToken.None)), (r, _) => { asked.Add(r); return ValueTask.FromResult(true); }, out _);

        await manager.OpenAsync(Profile(Ssh), CancellationToken.None);

        Assert.Equal(Key, Assert.Single(asked));
        Assert.Single(manager.Sessions);
    }

    [Fact]
    public async Task First_use_declined_is_a_cancellation_and_opens_nothing()
    {
        var manager = Manager(async p =>
        {
            if (!await p!(Key, CancellationToken.None))
                throw new SshHostKeyRejectedException(Key);
        }, (_, _) => ValueTask.FromResult(false), out _);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await manager.OpenAsync(Profile(Ssh), CancellationToken.None));

        Assert.Empty(manager.Sessions);
    }

    [Theory]
    [InlineData(UiLanguage.French, "diffère")]
    [InlineData(UiLanguage.English, "differs")]
    public async Task Changed_key_shows_both_fingerprints_and_the_file(UiLanguage language, string expected)
    {
        using var scope = UiText.Override(language);
        var manager = Manager(_ => throw new SshHostKeyChangedException(Key, @"C:\data\known-hosts.json", "SHA256:stored"), null, out _);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(async () => await manager.OpenAsync(Profile(Ssh), CancellationToken.None));

        Assert.Contains(expected, ex.Message, StringComparison.Ordinal);
        Assert.Contains("SHA256:stored", ex.Message, StringComparison.Ordinal);
        Assert.Contains("SHA256:presented", ex.Message, StringComparison.Ordinal);
        Assert.Contains(@"C:\data\known-hosts.json", ex.Message, StringComparison.Ordinal);
        Assert.Contains("jump:22", ex.Message, StringComparison.Ordinal);
        Assert.Empty(manager.Sessions);
    }

    [Fact]
    public async Task Profile_without_ssh_never_asks()
    {
        var asked = false;
        var manager = Manager(null, (_, _) => { asked = true; return ValueTask.FromResult(true); }, out _);

        await manager.OpenAsync(Profile(null), CancellationToken.None);

        Assert.False(asked);
    }
}
