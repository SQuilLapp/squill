using System.Text;

namespace Squill.Data.Informix;

/// <summary>Turns the <c>@name</c> parameters the app sends into the <c>?</c> markers Informix binds by position.</summary>
internal static class PositionalParameters
{
    /// <summary>Replaces each <c>@name</c> outside strings, quoted identifiers and comments (<c>--</c>, <c>/* */</c> and the Informix <c>{ }</c>) with <c>?</c>.</summary>
    /// <param name="sql">The statement text.</param>
    /// <returns>The rewritten text and the parameter names in marker order (a name used twice appears twice).</returns>
    public static (string Sql, IReadOnlyList<string> Names) Rewrite(string sql)
    {
        var names = new List<string>();
        var text = new StringBuilder(sql.Length);
        var i = 0;
        while (i < sql.Length)
        {
            var c = sql[i];
            var end = c switch
            {
                '\'' or '"' => Skip(sql, i, c),
                '{' => Skip(sql, i, '}'),
                '-' when At(sql, i + 1) == '-' => LineEnd(sql, i),
                '/' when At(sql, i + 1) == '*' => BlockEnd(sql, i),
                _ => i,
            };
            if (end > i)
            {
                text.Append(sql, i, end - i);
                i = end;
            }
            else if (c == '@' && (char.IsAsciiLetter(At(sql, i + 1)) || At(sql, i + 1) == '_'))
            {
                var j = i + 1;
                while (j < sql.Length && (char.IsAsciiLetterOrDigit(sql[j]) || sql[j] == '_'))
                    j++;
                names.Add(sql[(i + 1)..j]);
                text.Append('?');
                i = j;
            }
            else
            {
                text.Append(c);
                i++;
            }
        }

        return (text.ToString(), names);
    }

    private static char At(string sql, int i) => i < sql.Length ? sql[i] : '\0';

    private static int Skip(string sql, int start, char close)
    {
        var end = sql.IndexOf(close, start + 1);
        return end < 0 ? sql.Length : end + 1; // a doubled quote ('') simply closes and reopens
    }

    private static int LineEnd(string sql, int start)
    {
        var end = sql.IndexOf('\n', start);
        return end < 0 ? sql.Length : end;
    }

    private static int BlockEnd(string sql, int start)
    {
        var end = sql.IndexOf("*/", start + 2, StringComparison.Ordinal);
        return end < 0 ? sql.Length : end + 2;
    }
}
