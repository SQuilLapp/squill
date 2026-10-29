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
}
