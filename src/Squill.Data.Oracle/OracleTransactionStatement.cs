namespace Squill.Data.Oracle;

/// <summary>
/// Recognises the transaction-control statements the app sends (<c>SET TRANSACTION READ WRITE</c>, <c>COMMIT</c>, <c>ROLLBACK</c>, <c>SAVEPOINT x</c>,
/// <c>ROLLBACK TO SAVEPOINT x</c>) so they run as bridge operations on the driver's transaction instead of as SQL.
/// </summary>
internal static class OracleTransactionStatement
{
    /// <summary>Maps a statement to its bridge operation.</summary>
    /// <param name="sql">The statement text.</param>
    /// <param name="op">The operation: <c>begin</c>, <c>commit</c>, <c>rollback</c> or <c>savepoint</c>.</param>
    /// <param name="name">The savepoint name for <c>savepoint</c> and a rollback to a savepoint; otherwise <c>null</c>.</param>
    /// <returns><c>true</c> when the statement is one of the recognised forms; anything else is plain SQL.</returns>
    public static bool TryParse(string sql, out string op, out string? name)
    {
        op = "";
        name = null;
        var words = sql.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (words.Length > 0 && words[^1].EndsWith(';'))
            words[^1] = words[^1].TrimEnd(';');
        words = [.. words.Where(w => w.Length > 0)];
        bool Is(int i, string word) => i < words.Length && words[i].Equals(word, StringComparison.OrdinalIgnoreCase);

        switch (words.Length)
        {
            case 1 when Is(0, "COMMIT"):
                op = "commit";
                return true;
            case 1 when Is(0, "ROLLBACK"):
                op = "rollback";
                return true;
            case 2 when Is(0, "COMMIT") && Is(1, "WORK"):
                op = "commit";
                return true;
            case 2 when Is(0, "ROLLBACK") && Is(1, "WORK"):
                op = "rollback";
                return true;
            case 2 when Is(0, "SAVEPOINT") && IsName(words[1]):
                (op, name) = ("savepoint", words[1]);
                return true;
            case 3 when Is(0, "ROLLBACK") && Is(1, "TO") && IsName(words[2]):
                (op, name) = ("rollback", words[2]);
                return true;
            case 4 when Is(0, "ROLLBACK") && Is(1, "TO") && Is(2, "SAVEPOINT") && IsName(words[3]):
                (op, name) = ("rollback", words[3]);
                return true;
            case 4 when Is(0, "SET") && Is(1, "TRANSACTION") && Is(2, "READ") && Is(3, "WRITE"):
                op = "begin";
                return true;
            default:
                return false;
        }
    }

    private static bool IsName(string word) =>
        word.Length > 0 && char.IsAsciiLetter(word[0]) && word.All(c => char.IsAsciiLetterOrDigit(c) || c is '_' or '$' or '#');
}
