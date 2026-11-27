using System.Collections.Frozen;

namespace Squill.Sql.Keywords;

/// <summary>The ANSI reserved words plus the Oracle SQL and PL/SQL additions.</summary>
internal static class OracleKeywords
{
    /// <summary>Gets the case-insensitive set of reserved words.</summary>
    public static FrozenSet<string> Words { get; } = AnsiKeywords.Words.Concat(new[]
    {
        "ACCESS", "AUDIT", "BODY", "CLUSTER", "COMMENT", "COMPRESS", "CONNECT", "CONNECT_BY_ROOT", "DUAL", "ELSIF",
        "EXCEPTION", "EXCLUSIVE", "FUNCTION", "IDENTIFIED", "IF", "IMMEDIATE", "INCREMENT", "INITIAL", "LEVEL",
        "LOCK", "LONG", "LOOP", "MATCHED", "MAXEXTENTS", "MERGE", "MINUS", "MODE", "MODIFY", "NOCYCLE", "NOWAIT",
        "NUMBER", "OFFLINE", "ONLINE", "OPTION", "PACKAGE", "PCTFREE", "PRIOR", "PROCEDURE", "RAW", "RENAME",
        "REPLACE", "RESOURCE", "RETURN", "RETURNING", "ROWID", "ROWNUM", "SESSION", "SHARE", "SIZE", "START",
        "SYNONYM", "SYSDATE", "SYSTIMESTAMP", "UID", "VALIDATE", "VARCHAR2", "WHILE",
    }).ToFrozenSet(StringComparer.OrdinalIgnoreCase);
}
