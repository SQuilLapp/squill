using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Testcontainers.PostgreSql;
using Xunit;

namespace Squill.Data.Postgres.Tests;

/// <summary>Starts one throw-away PostgreSQL container for a test class.</summary>
public sealed class PostgresFixture : IAsyncLifetime
{
    public const string Database = "postgres";
    public const string UserName = "postgres";
    public const string Password = "squill-test";

    private PostgreSqlContainer? _container;

    public string Host => _container!.Hostname;

    public int Port => _container!.GetMappedPublicPort(5432);

    public async ValueTask InitializeAsync()
    {
        if (!DockerGate.Enabled)
            return;
        // The stock image has no TLS; write a throw-away self-signed certificate (from the environment) and start the server with ssl=on.
        using var key = RSA.Create(2048);
        var request = new CertificateRequest("CN=squill-test", key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        using var certificate = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(30));
        _container = new PostgreSqlBuilder("postgres:17-alpine")
            .WithPassword(Password)
            .WithEnvironment("SQUILL_CERT", certificate.ExportCertificatePem())
            .WithEnvironment("SQUILL_KEY", key.ExportPkcs8PrivateKeyPem())
            .WithEntrypoint("sh", "-c", "printf '%s\n' \"$SQUILL_CERT\" > /tmp/server.crt && printf '%s\n' \"$SQUILL_KEY\" > /tmp/server.key && chown postgres /tmp/server.crt /tmp/server.key && chmod 600 /tmp/server.key && exec docker-entrypoint.sh postgres -c ssl=on -c ssl_cert_file=/tmp/server.crt -c ssl_key_file=/tmp/server.key")
            .Build();
        await _container.StartAsync();
    }

    public async ValueTask DisposeAsync()
    {
        if (_container is not null)
            await _container.DisposeAsync();
    }
}
