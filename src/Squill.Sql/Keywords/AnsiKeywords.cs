using System.Collections.Frozen;

namespace Squill.Sql.Keywords;

/// <summary>The reserved words of the SQL-92/99 core.</summary>
internal static class AnsiKeywords
{
    /// <summary>Gets the case-insensitive set of reserved words.</summary>
    public static FrozenSet<string> Words { get; } = new[]
    {
        "ADD", "ALL", "ALTER", "AND", "ANY", "AS", "ASC", "AVG", "BEGIN", "BETWEEN", "BIGINT", "BOOLEAN", "BOTH", "BY",
        "CASCADE", "CASE", "CAST", "CHAR", "CHECK", "COLLATE", "COLUMN", "COMMIT", "CONSTRAINT", "COUNT", "CREATE",
        "CROSS", "CURRENT", "CURRENT_DATE", "CURRENT_TIME", "CURRENT_TIMESTAMP", "CURSOR", "DATE", "DAY", "DECIMAL",
        "DECLARE", "DEFAULT", "DELETE", "DESC", "DISTINCT", "DOUBLE", "DROP", "ELSE", "END", "ESCAPE", "EXCEPT",
        "EXEC", "EXECUTE", "EXISTS", "EXTRACT", "FALSE", "FETCH", "FILTER", "FLOAT", "FOLLOWING", "FOR", "FOREIGN",
        "FROM", "FULL", "GRANT", "GROUP", "HAVING", "HOUR", "IN", "INDEX", "INNER", "INSERT", "INT", "INTEGER",
        "INTERSECT", "INTERVAL", "INTO", "IS", "JOIN", "KEY", "LEADING", "LEFT", "LIKE", "LIMIT", "LOCAL", "MAX",
        "MIN", "MINUTE", "MONTH", "NATURAL", "NEXT", "NO", "NOT", "NULL", "NUMERIC", "OFFSET", "ON", "ONLY", "OR",
        "ORDER", "OUTER", "OVER", "PARTITION", "PRECEDING", "PRECISION", "PRIMARY", "RANGE", "REAL", "RECURSIVE",
        "REFERENCES", "RESTRICT", "REVOKE", "RIGHT", "ROLLBACK", "ROW", "ROWS", "SECOND", "SELECT", "SET",
        "SMALLINT", "SOME", "SUBSTRING", "SUM", "TABLE", "THEN", "TIME", "TIMESTAMP", "TO", "TOP", "TRAILING",
        "TRIGGER", "TRIM", "TRUE", "UNBOUNDED", "UNION", "UNIQUE", "UPDATE", "USING", "VALUES", "VARCHAR", "VIEW",
        "WHEN", "WHERE", "WINDOW", "WITH", "YEAR", "ZONE",
    }.ToFrozenSet(StringComparer.OrdinalIgnoreCase);
}
