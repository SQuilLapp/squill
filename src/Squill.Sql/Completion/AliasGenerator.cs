using System.Collections.Frozen;
using System.Text;
using Squill.Core;
using Squill.Sql.Analysis;
using Squill.Sql.Keywords;

namespace Squill.Sql.Completion;

/// <summary>Proposes short table aliases for generated joins and decides when one is needed.</summary>
internal static class AliasGenerator
{
    /// <summary>Builds a lower-case alias from the words of a table name that collides with nothing.</summary>
    /// <param name="tableName">The bare table name.</param>
    /// <param name="taken">Existing aliases and source names (compared case-insensitively).</param>
    /// <param name="dialect">The engine, which decides the reserved words.</param>
    /// <returns>A plain lower-case identifier, to be quoted by the caller.</returns>
    public static string Generate(string tableName, IReadOnlyCollection<string> taken, DatabaseDialect dialect)
    {
        var reserved = Reserved(dialect);
        var words = Split(tableName);
        var initials = new string(words.Select(w => w[0]).ToArray());

        foreach (var candidate in Candidates(words[0], initials))
        {
            if (IsFree(candidate, taken, reserved))
            {
                return candidate;
            }
        }

        for (var n = 2; ; n++)
        {
            var candidate = initials + n;
            if (IsFree(candidate, taken, reserved))
            {
                return candidate;
            }
        }
    }

    /// <summary>Tells whether the new table must be aliased in the FROM list.</summary>
    /// <param name="leftSources">The sources already in the FROM list.</param>
    /// <param name="newSchema">Schema of the new table, or <see langword="null"/> when not qualified.</param>
    /// <param name="newTable">Bare name of the new table.</param>
    /// <param name="selfReference">Whether the relation joins a table to itself.</param>
    /// <returns><see langword="true"/> when an alias is required.</returns>
    public static bool IsAliasNeeded(IReadOnlyList<VisibleSource> leftSources, string? newSchema, string newTable, bool selfReference)
    {
        if (selfReference)
        {
            return true;
        }

        foreach (var source in leftSources)
        {
            if (source.Alias is not null)
            {
                return true;
            }

            if (string.Equals(source.Name, newTable, StringComparison.OrdinalIgnoreCase)
                && (newSchema is null || source.Schema is null
                    || string.Equals(source.Schema, newSchema, StringComparison.OrdinalIgnoreCase)))
            {
                return true;
            }
        }

        return false;
    }

    private static IEnumerable<string> Candidates(string first, string initials)
    {
        yield return initials;
        var rest = initials[1..];
        for (var len = 2; len <= 3; len++)
        {
            if (first.Length >= len)
            {
                yield return first[..len] + rest;
            }
        }
    }

    private static bool IsFree(string candidate, IReadOnlyCollection<string> taken, FrozenSet<string> reserved)
        => !reserved.Contains(candidate) && !taken.Any(t => string.Equals(t, candidate, StringComparison.OrdinalIgnoreCase));

    private static FrozenSet<string> Reserved(DatabaseDialect dialect) => dialect switch
    {
        DatabaseDialect.PostgreSql => PostgresKeywords.Words,
        DatabaseDialect.MySql => MySqlKeywords.Words,
        DatabaseDialect.SqlServer => TSqlKeywords.Words,
        DatabaseDialect.Sqlite => SqliteKeywords.Words,
        _ => AnsiKeywords.Words,
    };

    /// <summary>Splits at separators, lower-to-upper boundaries and acronym ends; lower-cases the words.</summary>
    private static List<string> Split(string name)
    {
        var words = new List<string>();
        if (name.Length == 0 || !char.IsLetter(name[0]))
        {
            words.Add("t");
            return words;
        }

        var current = new StringBuilder();
        for (var i = 0; i < name.Length; i++)
        {
            var c = name[i];
            if (!char.IsLetterOrDigit(c))
            {
                Flush();
                continue;
            }

            var boundary = current.Length > 0 && char.IsUpper(c)
                && (char.IsLower(name[i - 1]) || (char.IsUpper(name[i - 1]) && i + 1 < name.Length && char.IsLower(name[i + 1])));
            if (boundary)
            {
                Flush();
            }

            current.Append(char.ToLowerInvariant(c));
        }

        Flush();

        // A word starting with a digit has no letter to take as initial.
        words.RemoveAll(w => !char.IsLetter(w[0]));
        if (words.Count == 0)
        {
            words.Add("t");
        }

        return words;

        void Flush()
        {
            if (current.Length > 0)
            {
                words.Add(current.ToString());
                current.Clear();
            }
        }
    }
}
