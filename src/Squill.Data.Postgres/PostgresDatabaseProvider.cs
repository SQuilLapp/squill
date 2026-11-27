using Npgsql;
using Squill.Core;

namespace Squill.Data.Postgres;

/// <summary>Opens PostgreSQL connections through Npgsql's slim data source (no reflection-based plugins).</summary>
public sealed class PostgresDatabaseProvider : IDatabaseProvider
{
    /// <inheritdoc />
    public DatabaseDialect Dialect => DatabaseDialect.PostgreSql;

    /// <summary>Builds the Npgsql connection string for a profile.</summary>
    public static string BuildConnectionString(ConnectionProfile profile, string? password)
    {
        ArgumentNullException.ThrowIfNull(profile);
        if (string.IsNullOrEmpty(profile.Host))
            throw new ArgumentException("A PostgreSQL profile requires a host.", nameof(profile));

        var builder = new NpgsqlConnectionStringBuilder
        {
            Host = profile.Host,
            Port = profile.Port ?? 5432,
            Database = string.IsNullOrEmpty(profile.Database) ? "postgres" : profile.Database,
            Username = profile.UserName,
            Password = password,
            Timeout = profile.ConnectTimeoutSeconds,
            CommandTimeout = profile.CommandTimeoutSeconds,
        };
        if (profile.Tls != TlsMode.Default)
            builder.SslMode = profile.Tls switch
            {
                TlsMode.Disable => SslMode.Disable,
                TlsMode.Require => SslMode.Require,
                _ => SslMode.VerifyFull,
            };
        if (!string.IsNullOrEmpty(profile.TlsCaFile))
            builder.RootCertificate = profile.TlsCaFile;
        if (!string.IsNullOrEmpty(profile.TlsClientCertFile))
            builder.SslCertificate = profile.TlsClientCertFile;
        if (!string.IsNullOrEmpty(profile.TlsClientKeyFile))
            builder.SslKey = profile.TlsClientKeyFile;
        if (!string.IsNullOrEmpty(profile.TlsKeyPassphrase))
            builder.SslPassword = profile.TlsKeyPassphrase;
        return builder.ConnectionString;
    }

    // The slim builder keeps the AOT footprint small; arrays are needed by the schema queries and array columns, the other type groups so that every column type reads, TLS by the Tls profile option.
    private static NpgsqlDataSource BuildDataSource(ConnectionProfile profile, string connectionString)
    {
        var builder = new NpgsqlSlimDataSourceBuilder(connectionString);
        // The name checked against the certificate when it differs from the host (an SSH tunnel connects to 127.0.0.1).
        if (!string.IsNullOrEmpty(profile.TlsServerName))
            builder.UseSslClientAuthenticationOptionsCallback(options => options.TargetHost = profile.TlsServerName);
        return Configure(builder).Build();
    }

    private static NpgsqlSlimDataSourceBuilder Configure(NpgsqlSlimDataSourceBuilder builder) =>
        builder.EnableArrays().EnableNetworkTypes().EnableJsonTypes().EnableRanges().EnableMultiranges().EnableRecords().EnableGeometricTypes().EnableTransportSecurity();

    /// <inheritdoc />
    public async ValueTask<IDatabaseConnection> OpenAsync(ConnectionProfile profile, string? password, CancellationToken cancellationToken)
    {
        var dataSource = BuildDataSource(profile, BuildConnectionString(profile, password));
        try
        {
            var connection = await dataSource.OpenConnectionAsync(cancellationToken);
            return new PostgresDatabaseConnection(dataSource, connection, profile.CommandTimeoutSeconds);
        }
        catch
        {
            await dataSource.DisposeAsync();
            throw;
        }
    }
}
