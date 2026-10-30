using Squill.Core;
using Squill.Sql.Lexing;
using Squill.Sql.Statements;

namespace Squill.Sql.Syntax;

/// <summary>Error-tolerant parser for SQL scripts.</summary>
public static class SqlParser
{
    /// <summary>Splits <paramref name="text"/> into statements and parses each one; never throws.</summary>
    /// <param name="text">The SQL text.</param>
    /// <param name="dialect">The dialect of the text.</param>
    /// <returns>The statements and the problems found.</returns>
    public static SqlScript Parse(string text, DatabaseDialect dialect)
    {
        var tokens = SqlTokenizers.For(dialect).Tokenize(text);
        var statements = new List<Statement>();
        var diagnostics = new List<ParseDiagnostic>();
        foreach (var span in SqlStatementSplitter.Split(text, dialect))
        {
            // the splitter keeps the terminating ';' in the span, the statement itself does not own it
            var inSpan = tokens.Where(t => t.Start >= span.Start && t.End <= span.End && !(t.End == span.End && text[t.Start] == ';' && t.Length == 1));
            var core = new SqlParserCore(text, dialect, inSpan);
            statements.Add(new SelectParser(core).ParseStatement());
            diagnostics.AddRange(core.Diagnostics);
        }

        return new SqlScript(statements, diagnostics);
    }
}
