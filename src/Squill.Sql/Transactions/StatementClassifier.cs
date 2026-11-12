using Squill.Core;
using Squill.Sql.Lexing;

namespace Squill.Sql.Transactions;

/// <summary>Token-based classification of a statement (no parser).</summary>
public static class StatementClassifier
{
    private static readonly HashSet<string> MySqlImplicitCommitHeads =
    [
        "CREATE", "ALTER", "DROP", "RENAME", "TRUNCATE", "LOCK", "UNLOCK", "ANALYZE", "OPTIMIZE", "REPAIR", "CHECK", "FLUSH", "GRANT", "REVOKE",
    ];

    /// <summary>Classifies one statement.</summary>
    /// <param name="statement">One statement; if several are present only the first is classified.</param>
    /// <param name="dialect">The engine, which selects the tokenizer and the dialect-specific rules.</param>
    /// <returns>The traits of the statement.</returns>
    public static StatementTraits Classify(string statement, DatabaseDialect dialect)
    {
        ArgumentNullException.ThrowIfNull(statement);
        var scan = StatementWords.Read(statement, dialect);
        var w = scan.Words;
        var first = 0;
        while (first < w.Count && w[first] == "(")
        {
            first++;
        }

        if (first >= w.Count)
        {
            return new StatementTraits(StatementKind.Empty, true, false, false, false, false, TransactionControl.None, false, false, false, null);
        }

        var depth = Depths(w, first);
        var m = w[first] == "WITH" ? MainStatement(w, depth, first) : first;
        var verb = w[m];
        var control = TransactionControl.None;
        var kind = verb switch
        {
            "SELECT" or "VALUES" or "TABLE" or "WITH" => StatementKind.Select,
            "INSERT" => StatementKind.Insert,
            "REPLACE" when dialect is DatabaseDialect.MySql or DatabaseDialect.Sqlite => StatementKind.Insert,
            "UPDATE" => StatementKind.Update,
            "DELETE" => StatementKind.Delete,
            "MERGE" => StatementKind.Merge,
            "TRUNCATE" => StatementKind.Truncate,
            "CREATE" => StatementKind.Create,
            "ALTER" or "RENAME" or "GRANT" or "REVOKE" => StatementKind.Alter,
            "COMMENT" when At(w, m + 1) == "ON" => StatementKind.Alter,
            "DROP" => StatementKind.Drop,
            "CALL" or "EXEC" or "EXECUTE" or "DO" => StatementKind.Call,
            "SET" or "PRAGMA" or "USE" or "SHOW" or "DESCRIBE" or "DESC" => StatementKind.Set,
            "BEGIN" or "START" or "COMMIT" or "END" or "ROLLBACK" or "ABORT" or "SAVEPOINT" or "SAVE" or "RELEASE" => StatementKind.Transaction,
            _ => StatementKind.Other,
        };

        if (kind == StatementKind.Transaction)
        {
            control = ControlOf(w, m, dialect);
            if (control == TransactionControl.None)
            {
                kind = StatementKind.Other;
            }
        }

        var writes = kind == StatementKind.Select && scan.WritesData();
        var modifies = kind is StatementKind.Insert or StatementKind.Update or StatementKind.Delete or StatementKind.Merge or StatementKind.Truncate || writes;
        var schema = kind is StatementKind.Create or StatementKind.Alter or StatementKind.Drop or StatementKind.Truncate;
        var readOnly = kind is StatementKind.Set or StatementKind.Empty
            || (kind == StatementKind.Select && !writes)
            || control == TransactionControl.Rollback;
        var noWhere = kind is StatementKind.Update or StatementKind.Delete && !HasTopLevel(w, depth, m + 1, "WHERE");
        var destructive = noWhere
            || kind is StatementKind.Truncate or StatementKind.Drop
            || (kind == StatementKind.Alter && HasTopLevel(w, depth, m + 1, "DROP"));
        var autocommitWord = kind == StatementKind.Set && w.Any(x => x.Contains("AUTOCOMMIT", StringComparison.OrdinalIgnoreCase));
        var setsTransactionBehavior = kind == StatementKind.Set
            && w.Any(x => x.Contains("AUTOCOMMIT", StringComparison.OrdinalIgnoreCase)
                || x.Contains("IMPLICIT_TRANSACTIONS", StringComparison.OrdinalIgnoreCase)
                || x.Contains("XACT_ABORT", StringComparison.OrdinalIgnoreCase));
        var implicitCommit = dialect == DatabaseDialect.MySql
            && (control == TransactionControl.Begin
                || (MySqlImplicitCommitHeads.Contains(verb) && !(verb is "CREATE" or "DROP" && At(w, m + 1) == "TEMPORARY"))
                || (autocommitWord && w.Contains("1")));
        var mayChange = kind is StatementKind.Call or StatementKind.Other || verb == "PRAGMA" || setsTransactionBehavior;

        return new StatementTraits(
            kind,
            readOnly,
            modifies,
            schema,
            destructive,
            noWhere,
            control,
            implicitCommit,
            CannotRun(w, depth, m, kind, dialect),
            mayChange,
            TargetOf(scan, m, kind));
    }

    private static string? At(List<string> w, int i) => i < w.Count ? w[i] : null;

    private static int[] Depths(List<string> w, int first)
    {
        var depth = new int[w.Count];
        var d = 0;
        for (var i = first; i < w.Count; i++)
        {
            if (w[i] == ")")
            {
                d = Math.Max(0, d - 1);
            }

            depth[i] = d;
            if (w[i] == "(")
            {
                d++;
            }
        }

        return depth;
    }

    private static int MainStatement(List<string> w, int[] depth, int first)
    {
        for (var i = first + 1; i < w.Count; i++)
        {
            if (depth[i] == 0 && w[i] is "SELECT" or "INSERT" or "UPDATE" or "DELETE" or "MERGE" or "VALUES")
            {
                return i;
            }
        }

        return first;
    }

    private static bool HasTopLevel(List<string> w, int[] depth, int from, string word)
    {
        for (var i = from; i < w.Count; i++)
        {
            if (depth[i] == 0 && w[i] == word)
            {
                return true;
            }
        }

        return false;
    }

    private static TransactionControl ControlOf(List<string> w, int m, DatabaseDialect dialect)
    {
        var n1 = At(w, m + 1);
        switch (w[m])
        {
            case "BEGIN":
                if (dialect == DatabaseDialect.Oracle || (dialect == DatabaseDialect.SqlServer && n1 is not (null or "TRAN" or "TRANSACTION" or "DISTRIBUTED")))
                {
                    return TransactionControl.None;
                }

                return TransactionControl.Begin;
            case "START":
                return n1 == "TRANSACTION" ? TransactionControl.Begin : TransactionControl.None;
            case "COMMIT":
                return TransactionControl.Commit;
            case "END":
                return dialect is DatabaseDialect.PostgreSql or DatabaseDialect.Sqlite && n1 is null or "TRANSACTION" or "WORK" or "TRAN"
                    ? TransactionControl.Commit
                    : TransactionControl.None;
            case "ROLLBACK" or "ABORT":
                var i = m + 1;
                var named = false;
                if (At(w, i) is "TRAN" or "TRANSACTION" or "WORK")
                {
                    named = At(w, i) is not "WORK";
                    i++;
                }

                if (At(w, i) == "TO" || (dialect == DatabaseDialect.SqlServer && named && At(w, i) is not null))
                {
                    return TransactionControl.RollbackToSavepoint;
                }

                return TransactionControl.Rollback;
            case "SAVEPOINT":
                return n1 is null ? TransactionControl.None : TransactionControl.Savepoint;
            case "SAVE":
                return dialect == DatabaseDialect.SqlServer && n1 is "TRAN" or "TRANSACTION" && At(w, m + 2) is not null
                    ? TransactionControl.Savepoint
                    : TransactionControl.None;
            case "RELEASE":
                return n1 is null ? TransactionControl.None : TransactionControl.ReleaseSavepoint;
            default:
                return TransactionControl.None;
        }
    }

    private static bool CannotRun(List<string> w, int[] depth, int m, StatementKind kind, DatabaseDialect dialect)
    {
        var verb = w[m];
        var n1 = At(w, m + 1);
        var createDropAlter = verb is "CREATE" or "DROP" or "ALTER";
        switch (dialect)
        {
            case DatabaseDialect.PostgreSql:
                return verb == "VACUUM"
                    || (verb is "CREATE" or "DROP" && n1 is "DATABASE" or "TABLESPACE")
                    || (verb == "ALTER" && n1 == "SYSTEM")
                    || (HasTopLevel(w, depth, m + 1, "CONCURRENTLY") && (verb == "REINDEX" || (verb is "CREATE" or "DROP" && w.Contains("INDEX"))));
            case DatabaseDialect.SqlServer:
                return (createDropAlter && n1 == "DATABASE") || verb is "BACKUP" or "RESTORE";
            case DatabaseDialect.Sqlite:
                return verb is "VACUUM" or "ATTACH" or "DETACH";
            default:
                return false;
        }
    }

    private static string? TargetOf(StatementWords scan, int m, StatementKind kind)
    {
        var w = scan.Words;
        var i = m + 1;
        switch (kind)
        {
            case StatementKind.Insert or StatementKind.Update or StatementKind.Delete or StatementKind.Truncate:
                while (i < w.Count)
                {
                    if (w[i] == "OR")
                    {
                        i += 2;
                    }
                    else if (w[i] == "TOP" && At(w, i + 1) == "(")
                    {
                        while (i < w.Count && w[i] != ")")
                        {
                            i++;
                        }

                        i++;
                    }
                    else if (w[i] is "INTO" or "FROM" or "ONLY" or "TABLE" or "IGNORE" or "LOW_PRIORITY" or "HIGH_PRIORITY" or "DELAYED" or "QUICK")
                    {
                        i++;
                    }
                    else
                    {
                        break;
                    }
                }

                return ReadName(scan, i);
            case StatementKind.Drop when At(w, i) == "TABLE":
                i++;
                if (At(w, i) == "IF" && At(w, i + 1) == "EXISTS")
                {
                    i += 2;
                }

                return ReadName(scan, i);
            default:
                return null;
        }
    }

    private static string? ReadName(StatementWords scan, int i)
    {
        var parts = new List<string>();
        while (i < scan.Raw.Count && scan.Kinds[i] is TokenKind.Identifier or TokenKind.Keyword or TokenKind.QuotedIdentifier)
        {
            var text = scan.Raw[i];
            if (scan.Kinds[i] == TokenKind.QuotedIdentifier && text.Length >= 2)
            {
                text = text[1..^1];
            }

            parts.Add(text);
            if (i + 1 < scan.Raw.Count && scan.Words[i + 1] == ".")
            {
                i += 2;
                continue;
            }

            break;
        }

        return parts.Count == 0 ? null : string.Join('.', parts);
    }
}
