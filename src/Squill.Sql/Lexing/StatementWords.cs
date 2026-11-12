using Squill.Core;

namespace Squill.Sql.Lexing;

/// <summary>The significant tokens of the first statement of a text, shared by the token-based statement checks.</summary>
internal sealed class StatementWords
{
    private StatementWords(List<string> words, List<TokenKind> kinds, List<string> raw)
    {
        Words = words;
        Kinds = kinds;
        Raw = raw;
    }

    /// <summary>Gets the tokens, upper-cased for keywords and identifiers and as written otherwise.</summary>
    public List<string> Words { get; }

    /// <summary>Gets the kind of each token.</summary>
    public List<TokenKind> Kinds { get; }

    /// <summary>Gets the tokens exactly as written.</summary>
    public List<string> Raw { get; }

    /// <summary>Tokenizes <paramref name="statement"/> up to its first terminating semicolon, dropping whitespace and comments.</summary>
    /// <param name="statement">The text; only the first statement is read.</param>
    /// <param name="dialect">The engine, which selects the tokenizer.</param>
    /// <returns>The tokens of that statement.</returns>
    public static StatementWords Read(string statement, DatabaseDialect dialect)
    {
        var words = new List<string>();
        var kinds = new List<TokenKind>();
        var raw = new List<string>();
        foreach (var token in SqlTokenizers.For(dialect).Tokenize(statement))
        {
            if (token.Kind is TokenKind.Whitespace or TokenKind.LineComment or TokenKind.BlockComment)
            {
                continue;
            }

            var text = token.Text(statement);
            if (token.Kind == TokenKind.Punctuation && text == ";")
            {
                if (words.Count > 0)
                {
                    break;
                }

                continue;
            }

            kinds.Add(token.Kind);
            raw.Add(text);
            words.Add(token.Kind is TokenKind.Keyword or TokenKind.Identifier ? text.ToUpperInvariant() : text);
        }

        return new StatementWords(words, kinds, raw);
    }

    /// <summary>Whether the tokens contain a data write: <c>INTO</c>, <c>DELETE</c>, <c>MERGE</c>, <c>UPDATE</c> or <c>INSERT</c>.</summary>
    /// <returns><see langword="true"/> when one is found; <c>UPDATE</c> after <c>FOR</c> or <c>KEY</c> is a lock clause and <c>INSERT(</c> a function.</returns>
    public bool WritesData()
    {
        // INTO catches SELECT ... INTO (creates a table).
        for (var i = 0; i < Words.Count; i++)
        {
            if (Kinds[i] != TokenKind.Keyword)
            {
                continue;
            }

            switch (Words[i])
            {
                case "INTO" or "DELETE" or "MERGE":
                    return true;
                case "UPDATE" when i == 0 || Words[i - 1] is not ("FOR" or "KEY"):
                    return true;
                // MySQL INSERT(str, pos, len, new) is a string function.
                case "INSERT" when i + 1 >= Words.Count || Words[i + 1] != "(":
                    return true;
            }
        }

        return false;
    }
}
