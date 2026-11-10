using System.Text;

namespace Squill.Sql.Completion;

/// <summary>Helpers for LoomEditor snippet templates (<c>$0</c>, <c>${1:default}</c>).</summary>
public static class SnippetText
{
    /// <summary>Escapes literal text so a snippet template inserts it as typed.</summary>
    /// <param name="literal">The literal text.</param>
    /// <returns>The text with <c>\</c>, <c>$</c> and <c>}</c> escaped.</returns>
    public static string Escape(string literal)
    {
        var builder = new StringBuilder(literal.Length + 4);
        foreach (var c in literal)
        {
            if (c is '\\' or '$' or '}')
            {
                builder.Append('\\');
            }

            builder.Append(c);
        }

        return builder.ToString();
    }

    /// <summary>Turns a snippet template into the plain text a non-snippet editor inserts.</summary>
    /// <param name="template">The template.</param>
    /// <returns>The text without tab stops, placeholders replaced by their default and escapes removed.</returns>
    public static string ToPlain(string template)
    {
        var builder = new StringBuilder(template.Length);
        var i = 0;
        while (i < template.Length)
        {
            var c = template[i];
            if (c == '\\' && i + 1 < template.Length && template[i + 1] is '\\' or '$' or '}')
            {
                builder.Append(template[i + 1]);
                i += 2;
            }
            else if (c == '$' && i + 1 < template.Length && char.IsAsciiDigit(template[i + 1]))
            {
                i++;
                while (i < template.Length && char.IsAsciiDigit(template[i]))
                {
                    i++;
                }
            }
            else if (c == '$' && i + 2 < template.Length && template[i + 1] == '{' && char.IsAsciiDigit(template[i + 2]))
            {
                i += 2;
                while (i < template.Length && char.IsAsciiDigit(template[i]))
                {
                    i++;
                }

                if (i < template.Length && template[i] == ':')
                {
                    i++;
                }

                // Default text runs to the closing brace; escaped characters inside are unescaped.
                while (i < template.Length && template[i] != '}')
                {
                    if (template[i] == '\\' && i + 1 < template.Length)
                    {
                        i++;
                    }

                    builder.Append(template[i]);
                    i++;
                }

                i++;
            }
            else
            {
                builder.Append(c);
                i++;
            }
        }

        return builder.ToString();
    }
}
