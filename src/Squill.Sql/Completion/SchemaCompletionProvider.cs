using Squill.Core;
using Squill.Sql.Analysis;
using Squill.Sql.Lexing;
using Squill.Sql.Syntax;

namespace Squill.Sql.Completion;

/// <summary>Proposes the schemas, tables, views, aliases and columns of the loaded schema snapshot.</summary>
public sealed class SchemaCompletionProvider : ISqlCompletionProvider
{
    /// <inheritdoc />
    public IReadOnlyList<CompletionItem> GetCompletions(SqlRequest request)
    {
        if (request.Schema is not { } schema)
        {
            return [];
        }

        var text = request.Text;
        var offset = Math.Clamp(request.Offset, 0, text.Length);
        var start = offset;
        while (start > 0 && (char.IsLetterOrDigit(text[start - 1]) || text[start - 1] is '_' or '$'))
        {
            start--;
        }

        var range = new TextRange(start, offset - start);
        var before = text[..start];
        var tokens = SqlTokenizers.For(request.Dialect).Tokenize(before)
            .Where(t => t.Kind is not (TokenKind.Whitespace or TokenKind.LineComment or TokenKind.BlockComment or TokenKind.EndOfFile))
            .Select(t => (t.Kind, Text: t.Text(before)))
            .ToList();

        // identifiers written before the caret as "a." or "a.b."
        var qualifiers = new List<string>();
        var end = tokens.Count;
        while (end >= 2 && tokens[end - 1].Text == "." && tokens[end - 2].Kind is TokenKind.Identifier or TokenKind.QuotedIdentifier or TokenKind.Keyword)
        {
            qualifiers.Insert(0, Unquote(tokens[end - 2]));
            end -= 2;
        }

        var scope = SqlScopeResolver.Resolve(SqlParser.Parse(text, request.Dialect), offset);
        var previous = end > 0 ? tokens[end - 1].Text : null;
        var tablePosition = previous is not null
            && (previous.Equals("FROM", StringComparison.OrdinalIgnoreCase)
                || previous.Equals("JOIN", StringComparison.OrdinalIgnoreCase)
                || previous.Equals("INTO", StringComparison.OrdinalIgnoreCase)
                || previous.Equals("UPDATE", StringComparison.OrdinalIgnoreCase)
                || (previous == "," && scope.Clause == ClauseKind.From));

        if (tablePosition)
        {
            return Tables(schema, qualifiers, request.Dialect, range);
        }

        if (scope.Clause is ClauseKind.Select or ClauseKind.Where or ClauseKind.On or ClauseKind.GroupBy
            or ClauseKind.Having or ClauseKind.OrderBy or ClauseKind.Set)
        {
            return Columns(schema, scope, qualifiers, request.Dialect, range);
        }

        return [];
    }

    private static List<CompletionItem> Tables(SchemaSnapshot schema, List<string> qualifiers, DatabaseDialect dialect, TextRange range)
    {
        var items = new List<CompletionItem>();
        if (qualifiers.Count > 0)
        {
            var owner = qualifiers[^1];
            foreach (var t in schema.Tables.Where(t => string.Equals(t.Schema, owner, StringComparison.OrdinalIgnoreCase)))
            {
                items.Add(Table(t, dialect, range));
            }

            return items;
        }

        items.AddRange(schema.Tables.Select(t => Table(t, dialect, range)));
        var schemas = schema.Tables.Select(t => t.Schema).OfType<string>().Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        if (schemas.Count > 1)
        {
            items.AddRange(schemas.Select(s => new CompletionItem(s, CompletionKind.Module, IdentifierQuoting.Quote(s, dialect), "schema", "1" + s, range)));
        }

        return items;
    }

    private static CompletionItem Table(TableInfo t, DatabaseDialect dialect, TextRange range) =>
        new(t.Name, CompletionKind.Struct, IdentifierQuoting.Quote(t.Name, dialect), t.Kind == TableKind.View ? "view" : "table", "0" + t.Name, range);

    private static List<CompletionItem> Columns(SchemaSnapshot schema, ScopeInfo scope, List<string> qualifiers, DatabaseDialect dialect, TextRange range)
    {
        var sources = scope.Sources.Where(s => Matches(s, qualifiers)).ToList();
        var columns = sources.Select(s => (Source: s, Columns: ColumnsOf(schema, s))).ToList();
        var items = new List<CompletionItem>();
        foreach (var (source, cols) in columns)
        {
            var owner = source.Alias ?? source.Name;
            foreach (var (name, type) in cols)
            {
                var shared = columns.Count(c => c.Columns.Any(x => string.Equals(x.Name, name, StringComparison.OrdinalIgnoreCase))) > 1;
                var detail = shared ? (type is null ? owner : owner + "." + type) : type;
                items.Add(new CompletionItem(name, CompletionKind.Field, IdentifierQuoting.Quote(name, dialect), detail, "0" + name, range));
            }
        }

        if (qualifiers.Count == 0)
        {
            foreach (var source in sources.Where(s => s.Name.Length > 0 || s.Alias is not null))
            {
                var label = source.Alias ?? source.Name;
                items.Add(new CompletionItem(label, CompletionKind.Class, IdentifierQuoting.Quote(label, dialect), source.Alias is null ? "table" : "alias", "1" + label, range));
            }
        }

        return items;
    }

    /// <summary>Tells whether a source is the target of the qualifiers written before the caret (none: every source).</summary>
    private static bool Matches(VisibleSource source, List<string> qualifiers)
    {
        if (qualifiers.Count == 0)
        {
            return true;
        }

        var name = qualifiers[^1];
        if (source.Alias is not null)
        {
            return qualifiers.Count == 1 && string.Equals(source.Alias, name, StringComparison.OrdinalIgnoreCase);
        }

        return string.Equals(source.Name, name, StringComparison.OrdinalIgnoreCase)
            && (qualifiers.Count == 1 || string.Equals(source.Schema, qualifiers[^2], StringComparison.OrdinalIgnoreCase));
    }

    private static List<(string Name, string? Type)> ColumnsOf(SchemaSnapshot schema, VisibleSource source)
    {
        if (source.DerivedColumns is { } derived)
        {
            return [.. derived.Select(n => (n, (string?)null))];
        }

        return schema.FindTable(source.Schema, source.Name) is { } table
            ? [.. table.Columns.Select(c => (c.Name, (string?)c.DataType))]
            : [];
    }

    private static string Unquote((TokenKind Kind, string Text) token) =>
        token.Kind == TokenKind.QuotedIdentifier && token.Text.Length >= 2 ? token.Text[1..^1] : token.Text;
}
