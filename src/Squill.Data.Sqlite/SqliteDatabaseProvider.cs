using Microsoft.Data.Sqlite;
using Squill.Core;

namespace Squill.Data.Sqlite;

/// <summary>Opens SQLite database files (or <c>:memory:</c>); the password is ignored, encryption is not supported.</summary>
public sealed class SqliteDatabaseProvider : IDatabaseProvider
{
    /// <inheritdoc />
    public DatabaseDialect Dialect => DatabaseDialect.Sqlite;

    /// <inheritdoc />
    public async ValueTask<IDatabaseConnection> OpenAsync(ConnectionProfile profile, string? password, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(profile);
        if (string.IsNullOrEmpty(profile.FilePath))
            throw new ArgumentException("A SQLite profile requires a file path.", nameof(profile));

        var connection = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = profile.FilePath }.ToString());
        try
        {
            await connection.OpenAsync(cancellationToken);
        }
        catch
        {
            await connection.DisposeAsync();
            throw;
        }
        return new SqliteDatabaseConnection(connection);
    }
}
