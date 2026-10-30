using Squill.Core;

namespace Squill.Presentation.Editor;

/// <summary>Builds small SQL texts for the editor, with identifiers quoted for the target engine.</summary>
public static class SqlSnippets
{
    /// <summary>Builds a query that reads the first rows of a table or view.</summary>
    /// <param name="t">The table or view.</param>
    /// <param name="d">The database engine, which decides quoting and the row-limit syntax.</param>
    /// <param name="n">The maximum number of rows.</param>
    /// <returns>The SQL text.</returns>
    public static string SelectTop(TableInfo t, DatabaseDialect d, int n = 100)
    {
        ArgumentNullException.ThrowIfNull(t);
        var name = t.Schema is { Length: > 0 } schema ? $"{Quote(schema, d)}.{Quote(t.Name, d)}" : Quote(t.Name, d);
        return d == DatabaseDialect.SqlServer
            ? $"SELECT TOP {n} * FROM {name}"
            : $"SELECT * FROM {name} LIMIT {n}";
    }

    /// <summary>Quotes an identifier, doubling the closing quote character inside it.</summary>
    /// <param name="identifier">The raw identifier.</param>
    /// <param name="d">The database engine.</param>
    /// <returns>The quoted identifier.</returns>
    public static string Quote(string identifier, DatabaseDialect d) => d switch
    {
        DatabaseDialect.MySql => $"`{identifier.Replace("`", "``", StringComparison.Ordinal)}`",
        DatabaseDialect.SqlServer => $"[{identifier.Replace("]", "]]", StringComparison.Ordinal)}]",
        _ => $"\"{identifier.Replace("\"", "\"\"", StringComparison.Ordinal)}\"",
    };
}
