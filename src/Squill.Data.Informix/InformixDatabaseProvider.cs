using Squill.Core;

namespace Squill.Data.Informix;

/// <summary>
/// Opens Informix connections through IBM's <c>clidriver</c> over the DRDA listener, with an own ODBC layer (no IBM managed package, so it is Native AOT safe).
/// The driver folder comes from <c>SQUILL_CLIDRIVER</c> or the <c>clidriver</c> folder next to the executable; <c>PATH</c> and the registry are never used.
/// </summary>
public sealed class InformixDatabaseProvider : IDatabaseProvider
{
    /// <summary>The DRDA port of the Informix developer image (<c>drsoctcp</c>); the classic <c>onsoctcp</c> listener on 9088 is not reachable by this driver.</summary>
    public const int DefaultPort = 9089;

    /// <summary>The database used when the profile has none.</summary>
    public const string DefaultDatabase = "sysmaster";

    /// <inheritdoc />
    public DatabaseDialect Dialect => DatabaseDialect.Informix;

    /// <inheritdoc />
    public async ValueTask<IDatabaseConnection> OpenAsync(ConnectionProfile profile, string? password, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(profile);
        if (string.IsNullOrEmpty(profile.Host))
            throw new ArgumentException("An Informix profile requires a host.", nameof(profile));
        if (string.IsNullOrEmpty(profile.UserName))
            throw new ArgumentException("An Informix profile requires a user.", nameof(profile));

        var database = string.IsNullOrEmpty(profile.Database) ? DefaultDatabase : profile.Database;
        var connectionString = OdbcConnectionString.Build(profile.Host, profile.Port ?? DefaultPort, database, profile.UserName, password ?? "");
        return await InformixDatabaseConnection.OpenAsync(connectionString, cancellationToken);
    }
}
