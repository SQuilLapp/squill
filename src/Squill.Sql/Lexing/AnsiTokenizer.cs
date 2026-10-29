using System.Collections.Frozen;
using Squill.Sql.Keywords;

namespace Squill.Sql.Lexing;

/// <summary>Tokenizer for the ANSI SQL core shared by every dialect.</summary>
public sealed class AnsiTokenizer : SqlTokenizerBase
{
    /// <inheritdoc />
    protected override FrozenSet<string> Keywords => AnsiKeywords.Words;
}
