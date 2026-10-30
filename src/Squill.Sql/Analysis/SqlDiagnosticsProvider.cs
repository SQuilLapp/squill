using Squill.Core;
using Squill.Sql.Completion;
using Squill.Sql.Syntax;

namespace Squill.Sql.Analysis;

/// <summary>Reports syntax errors and, when a schema is loaded, unknown tables and columns.</summary>
public static class SqlDiagnosticsProvider
{
    private static readonly HashSet<string> Niladic = new(StringComparer.OrdinalIgnoreCase)
    {
        "CURRENT_DATE", "CURRENT_TIME", "CURRENT_TIMESTAMP", "CURRENT_USER", "SESSION_USER", "LOCALTIME", "LOCALTIMESTAMP",
    };

    /// <summary>Analyzes the whole text of <paramref name="request"/>; never throws.</summary>
    /// <param name="request">The script, its dialect and the optional schema; the offset is ignored.</param>
    /// <returns>The diagnostics in order of position.</returns>
    public static IReadOnlyList<SqlDiagnostic> GetDiagnostics(SqlRequest request)
    {
        var text = request.Text;
        var script = SqlParser.Parse(text, request.Dialect);
        var result = new List<SqlDiagnostic>();
        foreach (var d in script.Diagnostics)
        {
            result.Add(new SqlDiagnostic(Clamp(d.Start, d.Length, text), DiagnosticSeverity.Error, d.Message, "SQL0001"));
        }

        if (request.Schema is { } schema)
        {
            // shortcut: scope is resolved per column reference (re-tokenizes the text), upgrade if huge scripts get slow
            foreach (var r in SqlTreeWalker.References(script))
            {
                switch (r.Node)
                {
                    case TableRef t when IsUnknownTable(schema, t, r.Ctes):
                        result.Add(new SqlDiagnostic(
                            NameRange(text, t, t.Name, t.Schema?.Length ?? 0, last: false), DiagnosticSeverity.Warning,
                            $"Unknown table '{t.Name}'", "SQL1001"));
                        break;
                    case ColumnRef c when UnknownColumnOwner(script, schema, c, r.Aliases) is { } owner:
                        result.Add(new SqlDiagnostic(
                            NameRange(text, c, c.Name, 0, last: true), DiagnosticSeverity.Warning,
                            $"Unknown column '{c.Name}' in '{owner}'", "SQL1002"));
                        break;
                }
            }
        }

        return [.. result.OrderBy(d => d.Range.Start)];
    }

    private static bool IsUnknownTable(SchemaSnapshot schema, TableRef t, IReadOnlyList<string> ctes)
    {
        if (t.Schema is null && ctes.Any(c => string.Equals(c, t.Name, StringComparison.OrdinalIgnoreCase)))
        {
            return false;
        }

        // an ambiguous unqualified name is known (FindTable returns null for it)
        return schema.FindTable(t.Schema, t.Name) is null
            && (t.Schema is not null || !schema.Tables.Any(x => string.Equals(x.Name, t.Name, StringComparison.OrdinalIgnoreCase)));
    }

    /// <summary>Returns the name to report as owner of a missing column, or <see langword="null"/> when the column exists or cannot be checked.</summary>
    private static string? UnknownColumnOwner(SqlScript script, SchemaSnapshot schema, ColumnRef c, IReadOnlySet<string> aliases)
    {
        var sources = SqlScopeResolver.Resolve(script, c.Start).Sources;
        if (c.Qualifier is { } qualifier)
        {
            var matched = sources.Where(s => SchemaCompletionProvider.Matches(s, [.. qualifier.Split('.')])).ToList();
            var known = matched.Select(s => SchemaCompletionProvider.ColumnsOf(schema, s)).Where(cols => cols.Count > 0).ToList();
            return matched.Count > 0 && known.Count == matched.Count && known.All(cols => !Has(cols, c.Name)) ? qualifier : null;
        }

        if (sources.Count != 1 || aliases.Contains(c.Name) || Niladic.Contains(c.Name))
        {
            return null;
        }

        var columns = SchemaCompletionProvider.ColumnsOf(schema, sources[0]);
        return columns.Count > 0 && !Has(columns, c.Name) ? sources[0].Alias ?? sources[0].Name : null;
    }

    private static bool Has(List<(string Name, string? Type)> columns, string name) =>
        columns.Any(x => string.Equals(x.Name, name, StringComparison.OrdinalIgnoreCase));

    /// <summary>Narrows a node to the identifier it holds, falling back to the whole node when it is quoted differently.</summary>
    private static TextRange NameRange(string text, SyntaxNode node, string name, int skip, bool last)
    {
        var span = text.AsSpan(node.Start, node.Length);
        skip = Math.Min(skip, span.Length);
        var index = last
            ? span.LastIndexOf(name, StringComparison.OrdinalIgnoreCase)
            : span[skip..].IndexOf(name, StringComparison.OrdinalIgnoreCase) is >= 0 and var i ? i + skip : -1;
        return index < 0 ? Clamp(node.Start, node.Length, text) : Clamp(node.Start + index, name.Length, text);
    }

    private static TextRange Clamp(int start, int length, string text)
    {
        start = Math.Clamp(start, 0, text.Length);
        return new TextRange(start, Math.Clamp(length, 0, text.Length - start));
    }
}
