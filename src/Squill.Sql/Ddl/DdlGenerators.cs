using Squill.Core;

namespace Squill.Sql.Ddl;

/// <summary>Access to the DDL generator of each engine.</summary>
public static class DdlGenerators
{
    private static readonly PostgresDdlGenerator Postgres = new();
    private static readonly MySqlDdlGenerator MySql = new();
    private static readonly SqlServerDdlGenerator SqlServer = new();
    private static readonly SqliteDdlGenerator Sqlite = new();
    private static readonly OracleDdlGenerator Oracle = new();
    private static readonly InformixDdlGenerator Informix = new();

    /// <summary>Returns the generator for an engine.</summary>
    /// <param name="d">The database engine.</param>
    /// <returns>The generator.</returns>
    public static IDdlGenerator For(DatabaseDialect d) => d switch
    {
        DatabaseDialect.PostgreSql => Postgres,
        DatabaseDialect.MySql => MySql,
        DatabaseDialect.SqlServer => SqlServer,
        DatabaseDialect.Sqlite => Sqlite,
        DatabaseDialect.Oracle => Oracle,
        DatabaseDialect.Informix => Informix,
        _ => throw new ArgumentOutOfRangeException(nameof(d)),
    };

    /// <summary>Tells whether the engine can roll back DDL statements inside a transaction.</summary>
    /// <param name="d">The database engine.</param>
    /// <returns><see langword="false"/> for MySQL and Oracle, which commit implicitly on DDL.</returns>
    public static bool UsesTransactionalDdl(DatabaseDialect d) => d is not (DatabaseDialect.MySql or DatabaseDialect.Oracle);

    /// <summary>Tells whether the table designer and the <c>CREATE TABLE</c> script work for the engine.</summary>
    /// <param name="d">The database engine.</param>
    /// <returns><see langword="false"/> for Informix, whose generator only drops tables so far.</returns>
    public static bool CanDesign(DatabaseDialect d) => d != DatabaseDialect.Informix;

    /// <summary>Joins statements into one script for display and copy.</summary>
    /// <param name="statements">The statements, without trailing semicolons.</param>
    /// <returns>Statements separated by a semicolon and a blank line, the last one terminated by a semicolon.</returns>
    public static string ToScript(IEnumerable<string> statements)
    {
        ArgumentNullException.ThrowIfNull(statements);
        return string.Concat(statements.Select((s, i) => (i == 0 ? "" : "\n\n") + s + ";"));
    }
}
