using MySqlConnector;
using Squill.Tests.Pki;
using Testcontainers.MySql;
using Xunit;

namespace Squill.Data.MySql.Tests;

/// <summary>Starts a MySQL container with certificates from a throw-away CA and an account that must present a client certificate (<c>REQUIRE X509</c>).</summary>
public sealed class MySqlTlsFixture : IAsyncLifetime
{
    public const string X509User = "squill_x509";
    public const string X509Password = "squill-x509-pass";

    private MySqlContainer? _container;

    public TestPki? Pki { get; private set; }

    public (string CertPem, string KeyPem, string CertFile, string KeyFile) Client { get; private set; }

    public string Host => _container!.Hostname;

    public int Port => _container!.GetMappedPublicPort(3306);

    public async ValueTask InitializeAsync()
    {
        if (!DockerGate.Enabled)
            return;
        Pki = new TestPki();
        var server = Pki.Issue("server", false, ["localhost"]);
        Client = Pki.Issue("squill-client", true, []);
        _container = new MySqlBuilder("mysql:8.4").WithDatabase(MySqlFixture.Database).WithPassword(MySqlFixture.Password)
            .WithEnvironment("SQUILL_CERT", server.CertPem)
            .WithEnvironment("SQUILL_KEY", server.KeyPem)
            .WithEnvironment("SQUILL_CA", Pki.CaPem)
            .WithEntrypoint("sh", "-c", "printf '%s\n' \"$SQUILL_CERT\" > /tmp/server.crt && printf '%s\n' \"$SQUILL_KEY\" > /tmp/server.key && printf '%s\n' \"$SQUILL_CA\" > /tmp/ca.crt "
                + "&& chown mysql /tmp/server.crt /tmp/server.key /tmp/ca.crt && chmod 600 /tmp/server.key "
                + "&& exec docker-entrypoint.sh mysqld --ssl-ca=/tmp/ca.crt --ssl-cert=/tmp/server.crt --ssl-key=/tmp/server.key")
            .Build();
        await _container.StartAsync();

        var builder = new MySqlConnectionStringBuilder
        {
            Server = Host, Port = (uint)Port, UserID = MySqlFixture.UserName, Password = MySqlFixture.Password, SslMode = MySqlSslMode.Required,
        };
        await using var connection = new MySqlConnection(builder.ConnectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = $"create user '{X509User}'@'%' identified by '{X509Password}' require x509; grant all on *.* to '{X509User}'@'%'";
        await command.ExecuteNonQueryAsync();
    }

    public async ValueTask DisposeAsync()
    {
        if (_container is not null)
            await _container.DisposeAsync();
        Pki?.Dispose();
    }
}
