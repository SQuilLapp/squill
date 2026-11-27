using Squill.Tests.Pki;
using Testcontainers.PostgreSql;
using Xunit;

namespace Squill.Data.Postgres.Tests;

/// <summary>Starts a PostgreSQL container whose certificates come from a throw-away CA and that demands a client certificate (<c>clientcert=verify-full</c>) for every TCP login.</summary>
public sealed class PostgresTlsFixture : IAsyncLifetime
{
    /// <summary>The only name in the server certificate: the host of the container is never it.</summary>
    public const string ServerName = "db.squill.test";
    public const string KeyPassphrase = "squill-key-pass";

    private PostgreSqlContainer? _container;

    public TestPki? Pki { get; private set; }

    public (string CertPem, string KeyPem, string CertFile, string KeyFile) Client { get; private set; }

    public (string CertPem, string KeyPem, string CertFile, string KeyFile) EncryptedClient { get; private set; }

    public string Host => _container!.Hostname;

    public int Port => _container!.GetMappedPublicPort(5432);

    public async ValueTask InitializeAsync()
    {
        if (!DockerGate.Enabled)
            return;
        Pki = new TestPki();
        var server = Pki.Issue("server", false, [ServerName], loopback: false);
        // clientcert=verify-full: the certificate common name must be the database user.
        Client = Pki.Issue(PostgresFixture.UserName, true, []);
        EncryptedClient = Pki.Issue(PostgresFixture.UserName, true, [], KeyPassphrase, "postgres-encrypted");
        _container = new PostgreSqlBuilder("postgres:17-alpine")
            .WithPassword(PostgresFixture.Password)
            .WithEnvironment("SQUILL_CERT", server.CertPem)
            .WithEnvironment("SQUILL_KEY", server.KeyPem)
            .WithEnvironment("SQUILL_CA", Pki.CaPem)
            .WithEntrypoint("sh", "-c", "printf '%s\n' \"$SQUILL_CERT\" > /tmp/server.crt && printf '%s\n' \"$SQUILL_KEY\" > /tmp/server.key && printf '%s\n' \"$SQUILL_CA\" > /tmp/ca.crt "
                + "&& printf 'local all all trust\nhostssl all all all scram-sha-256 clientcert=verify-full\n' > /tmp/hba.conf "
                + "&& chown postgres /tmp/server.crt /tmp/server.key /tmp/ca.crt /tmp/hba.conf && chmod 600 /tmp/server.key "
                + "&& exec docker-entrypoint.sh postgres -c ssl=on -c ssl_cert_file=/tmp/server.crt -c ssl_key_file=/tmp/server.key -c ssl_ca_file=/tmp/ca.crt -c hba_file=/tmp/hba.conf")
            .Build();
        await _container.StartAsync();
    }

    public async ValueTask DisposeAsync()
    {
        if (_container is not null)
            await _container.DisposeAsync();
        Pki?.Dispose();
    }
}
