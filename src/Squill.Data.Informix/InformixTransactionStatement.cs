namespace Squill.Data.Informix;

/// <summary>Recognises <c>BEGIN [WORK]</c>, <c>COMMIT [WORK]</c> and <c>ROLLBACK [WORK]</c>, which run on the ODBC connection (autocommit and <c>SQLEndTran</c>) instead of as SQL.</summary>
internal static class InformixTransactionStatement
{
    /// <summary>The begin command.</summary>
    public const int Begin = 1;

    /// <summary>The commit command.</summary>
    public const int Commit = 2;

    /// <summary>The rollback command.</summary>
    public const int Rollback = 3;

    /// <summary>Maps a statement to a command.</summary>
    /// <param name="sql">The statement text.</param>
    /// <returns><see cref="Begin"/>, <see cref="Commit"/> or <see cref="Rollback"/>; <see langword="null"/> for any other statement (savepoints included, which run as SQL inside the transaction).</returns>
    public static int? TryParse(string sql)
    {
        var words = sql.Trim().TrimEnd(';').Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        if (words.Length is 0 or > 2 || (words.Length == 2 && !words[1].Equals("WORK", StringComparison.OrdinalIgnoreCase)))
            return null;
        return words[0].ToUpperInvariant() switch { "BEGIN" => Begin, "COMMIT" => Commit, "ROLLBACK" => Rollback, _ => null };
    }
}
