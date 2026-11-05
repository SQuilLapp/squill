using Microsoft.Data.SqlClient;
using Squill.Core;

namespace Squill.Data.SqlServer;

/// <summary>Opens SQL Server connections through Microsoft.Data.SqlClient.</summary>
public sealed class SqlServerDatabaseProvider : IDatabaseProvider
{
    /// <inheritdoc />
    public DatabaseDialect Dialect => DatabaseDialect.SqlServer;

    /// <summary>Builds the SqlClient connection string for a profile.</summary>
    /// <remarks>The server certificate is trusted without validation when the profile asks for it, for <see cref="TlsMode.Require"/>, or when the host is <c>localhost</c>, <c>127.0.0.1</c> or <c>::1</c>; never for <see cref="TlsMode.VerifyFull"/>.</remarks>
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
            ConnectTimeout = profile.ConnectTimeoutSeconds,
            CommandTimeout = profile.CommandTimeoutSeconds,
            TrustServerCertificate = profile.Tls != TlsMode.VerifyFull && (profile.TrustServerCertificate || profile.Tls == TlsMode.Require || IsLoopback(profile.Host)),
        };
        if (profile.Tls != TlsMode.Default)
            builder.Encrypt = profile.Tls == TlsMode.Disable ? SqlConnectionEncryptOption.Optional : SqlConnectionEncryptOption.Mandatory;
        return builder.ConnectionString;
    }

    // Containers and tunnels reach the server on a numeric loopback address; it is the same machine as localhost.
    private static bool IsLoopback(string host) =>
        string.Equals(host, "localhost", StringComparison.OrdinalIgnoreCase) || host is "127.0.0.1" or "::1";

    /// <inheritdoc />
    public async ValueTask<IDatabaseConnection> OpenAsync(ConnectionProfile profile, string? password, CancellationToken cancellationToken)
    {
        var connection = new SqlConnection(BuildConnectionString(profile, password));
        try
        {
            await connection.OpenAsync(cancellationToken);
            return new SqlServerDatabaseConnection(connection, profile.CommandTimeoutSeconds);
        }
        catch (SqlException ex) when (ex.InnerException is not null)
        {
            await connection.DisposeAsync();
            // On Linux SqlClient reports a failed TLS handshake as a generic pre-login error and keeps the certificate reason in the inner exceptions; surface it.
            var reasons = new List<string>();
            for (var inner = ex.InnerException; inner is not null; inner = inner.InnerException)
                reasons.Add(inner.Message);
            throw new InvalidOperationException(ex.Message + " " + string.Join(' ', reasons), ex);
        }
        catch
        {
            await connection.DisposeAsync();
            throw;
        }
    }
}
