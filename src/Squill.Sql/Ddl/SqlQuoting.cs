using Squill.Core;

namespace Squill.Sql.Ddl;

/// <summary>Quoting of identifiers and string literals for generated SQL.</summary>
public static class SqlQuoting
{
    /// <summary>Quotes an identifier, doubling the closing quote character inside it.</summary>
    /// <param name="identifier">The raw identifier.</param>
    /// <param name="d">The database engine.</param>
    /// <returns>The quoted identifier.</returns>
    public static string Quote(string identifier, DatabaseDialect d)
    {
        ArgumentNullException.ThrowIfNull(identifier);
        return d switch
        {
            DatabaseDialect.MySql => $"`{identifier.Replace("`", "``", StringComparison.Ordinal)}`",
            DatabaseDialect.SqlServer => $"[{identifier.Replace("]", "]]", StringComparison.Ordinal)}]",
            _ => $"\"{identifier.Replace("\"", "\"\"", StringComparison.Ordinal)}\"",
        };
    }

    /// <summary>Quotes a possibly schema-qualified object name.</summary>
    /// <param name="schema">The schema, omitted from the result when <see langword="null"/> or empty.</param>
    /// <param name="name">The object name.</param>
    /// <param name="d">The database engine.</param>
    /// <returns>The quoted name.</returns>
    public static string QualifiedName(string? schema, string name, DatabaseDialect d) =>
        string.IsNullOrEmpty(schema) ? Quote(name, d) : $"{Quote(schema, d)}.{Quote(name, d)}";

    /// <summary>Builds a single-quoted string literal, doubling embedded single quotes.</summary>
    /// <param name="value">The raw text.</param>
    /// <returns>The SQL literal.</returns>
    public static string Literal(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return $"'{value.Replace("'", "''", StringComparison.Ordinal)}'";
    }
}
