namespace Squill.Sql.Lexing;

/// <summary>Splits SQL text into contiguous, lossless tokens and never throws on malformed input.</summary>
public interface ISqlTokenizer
{
    /// <summary>Tokenizes <paramref name="text"/>.</summary>
    /// <param name="text">The SQL text.</param>
    /// <returns>Tokens that cover the whole text, in order; no <see cref="TokenKind.EndOfFile"/> token is emitted.</returns>
    IReadOnlyList<Token> Tokenize(string text);
}
