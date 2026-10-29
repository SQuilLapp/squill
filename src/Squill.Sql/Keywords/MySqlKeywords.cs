using System.Collections.Frozen;

namespace Squill.Sql.Keywords;

/// <summary>The ANSI reserved words plus the MySQL/MariaDB additions.</summary>
internal static class MySqlKeywords
{
    /// <summary>Gets the case-insensitive set of reserved words.</summary>
    public static FrozenSet<string> Words { get; } = AnsiKeywords.Words.Concat(new[]
    {
        "ANALYZE", "AUTO_INCREMENT", "BINARY", "CHANGE", "DATABASES", "DELAYED", "DESCRIBE", "DISTINCTROW", "DIV",
        "DUAL", "EXPLAIN", "FORCE", "FULLTEXT", "HIGH_PRIORITY", "IGNORE", "ILIKE", "KEYS", "KILL", "LIMIT", "LOCK",
        "LOW_PRIORITY", "MOD", "OPTIMIZE", "REGEXP", "RENAME", "REPLACE", "RLIKE", "SHOW", "STRAIGHT_JOIN",
        "UNLOCK", "UNSIGNED", "USE", "ZEROFILL",
    }).ToFrozenSet(StringComparer.OrdinalIgnoreCase);
}
