using System.Globalization;
using System.Text;

namespace Squill.Corpus;

/// <summary>One statement (or short script) of a dialect corpus file, with the directives written next to it.</summary>
/// <param name="Index">One-based position in the file.</param>
/// <param name="Sql">The text without its directive lines.</param>
/// <param name="Statements">How many statements the splitter must find in <paramref name="Sql"/> (<c>-- statements: N</c>, default 1).</param>
/// <param name="Expect">The message code a statement of an invalid corpus must produce (<c>-- expect: code</c>).</param>
/// <param name="Unsupported">The reason the parser does not support the construct yet (<c>-- unsupported: reason</c>); the parser test skips the entry.</param>
public sealed record CorpusEntry(int Index, string Sql, int Statements, string? Expect, string? Unsupported);

/// <summary>Reads <c>SqlCorpus/&lt;dialect&gt;.valid.sql</c> and <c>.invalid.sql</c>: entries separated by lines <c>-- ---</c>.</summary>
public static class SqlCorpus
{
    /// <summary>Loads one corpus file copied next to the test assembly.</summary>
    /// <param name="dialect">File prefix: <c>postgres</c>, <c>mysql</c>, <c>sqlserver</c> or <c>sqlite</c>.</param>
    /// <param name="kind"><c>valid</c> or <c>invalid</c>.</param>
    /// <returns>The entries in file order.</returns>
    public static IReadOnlyList<CorpusEntry> Load(string dialect, string kind)
    {
        var text = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "SqlCorpus", $"{dialect}.{kind}.sql"));
        var entries = new List<CorpusEntry>();
        var body = new StringBuilder();
        var statements = 1;
        string? expect = null, unsupported = null;

        void Flush()
        {
            var sql = body.ToString().Trim();
            if (sql.Length > 0)
                entries.Add(new CorpusEntry(entries.Count + 1, sql, statements, expect, unsupported));
            body.Clear();
            statements = 1;
            expect = unsupported = null;
        }

        foreach (var line in text.ReplaceLineEndings("\n").Split('\n'))
        {
            if (line == "-- ---")
                Flush();
            else if (line.StartsWith("-- statements: ", StringComparison.Ordinal))
                statements = int.Parse(line["-- statements: ".Length..], CultureInfo.InvariantCulture);
            else if (line.StartsWith("-- expect: ", StringComparison.Ordinal))
                expect = line["-- expect: ".Length..].Trim();
            else if (line.StartsWith("-- unsupported: ", StringComparison.Ordinal))
                unsupported = line["-- unsupported: ".Length..].Trim();
            else
                body.Append(line).Append('\n');
        }

        Flush();
        return entries;
    }
}
