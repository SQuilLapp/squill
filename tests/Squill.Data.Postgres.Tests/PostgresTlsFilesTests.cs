using Npgsql;
using Squill.Core;
using Squill.Data;
using Xunit;

namespace Squill.Data.Postgres.Tests;

public sealed class PostgresTlsFilesTests
{
    private static readonly ConnectionProfile Profile =
        new(Guid.NewGuid(), "pg", DatabaseDialect.PostgreSql, "db.local", null, "app", "me", null, null);

    [Fact]
    public void BuildConnectionString_TlsFiles_AreMapped()
    {
        var profile = Profile with { TlsCaFile = "ca.pem", TlsClientCertFile = "c.crt", TlsClientKeyFile = "c.key", TlsKeyPassphrase = "pp" };
        var builder = new NpgsqlConnectionStringBuilder(PostgresDatabaseProvider.BuildConnectionString(profile, null));
        Assert.Equal("ca.pem", builder.RootCertificate);
        Assert.Equal("c.crt", builder.SslCertificate);
        Assert.Equal("c.key", builder.SslKey);
        Assert.Equal("pp", builder.SslPassword);
    }

    [Fact]
    public void BuildConnectionString_NoTlsFiles_LeavesThemUnset()
    {
        var builder = new NpgsqlConnectionStringBuilder(PostgresDatabaseProvider.BuildConnectionString(Profile, null));
        Assert.Null(builder.RootCertificate);
        Assert.Null(builder.SslCertificate);
        Assert.Null(builder.SslKey);
    }
}

public sealed class PostgresTlsConnectionTests(PostgresTlsFixture server) : IClassFixture<PostgresTlsFixture>
{
    private ConnectionProfile Profile(bool ca = true, bool client = true, bool encrypted = false, string? serverName = PostgresTlsFixture.ServerName)
    {
        var cert = encrypted ? server.EncryptedClient : server.Client;
        return new(Guid.NewGuid(), "pg", DatabaseDialect.PostgreSql, server.Host, server.Port, PostgresFixture.Database, PostgresFixture.UserName, null, null,
            Tls: TlsMode.VerifyFull,
            TlsCaFile: ca ? server.Pki!.CaFile : null,
            TlsClientCertFile: client ? cert.CertFile : null,
            TlsClientKeyFile: client ? cert.KeyFile : null,
            TlsServerName: serverName)
        {
            TlsKeyPassphrase = encrypted ? PostgresTlsFixture.KeyPassphrase : null,
        };
    }

    private static ValueTask<IDatabaseConnection> OpenAsync(ConnectionProfile profile) =>
        new PostgresDatabaseProvider().OpenAsync(profile, PostgresFixture.Password, CancellationToken.None);

    private static async Task<object?> ClientDnAsync(IDatabaseConnection connection)
    {
        await using var reader = await connection.ExecuteAsync("select client_dn from pg_stat_ssl where pid = pg_backend_pid()", [], CancellationToken.None);
        return Assert.Single(await reader.ReadAllAsync(10, CancellationToken.None))[0];
    }

    [Fact(Skip = DockerGate.SkipReason, SkipUnless = nameof(DockerGate.Enabled), SkipType = typeof(DockerGate))]
    public async Task OpenAsync_CaAndClientCertificate_Connects()
    {
        await using var connection = await OpenAsync(Profile());
        Assert.Contains("CN=postgres", (string)(await ClientDnAsync(connection))!);
    }

    [Fact(Skip = DockerGate.SkipReason, SkipUnless = nameof(DockerGate.Enabled), SkipType = typeof(DockerGate))]
    public async Task OpenAsync_EncryptedClientKeyWithPassphrase_Connects()
    {
        await using var connection = await OpenAsync(Profile(encrypted: true));
        Assert.Contains("CN=postgres", (string)(await ClientDnAsync(connection))!);
    }

    [Fact(Skip = DockerGate.SkipReason, SkipUnless = nameof(DockerGate.Enabled), SkipType = typeof(DockerGate))]
    public async Task OpenAsync_WithoutClientCertificate_IsRefused() =>
        await Assert.ThrowsAnyAsync<Exception>(async () => await OpenAsync(Profile(client: false)));

    [Fact(Skip = DockerGate.SkipReason, SkipUnless = nameof(DockerGate.Enabled), SkipType = typeof(DockerGate))]
    public async Task OpenAsync_WithoutCaFile_RejectsTheServerCertificate() =>
        await Assert.ThrowsAnyAsync<Exception>(async () => await OpenAsync(Profile(ca: false)));

    [Fact(Skip = DockerGate.SkipReason, SkipUnless = nameof(DockerGate.Enabled), SkipType = typeof(DockerGate))]
    public async Task OpenAsync_WithoutServerName_FailsTheHostNameCheck() =>
        await Assert.ThrowsAnyAsync<Exception>(async () => await OpenAsync(Profile(serverName: null)));
}
