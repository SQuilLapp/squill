using System.Collections.Frozen;

namespace Squill.Sql.Keywords;

/// <summary>The ANSI reserved words plus the SQLite additions.</summary>
internal static class SqliteKeywords
{
    /// <summary>Gets the case-insensitive set of reserved words.</summary>
    public static FrozenSet<string> Words { get; } = AnsiKeywords.Words.Concat(new[]
    {
        "ABORT", "ATTACH", "AUTOINCREMENT", "CONFLICT", "DATABASE", "DETACH", "EXPLAIN", "FAIL", "GLOB", "IGNORE",
        "ISNULL", "NOTHING", "NOTNULL", "PRAGMA", "REGEXP", "REINDEX", "RELEASE", "REPLACE", "RETURNING", "ROWID",
        "SAVEPOINT", "STRICT", "TEMP", "TEMPORARY", "VACUUM", "VIRTUAL", "WITHOUT",
    }).ToFrozenSet(StringComparer.OrdinalIgnoreCase);
}
