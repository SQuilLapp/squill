namespace Squill.Core;

/// <summary>Supported database engines (MariaDB is <see cref="MySql"/>).</summary>
public enum DatabaseDialect
{
    /// <summary>PostgreSQL.</summary>
    PostgreSql,
    /// <summary>MySQL and MariaDB.</summary>
    MySql,
    /// <summary>Microsoft SQL Server.</summary>
    SqlServer,
    /// <summary>SQLite.</summary>
    Sqlite,
    /// <summary>Oracle Database, reached through the out-of-process bridge (proof of concept).</summary>
    Oracle,
    /// <summary>IBM Informix, reached through IBM's <c>clidriver</c> over ODBC and the DRDA listener.</summary>
    Informix,
}
