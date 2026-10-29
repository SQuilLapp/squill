using Squill.Core;

namespace Squill.Sql.Lexing;

/// <summary>Factory for the tokenizer of a database dialect.</summary>
public static class SqlTokenizers
{
    private static readonly AnsiTokenizer Ansi = new();

    /// <summary>Gets the tokenizer for a dialect.</summary>
    /// <param name="dialect">The database engine.</param>
    /// <returns>A shared, stateless tokenizer (the ANSI one until dialect tokenizers exist).</returns>
    public static ISqlTokenizer For(DatabaseDialect dialect) => Ansi;
}
