using MySqlConnector;
using Squill.Core;
using Squill.Data;
using Xunit;

namespace Squill.Data.MySql.Tests;

public sealed class MySqlTlsFilesTests
{
    [Fact]
    public void BuildConnectionString_TlsFiles_AreMapped()
    {
        var profile = new ConnectionProfile(Guid.NewGuid(), "my", DatabaseDialect.MySql, "db.local", null, "app", "me", null, null,
            TlsCaFile: "ca.pem", TlsClientCertFile: "c.crt", TlsClientKeyFile: "c.key");
        var builder = new MySqlConnectionStringBuilder(MySqlDatabaseProvider.BuildConnectionString(profile, null));
        Assert.Equal("ca.pem", builder.SslCa);
        Assert.Equal("c.crt", builder.SslCert);
        Assert.Equal("c.key", builder.SslKey);
    }
}

public sealed class MySqlTlsConnectionTests(MySqlTlsFixture server) : IClassFixture<MySqlTlsFixture>
{
    private ConnectionProfile Profile(bool ca = true, bool client = true) =>
        new(Guid.NewGuid(), "my", DatabaseDialect.MySql, server.Host, server.Port, MySqlFixture.Database, MySqlTlsFixture.X509User, null, null,
            Tls: TlsMode.VerifyFull,
            TlsCaFile: ca ? server.Pki!.CaFile : null,
            TlsClientCertFile: client ? server.Client.CertFile : null,
            TlsClientKeyFile: client ? server.Client.KeyFile : null);

    private static ValueTask<IDatabaseConnection> OpenAsync(ConnectionProfile profile) =>
        new MySqlDatabaseProvider().OpenAsync(profile, MySqlTlsFixture.X509Password, CancellationToken.None);

    [Fact(Skip = DockerGate.SkipReason, SkipUnless = nameof(DockerGate.Enabled), SkipType = typeof(DockerGate))]
    public async Task OpenAsync_CaAndClientCertificate_Connects()
    {
        await using var connection = await OpenAsync(Profile());
        await using var reader = await connection.ExecuteAsync("show session status like 'Ssl_cipher'", [], CancellationToken.None);
        Assert.False(string.IsNullOrEmpty((string?)Assert.Single(await reader.ReadAllAsync(10, CancellationToken.None))[1]));
    }

    [Fact(Skip = DockerGate.SkipReason, SkipUnless = nameof(DockerGate.Enabled), SkipType = typeof(DockerGate))]
    public async Task OpenAsync_WithoutClientCertificate_IsRefused() =>
        await Assert.ThrowsAnyAsync<Exception>(async () => await OpenAsync(Profile(client: false)));

    [Fact(Skip = DockerGate.SkipReason, SkipUnless = nameof(DockerGate.Enabled), SkipType = typeof(DockerGate))]
    public async Task OpenAsync_WithoutCaFile_RejectsTheServerCertificate() =>
        await Assert.ThrowsAnyAsync<Exception>(async () => await OpenAsync(Profile(ca: false)));
}
