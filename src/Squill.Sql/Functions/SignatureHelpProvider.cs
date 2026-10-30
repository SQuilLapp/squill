using Squill.Sql.Completion;
using Squill.Sql.Lexing;

namespace Squill.Sql.Functions;

/// <summary>Finds the catalog function whose argument list holds the caret.</summary>
public static class SignatureHelpProvider
{
    /// <summary>Gets the signature help at the caret.</summary>
    /// <param name="request">The script, caret and dialect.</param>
    /// <returns>The innermost enclosing call of a known function, or <see langword="null"/>.</returns>
    public static SignatureHelp? GetSignatureHelp(SqlRequest request)
    {
        var offset = Math.Clamp(request.Offset, 0, request.Text.Length);
        var before = request.Text[..offset];
        var catalog = FunctionCatalog.For(request.Dialect);

        // one frame per unclosed "(": the word before it (null when none) and its top-level comma count
        var frames = new List<(string? Name, int Commas)>();
        string? last = null;
        foreach (var token in SqlTokenizers.For(request.Dialect).Tokenize(before))
        {
            if (token.Kind is TokenKind.Whitespace or TokenKind.LineComment or TokenKind.BlockComment)
            {
                continue;
            }

            var text = token.Text(before);
            if (token.Kind == TokenKind.Punctuation && text == "(")
            {
                frames.Add((last, 0));
            }
            else if (token.Kind == TokenKind.Punctuation && text == ")")
            {
                if (frames.Count > 0)
                {
                    frames.RemoveAt(frames.Count - 1);
                }
            }
            else if (token.Kind == TokenKind.Punctuation && text == "," && frames.Count > 0)
            {
                frames[^1] = (frames[^1].Name, frames[^1].Commas + 1);
            }

            last = token.Kind is TokenKind.Identifier or TokenKind.Keyword ? text : null;
        }

        for (var i = frames.Count - 1; i >= 0; i--)
        {
            var (name, commas) = frames[i];
            var signature = name is null ? null : catalog.FirstOrDefault(f => f.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
            if (signature is not null)
            {
                return new SignatureHelp(signature, commas);
            }
        }

        return null;
    }
}
