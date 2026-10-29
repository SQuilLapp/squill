using Xunit;
using System.Runtime.Versioning;
using Squill.Platform.Secrets;

namespace Squill.Platform.Tests;

public sealed class LibSecretStoreTests
{
    public const string SkipReason = "Needs Linux, a Secret Service and SQUILL_SECRET_SERVICE_TESTS=1.";

    public static bool SecretServiceAvailable =>
        OperatingSystem.IsLinux() && Environment.GetEnvironmentVariable("SQUILL_SECRET_SERVICE_TESTS") == "1";

    [Fact]
    public void For_Key_ReturnsApplicationAndKeyAttributes()
    {
        var attributes = LibSecretAttributes.For("conn/1");

        Assert.Equal(2, attributes.Count);
        Assert.Equal("squill", attributes["application"]);
        Assert.Equal("conn/1", attributes["key"]);
    }

    [Fact(Skip = SkipReason, SkipUnless = nameof(SecretServiceAvailable), SkipType = typeof(LibSecretStoreTests))]
    [SupportedOSPlatform("linux")]
    public async Task RoundTrip_SetGetDelete_WorksAgainstSecretService()
    {
        var ct = TestContext.Current.CancellationToken;
        var store = new LibSecretStore();
        string key = "test-" + Guid.NewGuid().ToString("N");

        await store.SetAsync(key, "s3cret", ct);
        Assert.Equal("s3cret", await store.GetAsync(key, ct));

        await store.DeleteAsync(key, ct);
        await store.DeleteAsync(key, ct);
        Assert.Null(await store.GetAsync(key, ct));
    }
}
