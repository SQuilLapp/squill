using Squill.Core;

namespace Squill.Sql.Completion;

/// <summary>Quotes identifiers that are not plain names.</summary>
internal static class IdentifierQuoting
{
    /// <summary>Gets the text to insert for <paramref name="name"/>: unchanged when it matches <c>[a-z_][a-z0-9_]*</c> once lowercased, quoted for the dialect otherwise.</summary>
    /// <param name="name">The identifier.</param>
    /// <param name="dialect">The dialect that decides the quote characters.</param>
    /// <returns>The text to insert.</returns>
    public static string Quote(string name, DatabaseDialect dialect)
    {
        if (IsPlain(name))
        {
            return name;
        }

        return dialect switch
        {
            DatabaseDialect.MySql => "`" + name.Replace("`", "``", StringComparison.Ordinal) + "`",
            DatabaseDialect.SqlServer => "[" + name.Replace("]", "]]", StringComparison.Ordinal) + "]",
            _ => "\"" + name.Replace("\"", "\"\"", StringComparison.Ordinal) + "\"",
        };
    }

    private static bool IsPlain(string name)
    {
        if (name.Length == 0)
        {
            return false;
        }

        for (var i = 0; i < name.Length; i++)
        {
            var c = char.ToLowerInvariant(name[i]);
            if (!(c is >= 'a' and <= 'z' || c == '_' || (i > 0 && c is >= '0' and <= '9')))
            {
                return false;
            }
        }

        return true;
    }
}
