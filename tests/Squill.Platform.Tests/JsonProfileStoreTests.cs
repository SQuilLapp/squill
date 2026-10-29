using Squill.Core;
using Squill.Platform.Profiles;
using Squill.Platform.Secrets;
using Xunit;

namespace Squill.Platform.Tests;

public sealed class JsonProfileStoreTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "squill-tests-" + Guid.NewGuid().ToString("N"));
    private readonly InMemorySecretStore _secrets = new();
    private readonly CancellationToken _ct = TestContext.Current.CancellationToken;

    public void Dispose()
    {
        if (Directory.Exists(_dir))
        {
            Directory.Delete(_dir, recursive: true);
        }
    }

    private JsonProfileStore NewStore() => new(new AppPaths(_dir), _secrets);

    private static ConnectionProfile Profile(string name = "p") =>
        new(Guid.NewGuid(), name, DatabaseDialect.PostgreSql, "localhost", 5432, "db", "user", null, null);

    [Fact]
    public async Task SaveAsync_ThenList_RoundTripsProfileAndSecrets()
    {
        var profile = Profile();
        await NewStore().SaveAsync(profile, "pw", "ssh", _ct);

        var store = NewStore();
        Assert.Equal([profile], await store.ListAsync(_ct));
        Assert.Equal("pw", await store.GetPasswordAsync(profile.Id, _ct));
        Assert.Equal("ssh", await store.GetSshSecretAsync(profile.Id, _ct));
    }

    [Fact]
    public async Task SaveAsync_ExistingProfile_ReplacesItAndNullKeepsSecretEmptyDeletesIt()
    {
        var store = NewStore();
        var profile = Profile("old");
        await store.SaveAsync(profile, "pw", "ssh", _ct);
        await store.SaveAsync(profile with { Name = "new" }, null, "", _ct);

        Assert.Equal("new", Assert.Single(await store.ListAsync(_ct)).Name);
        Assert.Equal("pw", await store.GetPasswordAsync(profile.Id, _ct));
        Assert.Null(await store.GetSshSecretAsync(profile.Id, _ct));
    }

    [Fact]
    public async Task DeleteAsync_RemovesProfileAndBothSecrets()
    {
        var store = NewStore();
        var profile = Profile();
        await store.SaveAsync(profile, "pw", "ssh", _ct);
        await store.DeleteAsync(profile.Id, _ct);

        Assert.Empty(await store.ListAsync(_ct));
        Assert.Null(await store.GetPasswordAsync(profile.Id, _ct));
        Assert.Null(await store.GetSshSecretAsync(profile.Id, _ct));
    }

    [Fact]
    public async Task ListAsync_CorruptFile_RenamesItAndReturnsEmpty()
    {
        Directory.CreateDirectory(_dir);
        await File.WriteAllTextAsync(Path.Combine(_dir, "profiles.json"), "{ not json", _ct);

        Assert.Empty(await NewStore().ListAsync(_ct));
        Assert.True(File.Exists(Path.Combine(_dir, "profiles.json.bad")));
        Assert.False(File.Exists(Path.Combine(_dir, "profiles.json")));
    }

    [Fact]
    public async Task SaveAsync_WithPassword_DoesNotWritePasswordToFile()
    {
        await NewStore().SaveAsync(Profile(), "s3cret-pw", "s3cret-ssh", _ct);

        var text = await File.ReadAllTextAsync(Path.Combine(_dir, "profiles.json"), _ct);
        Assert.DoesNotContain("s3cret", text);
        Assert.False(File.Exists(Path.Combine(_dir, "profiles.json.tmp")));
    }

    [Fact]
    public void Combine_CreatesDirectory()
    {
        var path = new AppPaths(_dir).Combine("sub", "f.json");

        Assert.Equal(Path.Combine(_dir, "sub", "f.json"), path);
        Assert.True(Directory.Exists(Path.Combine(_dir, "sub")));
    }
}
