using System.Text;
using Squill.Core;
using Squill.Sql.Lexing;

namespace Squill.Presentation.Query;

/// <summary>Finds the named parameters of a statement and rewrites them to the <c>@name</c> form every provider binds.</summary>
public static class ParameterScanner
{
    /// <summary>Lists the distinct named parameters of a statement.</summary>
    /// <param name="sql">The statement text.</param>
    /// <param name="dialect">Dialect used to tokenize, so strings and comments are ignored.</param>
    /// <returns>Names without sigil, in first-appearance order.</returns>
    public static IReadOnlyList<string> Scan(string sql, DatabaseDialect dialect)
    {
        ArgumentNullException.ThrowIfNull(sql);
        List<string> names = [];
        foreach (var (_, name) in Named(sql, dialect))
        {
            if (!names.Contains(name))
                names.Add(name);
        }

        return names;
    }

    /// <summary>Tells whether a statement uses positional parameters (<c>?</c>, <c>$1</c>), which are not supported.</summary>
    /// <param name="sql">The statement text.</param>
    /// <param name="dialect">Dialect used to tokenize.</param>
    /// <returns><see langword="true"/> when a positional parameter token exists.</returns>
    public static bool HasPositional(string sql, DatabaseDialect dialect)
    {
        ArgumentNullException.ThrowIfNull(sql);
        foreach (var token in SqlTokenizers.For(dialect).Tokenize(sql))
        {
            if (token.Kind == TokenKind.Parameter && NameOf(token.Text(sql)) is null)
                return true;
        }

        return false;
    }

    /// <summary>Rewrites every <c>:name</c> and <c>$name</c> parameter to <c>@name</c>.</summary>
    /// <param name="sql">The statement text.</param>
    /// <param name="dialect">Dialect used to tokenize.</param>
    /// <returns>The text to send to the provider.</returns>
    public static string Rewrite(string sql, DatabaseDialect dialect)
    {
        ArgumentNullException.ThrowIfNull(sql);
        var builder = new StringBuilder(sql.Length);
        var last = 0;
        foreach (var (token, name) in Named(sql, dialect))
        {
            builder.Append(sql, last, token.Start - last).Append('@').Append(name);
            last = token.End;
        }

        return builder.Append(sql, last, sql.Length - last).ToString();
    }

    private static IEnumerable<(Token Token, string Name)> Named(string sql, DatabaseDialect dialect)
    {
        foreach (var token in SqlTokenizers.For(dialect).Tokenize(sql))
        {
            if (token.Kind == TokenKind.Parameter && NameOf(token.Text(sql)) is { } name)
                yield return (token, name);
        }
    }

    private static string? NameOf(string text) =>
        text.Length > 1 && text[0] is ':' or '@' or '$' && !char.IsAsciiDigit(text[1]) ? text[1..] : null;
}
