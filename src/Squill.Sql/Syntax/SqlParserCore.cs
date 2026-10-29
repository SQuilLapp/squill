using Squill.Core;
using Squill.Sql.Lexing;

namespace Squill.Sql.Syntax;

/// <summary>A forward cursor over the significant tokens of a text, with the diagnostics collected so far.</summary>
internal sealed class SqlParserCore
{
    private readonly List<Token> _tokens = [];
    private int _position;

    /// <summary>Tokenizes <paramref name="text"/> and drops whitespace and comments.</summary>
    /// <param name="text">The SQL text.</param>
    /// <param name="dialect">The dialect that selects the tokenizer.</param>
    public SqlParserCore(string text, DatabaseDialect dialect)
    {
        Text = text;
        foreach (var token in SqlTokenizers.For(dialect).Tokenize(text))
        {
            if (token.Kind is not (TokenKind.Whitespace or TokenKind.LineComment or TokenKind.BlockComment))
            {
                _tokens.Add(token);
            }
        }
    }

    /// <summary>Gets the parsed text.</summary>
    public string Text { get; }

    /// <summary>Gets the problems reported so far.</summary>
    public List<ParseDiagnostic> Diagnostics { get; } = [];

    /// <summary>Gets the offset just after the last consumed token (zero before any).</summary>
    public int LastEnd { get; private set; }

    /// <summary>Gets a value indicating whether every token has been consumed.</summary>
    public bool AtEnd => _position >= _tokens.Count;

    /// <summary>Looks at a token without consuming it.</summary>
    /// <param name="ahead">How many tokens past the current one.</param>
    /// <returns>The token, or a zero-length <see cref="TokenKind.EndOfFile"/> token past the end.</returns>
    public Token Peek(int ahead = 0) =>
        _position + ahead < _tokens.Count ? _tokens[_position + ahead] : new Token(TokenKind.EndOfFile, Text.Length, 0);

    /// <summary>Consumes the current token.</summary>
    /// <returns>The consumed token, or the end-of-file token when nothing is left.</returns>
    public Token Next()
    {
        var token = Peek();
        if (!AtEnd)
        {
            _position++;
            LastEnd = token.End;
        }

        return token;
    }

    /// <summary>Gets the source text of a token.</summary>
    /// <param name="token">A token of this text.</param>
    /// <returns>The characters it covers.</returns>
    public string TextOf(Token token) => token.Text(Text);

    /// <summary>Tests whether a keyword or identifier token spells <paramref name="word"/>, ignoring case.</summary>
    /// <param name="token">The token to test.</param>
    /// <param name="word">The word to compare with.</param>
    /// <returns><see langword="true"/> when they match.</returns>
    public bool IsWord(Token token, string word) =>
        token.Kind is TokenKind.Keyword or TokenKind.Identifier
        && Text.AsSpan(token.Start, token.Length).Equals(word, StringComparison.OrdinalIgnoreCase);

    /// <summary>Tests whether a token is the punctuation character <paramref name="c"/>.</summary>
    /// <param name="token">The token to test.</param>
    /// <param name="c">One of <c>( ) , ; .</c>.</param>
    /// <returns><see langword="true"/> when it is that character.</returns>
    public bool IsPunct(Token token, char c) =>
        token.Kind == TokenKind.Punctuation && token.Length == 1 && Text[token.Start] == c;

    /// <summary>Tests whether a token is the operator <paramref name="op"/>.</summary>
    /// <param name="token">The token to test.</param>
    /// <param name="op">The operator text.</param>
    /// <returns><see langword="true"/> when it is that operator.</returns>
    public bool IsOperator(Token token, string op) =>
        token.Kind == TokenKind.Operator && Text.AsSpan(token.Start, token.Length).SequenceEqual(op);

    /// <summary>Consumes the current token when it spells <paramref name="word"/>.</summary>
    /// <param name="word">The keyword to match, ignoring case.</param>
    /// <returns><see langword="true"/> when a token was consumed.</returns>
    public bool Match(string word)
    {
        if (!IsWord(Peek(), word))
        {
            return false;
        }

        Next();
        return true;
    }

    /// <summary>Consumes the current token when it is the punctuation character <paramref name="c"/>.</summary>
    /// <param name="c">The character to match.</param>
    /// <returns><see langword="true"/> when a token was consumed.</returns>
    public bool MatchPunct(char c)
    {
        if (!IsPunct(Peek(), c))
        {
            return false;
        }

        Next();
        return true;
    }

    /// <summary>Consumes <paramref name="c"/> or reports that it is missing.</summary>
    /// <param name="c">The expected punctuation character.</param>
    /// <returns><see langword="true"/> when it was present.</returns>
    public bool Expect(char c)
    {
        if (MatchPunct(c))
        {
            return true;
        }

        ReportMissing($"Expected '{c}'");
        return false;
    }

    /// <summary>Records a diagnostic.</summary>
    /// <param name="start">Offset of the first character concerned.</param>
    /// <param name="length">Number of characters concerned.</param>
    /// <param name="message">The description.</param>
    public void Report(int start, int length, string message) => Diagnostics.Add(new ParseDiagnostic(start, length, message));

    /// <summary>Records a zero-length diagnostic where the last consumed token ends.</summary>
    /// <param name="message">The description.</param>
    public void ReportMissing(string message) => Report(LastEnd, 0, message);
}
