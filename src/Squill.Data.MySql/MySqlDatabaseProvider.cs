using MySqlConnector;
using Squill.Core;

namespace Squill.Data.MySql;

/// <summary>Opens MySQL and MariaDB connections through MySqlConnector.</summary>
public sealed class MySqlDatabaseProvider : IDatabaseProvider
{
    /// <inheritdoc />
    public DatabaseDialect Dialect => DatabaseDialect.MySql;

    /// <summary>Builds the MySqlConnector connection string for a profile.</summary>
    public static string BuildConnectionString(ConnectionProfile profile, string? password)
    {
        ArgumentNullException.ThrowIfNull(profile);
        if (string.IsNullOrEmpty(profile.Host))
            throw new ArgumentException("A MySQL profile requires a host.", nameof(profile));

        var builder = new MySqlConnectionStringBuilder
        {
            Server = profile.Host,
            Port = (uint)(profile.Port ?? 3306),
            Database = profile.Database ?? string.Empty,
            UserID = profile.UserName ?? string.Empty,
            Password = password ?? string.Empty,
            ConnectionTimeout = (uint)profile.ConnectTimeoutSeconds,
            DefaultCommandTimeout = (uint)profile.CommandTimeoutSeconds,
            ConvertZeroDateTime = true,
            AllowUserVariables = true, // SET @x = 1 and @x := 2 are ordinary MySQL; without it the driver reads @x as a missing parameter
        };
        if (profile.Tls != TlsMode.Default)
            builder.SslMode = profile.Tls switch
            {
                TlsMode.Disable => MySqlSslMode.None,
                TlsMode.Require => MySqlSslMode.Required,
                _ => MySqlSslMode.VerifyFull,
            };
        // shortcut: MySqlConnector has no server-name override and no encrypted-key support, so TlsServerName and the key passphrase are ignored; upgrade when the driver adds them.
        if (!string.IsNullOrEmpty(profile.TlsCaFile))
            builder.SslCa = profile.TlsCaFile;
        if (!string.IsNullOrEmpty(profile.TlsClientCertFile))
            builder.SslCert = profile.TlsClientCertFile;
        if (!string.IsNullOrEmpty(profile.TlsClientKeyFile))
            builder.SslKey = profile.TlsClientKeyFile;
        return builder.ConnectionString;
    }

    /// <inheritdoc />
    public async ValueTask<IDatabaseConnection> OpenAsync(ConnectionProfile profile, string? password, CancellationToken cancellationToken)
    {
        var connectionString = BuildConnectionString(profile, password);
        var connection = new MySqlConnection(connectionString);
        try
        {
            await connection.OpenAsync(cancellationToken);
            return new MySqlDatabaseConnection(connection, profile.CommandTimeoutSeconds, connectionString);
        }
        catch
        {
            await connection.DisposeAsync();
            throw;
        }
    }
}
