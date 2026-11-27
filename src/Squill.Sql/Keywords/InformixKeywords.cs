using System.Collections.Frozen;

namespace Squill.Sql.Keywords;

/// <summary>The ANSI reserved words plus the Informix SQL and SPL additions, including its data type names.</summary>
internal static class InformixKeywords
{
    /// <summary>Gets the case-insensitive set of reserved words.</summary>
    public static FrozenSet<string> Words { get; } = AnsiKeywords.Words.Concat(new[]
    {
        "BIGSERIAL", "BLOB", "BYTE", "CLOB", "DATETIME", "DBA", "EXTEND", "FIRST", "FOREACH", "FUNCTION", "INT8", "LVARCHAR",
        "MATCHES", "MDY", "MONEY", "PROCEDURE", "RETURNING", "ROUTINE", "SERIAL", "SERIAL8", "SKIP", "TEXT", "TODAY",
    }).ToFrozenSet(StringComparer.OrdinalIgnoreCase);
}
