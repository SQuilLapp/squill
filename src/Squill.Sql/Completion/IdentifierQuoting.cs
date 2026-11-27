using System.Collections.Frozen;
using Squill.Core;
using Squill.Sql.Keywords;

namespace Squill.Sql.Completion;

/// <summary>Quotes identifiers that are not plain names.</summary>
internal static class IdentifierQuoting
{
    /// <summary>Gets the text to insert for <paramref name="name"/>: unchanged when it matches <c>[a-z_][a-z0-9_]*</c> once lowercased, is not a keyword of the dialect (and, on PostgreSQL, has no capital), quoted for the dialect otherwise.</summary>
    /// <param name="name">The identifier.</param>
    /// <param name="dialect">The dialect that decides the quote characters.</param>
    /// <returns>The text to insert.</returns>
    public static string Quote(string name, DatabaseDialect dialect)
    {
        // PostgreSQL folds an unquoted name to lower case, so a name with capitals only resolves when quoted.
        // Oracle folds an unquoted name to upper case, so only [A-Z][A-Z0-9_$#]* resolves bare.
        if (dialect == DatabaseDialect.Oracle)
        {
            return IsOraclePlain(name) && !OracleKeywords.Words.Contains(name) ? name : "\"" + name.Replace("\"", "\"\"", StringComparison.Ordinal) + "\"";
        }

        if (IsPlain(name) && !KeywordsOf(dialect).Contains(name) && !(dialect == DatabaseDialect.PostgreSql && name.Any(char.IsUpper)))
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

    private static FrozenSet<string> KeywordsOf(DatabaseDialect dialect) => dialect switch
    {
        DatabaseDialect.PostgreSql => PostgresKeywords.Words,
        DatabaseDialect.MySql => MySqlKeywords.Words,
        DatabaseDialect.SqlServer => TSqlKeywords.Words,
        DatabaseDialect.Sqlite => SqliteKeywords.Words,
        DatabaseDialect.Oracle => OracleKeywords.Words,
        _ => AnsiKeywords.Words,
    };

    private static bool IsOraclePlain(string name) =>
        name.Length > 0 && name[0] is >= 'A' and <= 'Z' && name.All(c => c is >= 'A' and <= 'Z' or >= '0' and <= '9' or '_' or '$' or '#');

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
