using Microsoft.Data.SqlClient;
using Squill.Core;

namespace Squill.Data.SqlServer;

/// <summary>Opens SQL Server connections through Microsoft.Data.SqlClient.</summary>
public sealed class SqlServerDatabaseProvider : IDatabaseProvider
{
    /// <inheritdoc />
    public DatabaseDialect Dialect => DatabaseDialect.SqlServer;

    /// <summary>Builds the SqlClient connection string for a profile.</summary>
    /// <remarks>The server certificate is trusted without validation only when the host is <c>localhost</c>.</remarks>
    public static string BuildConnectionString(ConnectionProfile profile, string? password)
    {
        ArgumentNullException.ThrowIfNull(profile);
        if (string.IsNullOrEmpty(profile.Host))
            throw new ArgumentException("A SQL Server profile requires a host.", nameof(profile));

        var builder = new SqlConnectionStringBuilder
        {
            DataSource = $"{profile.Host},{profile.Port ?? 1433}",
            InitialCatalog = profile.Database ?? string.Empty,
            UserID = profile.UserName ?? string.Empty,
            Password = password ?? string.Empty,
            TrustServerCertificate = string.Equals(profile.Host, "localhost", StringComparison.OrdinalIgnoreCase),
        };
        return builder.ConnectionString;
    }

    /// <inheritdoc />
    public async ValueTask<IDatabaseConnection> OpenAsync(ConnectionProfile profile, string? password, CancellationToken cancellationToken)
    {
        var connection = new SqlConnection(BuildConnectionString(profile, password));
        try
        {
            await connection.OpenAsync(cancellationToken);
            return new SqlServerDatabaseConnection(connection);
        }
        catch
        {
            await connection.DisposeAsync();
            throw;
        }
    }
}
