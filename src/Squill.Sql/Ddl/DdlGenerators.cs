using Squill.Core;

namespace Squill.Sql.Ddl;

/// <summary>Access to the DDL generator of each engine.</summary>
public static class DdlGenerators
{
    private static readonly PostgresDdlGenerator Postgres = new();
    private static readonly MySqlDdlGenerator MySql = new();
    private static readonly SqlServerDdlGenerator SqlServer = new();
    private static readonly SqliteDdlGenerator Sqlite = new();

    /// <summary>Returns the generator for an engine.</summary>
    /// <param name="d">The database engine.</param>
    /// <returns>The generator.</returns>
    public static IDdlGenerator For(DatabaseDialect d) => d switch
    {
        DatabaseDialect.PostgreSql => Postgres,
        DatabaseDialect.MySql => MySql,
        DatabaseDialect.SqlServer => SqlServer,
        DatabaseDialect.Sqlite => Sqlite,
        _ => throw new ArgumentOutOfRangeException(nameof(d)),
    };

    /// <summary>Tells whether the engine can roll back DDL statements inside a transaction.</summary>
    /// <param name="d">The database engine.</param>
    /// <returns><see langword="false"/> for MySQL, which commits implicitly on DDL.</returns>
    public static bool UsesTransactionalDdl(DatabaseDialect d) => d != DatabaseDialect.MySql;

    /// <summary>Joins statements into one script for display and copy.</summary>
    /// <param name="statements">The statements, without trailing semicolons.</param>
    /// <returns>Statements separated by a semicolon and a blank line, the last one terminated by a semicolon.</returns>
    public static string ToScript(IEnumerable<string> statements)
    {
        ArgumentNullException.ThrowIfNull(statements);
        return string.Concat(statements.Select((s, i) => (i == 0 ? "" : "\n\n") + s + ";"));
    }
}
