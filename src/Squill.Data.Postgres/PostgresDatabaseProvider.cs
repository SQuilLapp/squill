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
        };
        return builder.ConnectionString;
    }

    /// <inheritdoc />
    public async ValueTask<IDatabaseConnection> OpenAsync(ConnectionProfile profile, string? password, CancellationToken cancellationToken)
    {
        var dataSource = new NpgsqlSlimDataSourceBuilder(BuildConnectionString(profile, password)).Build();
        try
        {
            var connection = await dataSource.OpenConnectionAsync(cancellationToken);
            return new PostgresDatabaseConnection(dataSource, connection);
        }
        catch
        {
            await dataSource.DisposeAsync();
            throw;
        }
    }
}
