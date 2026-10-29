using System.Runtime.Versioning;
using Xunit;
using Squill.Platform.Secrets;

namespace Squill.Platform.Tests;

public sealed class SecretStoreFactoryTests
{
    [Fact]
    public void Create_OnCurrentOs_ReturnsAStore()
    {
        Assert.NotNull(SecretStoreFactory.Create(Path.GetTempPath()));
    }

    [Fact(Skip = DpapiSecretStoreTests.SkipReason, SkipUnless = nameof(DpapiSecretStoreTests.IsWindows), SkipType = typeof(DpapiSecretStoreTests))]
    [SupportedOSPlatform("windows")]
    public void Create_OnWindows_ReturnsDpapiStore()
    {
        Assert.IsType<DpapiSecretStore>(SecretStoreFactory.Create(Path.GetTempPath()));
    }
}
