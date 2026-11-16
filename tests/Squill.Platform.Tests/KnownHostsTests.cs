using Squill.Platform;
using Squill.Platform.Ssh;
using Xunit;
using static Squill.Platform.Ssh.SshNetTunnelFactory;

namespace Squill.Platform.Tests;

public sealed class KnownHostsTests : IDisposable
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private readonly string _dir = Path.Combine(Path.GetTempPath(), "squill-kh-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_dir))
        {
            Directory.Delete(_dir, recursive: true);
        }
    }

    private static SshHostKeyRequest Key(string fingerprint = "SHA256:abc", string host = "Jump.Example") => new(host, 22, "ssh-ed25519", fingerprint);

    [Fact]
    public async Task Json_store_round_trips_and_lowercases_the_host()
    {
        using var store = new JsonKnownHostsStore(new AppPaths(_dir));
        await store.SaveAsync(Key(), Ct);
        await store.SaveAsync(Key("SHA256:new"), Ct);

        Assert.Equal("SHA256:new", await store.FindAsync("jump.example", 22, "ssh-ed25519", Ct));
        Assert.Null(await store.FindAsync("jump.example", 2222, "ssh-ed25519", Ct));
        Assert.Null(await store.FindAsync("jump.example", 22, "ssh-rsa", Ct));
        Assert.Equal(Path.Combine(_dir, "known-hosts.json"), store.Location);
    }

    [Fact]
    public async Task Json_store_treats_a_corrupted_file_as_empty()
    {
        Directory.CreateDirectory(_dir);
        await File.WriteAllTextAsync(Path.Combine(_dir, "known-hosts.json"), "{not json", Ct);
        using var store = new JsonKnownHostsStore(new AppPaths(_dir));

        Assert.Null(await store.FindAsync("jump.example", 22, "ssh-ed25519", Ct));
        await store.SaveAsync(Key(), Ct);
        Assert.Equal("SHA256:abc", await store.FindAsync("jump.example", 22, "ssh-ed25519", Ct));
    }

    [Theory]
    [InlineData(null, true, "Trusted", true)]
    [InlineData(null, false, "Rejected", false)]
    [InlineData("SHA256:abc", false, "Trusted", false)]
    [InlineData("SHA256:other", true, "Changed", false)]
    public async Task Decide_applies_trust_on_first_use(string? known, bool answer, string expected, bool saved)
    {
        var store = new InMemoryKnownHostsStore();
        if (known is not null)
        {
            await store.SaveAsync(Key(known), Ct);
        }

        var prompts = 0;
        var verdict = await DecideAsync(store, Key(), (_, _) => { prompts++; return ValueTask.FromResult(answer); }, Ct);

        Assert.Equal(expected, verdict.ToString());
        Assert.Equal(known is null ? 1 : 0, prompts);
        Assert.Equal(known is null && saved ? "SHA256:abc" : known, await store.FindAsync("jump.example", 22, "ssh-ed25519", Ct));
    }

    [Fact]
    public async Task Decide_without_a_prompt_rejects_an_unknown_key()
    {
        Assert.Equal(HostKeyVerdict.Rejected, await DecideAsync(new InMemoryKnownHostsStore(), Key(), null, Ct));
    }
}
