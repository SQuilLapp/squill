namespace Squill.Data.Oracle;

/// <summary>Finds the output parameters of a DML statement with a <c>RETURNING ... INTO :name</c> clause.</summary>
internal static class OracleReturning
{
    private static readonly System.Buffers.SearchValues<char> NameChars = System.Buffers.SearchValues.Create("abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789_$#");

    /// <summary>Lists the bind variables after <c>INTO</c> of the last <c>RETURNING</c> clause that the caller did not supply a value for.</summary>
    /// <param name="sql">The statement text.</param>
    /// <param name="supplied">The parameters the caller binds as inputs.</param>
    /// <returns>The output parameters (read back as numbers: generated keys are identity columns), or <see langword="null"/> when there are none.</returns>
    public static WireOut[]? OutParameters(string sql, IReadOnlyList<QueryParameter> supplied)
    {
        var returning = sql.LastIndexOf("RETURNING", StringComparison.OrdinalIgnoreCase);
        var into = returning < 0 ? -1 : sql.IndexOf(" INTO ", returning, StringComparison.OrdinalIgnoreCase);
        if (into < 0)
            return null;
        var names = new List<WireOut>();
        foreach (var part in sql[(into + 6)..].TrimEnd().TrimEnd(';').Split(','))
        {
            var bind = part.Trim();
            if (bind.Length < 2 || bind[0] != ':' || bind.AsSpan(1).ContainsAnyExcept(NameChars))
                return null;
            var name = bind[1..];
            if (!supplied.Any(p => string.Equals(p.Name.TrimStart(':', '@'), name, StringComparison.OrdinalIgnoreCase)))
                names.Add(new WireOut(name, "d"));
        }
        return names.Count == 0 ? null : [.. names];
    }
}
