using System.Collections.Frozen;

namespace Squill.Sql.Keywords;

/// <summary>The ANSI reserved words plus the T-SQL additions.</summary>
internal static class TSqlKeywords
{
    /// <summary>Gets the case-insensitive set of reserved words.</summary>
    public static FrozenSet<string> Words { get; } = AnsiKeywords.Words.Concat(new[]
    {
        "APPLY", "BACKUP", "BREAK", "BROWSE", "BULK", "CHECKPOINT", "CLUSTERED", "COMPUTE", "CONTAINS", "CONTINUE",
        "DBCC", "DENY", "DISK", "DUMP", "EXEC", "EXECUTE", "FILE", "FREETEXT", "GOTO", "HOLDLOCK", "IDENTITY",
        "IDENTITY_INSERT", "IDENTITYCOL", "KILL", "LINENO", "LOAD", "MERGE", "NOCHECK", "NOLOCK", "NONCLUSTERED",
        "OFFSETS", "OPENDATASOURCE", "OPENQUERY", "OPENROWSET", "OPENXML", "OUTPUT", "PERCENT", "PIVOT", "PLAN",
        "PRINT", "PROC", "PROCEDURE", "RAISERROR", "READTEXT", "RECONFIGURE", "REPLICATION", "RETURN", "REVERT",
        "ROWCOUNT", "ROWGUIDCOL", "RULE", "SAVE", "SETUSER", "SHUTDOWN", "STATISTICS", "TABLESAMPLE", "TEXTSIZE",
        "THROW", "TOP", "TRAN", "TRY_CONVERT", "TSEQUAL", "UNPIVOT", "UPDATETEXT", "USE", "WAITFOR", "WHILE",
        "WRITETEXT",
    }).ToFrozenSet(StringComparer.OrdinalIgnoreCase);
}
