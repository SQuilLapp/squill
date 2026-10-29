using Xunit;
using Squill.Core;
using Squill.Platform.Ssh;

namespace Squill.Platform.Tests;

public sealed class SshNetTunnelFactoryTests
{
    private static readonly SshTunnelSettings Valid = new("localhost", 22, "u", SshAuthMethod.Password, null);

    public static TheoryData<SshTunnelSettings, string, int> InvalidArguments => new()
    {
        { Valid with { Host = " " }, "db", 5432 },
        { Valid with { Port = 0 }, "db", 5432 },
        { Valid with { Port = 65536 }, "db", 5432 },
        { Valid with { AuthMethod = SshAuthMethod.PrivateKey, PrivateKeyPath = null }, "db", 5432 },
        { Valid, "", 5432 },
        { Valid, "db", 0 },
        { Valid, "db", 70000 },
    };

    [Theory]
    [MemberData(nameof(InvalidArguments))]
    public async Task OpenAsync_rejects_invalid_arguments_before_connecting(SshTunnelSettings settings, string host, int port)
    {
        await Assert.ThrowsAsync<ArgumentException>(async () =>
            await new SshNetTunnelFactory().OpenAsync(settings, null, host, port, TestContext.Current.CancellationToken));
    }
}
