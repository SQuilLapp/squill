using Xunit;
using System.Runtime.Versioning;
using System.Text;
using Squill.Platform.Secrets;

namespace Squill.Platform.Tests;

public sealed class DpapiSecretStoreTests
{
    public const string SkipReason = "DPAPI is only available on Windows.";

    public static bool IsWindows => OperatingSystem.IsWindows();

    [Fact(Skip = SkipReason, SkipUnless = nameof(IsWindows), SkipType = typeof(DpapiSecretStoreTests))]
    [SupportedOSPlatform("windows")]
    public async Task RoundTrip_StoresEncryptedFile_AndReadsSecretBack()
    {
        string dir = Path.Combine(Path.GetTempPath(), "squill-dpapi-" + Guid.NewGuid().ToString("N"));
        var ct = TestContext.Current.CancellationToken;
        try
        {
            var store = new DpapiSecretStore(dir);
            Assert.Null(await store.GetAsync("k", ct));

            await store.SetAsync("k", "p4ssw0rd-secret", ct);

            Assert.Equal("p4ssw0rd-secret", await store.GetAsync("k", ct));
            string file = Assert.Single(Directory.GetFiles(dir, "*.bin"));
            Assert.Equal(64 + ".bin".Length, Path.GetFileName(file).Length);
            Assert.DoesNotContain("p4ssw0rd", Encoding.UTF8.GetString(await File.ReadAllBytesAsync(file, ct)));

            await store.DeleteAsync("k", ct);
            await store.DeleteAsync("k", ct);
            Assert.Null(await store.GetAsync("k", ct));
        }
        finally
        {
            if (Directory.Exists(dir))
            {
                Directory.Delete(dir, true);
            }
        }
    }
}
