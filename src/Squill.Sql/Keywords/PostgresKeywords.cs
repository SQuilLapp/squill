using System.Collections.Frozen;

namespace Squill.Sql.Keywords;

/// <summary>The ANSI reserved words plus the PostgreSQL additions.</summary>
internal static class PostgresKeywords
{
    /// <summary>Gets the case-insensitive set of reserved words.</summary>
    public static FrozenSet<string> Words { get; } = AnsiKeywords.Words.Concat(new[]
    {
        "ANALYZE", "ARRAY", "CONFLICT", "DO", "FREEZE", "GROUPS", "ILIKE", "ISNULL", "LATERAL", "MATERIALIZED",
        "NOTHING", "NOTNULL", "OVERLAPS", "RETURNING", "SIMILAR", "TABLESAMPLE", "VARIADIC", "VERBOSE", "WITHIN",
    }).ToFrozenSet(StringComparer.OrdinalIgnoreCase);
}
