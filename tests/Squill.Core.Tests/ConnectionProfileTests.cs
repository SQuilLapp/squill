using System.Text.Json;
using Squill.Core;
using Xunit;

namespace Squill.Core.Tests;

public sealed class ConnectionProfileTests
{
    private static readonly Guid Id = Guid.Parse("0123456789abcdef0123456789abcdef");

    private static ConnectionProfile Sample() => new(
        Id, "prod", DatabaseDialect.PostgreSql, "db.example.com", 5432, "app", "alice", null,
        new SshTunnelSettings("bastion", 22, "bob", SshAuthMethod.PrivateKey, "/keys/id_ed25519"));

    [Fact]
    public void Serialize_ProfileWithSsh_RoundTrips()
    {
        var json = JsonSerializer.Serialize(Sample(), ProfileJsonContext.Default.ConnectionProfile);
        var back = JsonSerializer.Deserialize(json, ProfileJsonContext.Default.ConnectionProfile);
        Assert.Equal(Sample(), back);
        Assert.Contains("\"dialect\": \"PostgreSql\"", json);
    }

    [Fact]
    public void Serialize_ProfileList_RoundTrips()
    {
        var list = new List<ConnectionProfile> { Sample() };
        var json = JsonSerializer.Serialize(list, ProfileJsonContext.Default.ListConnectionProfile);
        var back = JsonSerializer.Deserialize(json, ProfileJsonContext.Default.ListConnectionProfile);
        Assert.Equal(list, back);
    }

    [Fact]
    public void Serialize_Profile_ContainsNoPassword()
    {
        var json = JsonSerializer.Serialize(Sample(), ProfileJsonContext.Default.ConnectionProfile);
        Assert.DoesNotContain("password", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("passphrase", json, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void SecretKeys_Id_UsesNFormat()
    {
        Assert.Equal("profile:0123456789abcdef0123456789abcdef:password", SecretKeys.ProfilePassword(Id));
        Assert.Equal("profile:0123456789abcdef0123456789abcdef:ssh", SecretKeys.SshSecret(Id));
    }
}
