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
            Database = profile.Database,
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
        return builder.ConnectionString;
    }

    // The slim builder keeps the AOT footprint small; arrays are needed by the schema queries and array columns, TLS by the Tls profile option.
    private static NpgsqlDataSource BuildDataSource(string connectionString) =>
        new NpgsqlSlimDataSourceBuilder(connectionString).EnableArrays().EnableTransportSecurity().Build();

    /// <inheritdoc />
    public async ValueTask<IDatabaseConnection> OpenAsync(ConnectionProfile profile, string? password, CancellationToken cancellationToken)
    {
        var dataSource = BuildDataSource(BuildConnectionString(profile, password));
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
