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

        // A prefix typed after an opening quote ("Mix, `Mix, [Mix) is replaced together with the quote, since the items insert their own.
        var quote = request.Dialect switch { DatabaseDialect.MySql => '`', DatabaseDialect.SqlServer => '[', _ => '"' };
        if (start > 0 && text[start - 1] == quote && (quote == '[' || text.AsSpan(0, start).Count(quote) % 2 == 1))
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

        if (qualifiers.Count == 0 && InsertTarget(tokens, end) is var (insertSchema, insertTable) && schema.FindTable(insertSchema, insertTable) is { } target)
        {
            return [.. target.Columns.Select(c => new CompletionItem(c.Name, CompletionKind.Field, IdentifierQuoting.Quote(c.Name, request.Dialect), c.DataType, "0" + c.Name, range))];
        }

        if (tablePosition)
        {
            var tables = Tables(schema, qualifiers, request.Dialect, range);

            // Join rows go above the plain rows; a typed schema qualifier hides the join context from the analyzer, so none are offered then.
            if (qualifiers.Count == 0 && previous!.Equals("JOIN", StringComparison.OrdinalIgnoreCase))
            {
                tables.InsertRange(0, JoinRows.Build(schema, JoinContextAnalyzer.Analyze(request, start, offset), request.Dialect, range));
            }

            return tables;
        }

        if (scope.Clause is ClauseKind.Select or ClauseKind.Where or ClauseKind.On or ClauseKind.GroupBy
            or ClauseKind.Having or ClauseKind.OrderBy or ClauseKind.Set)
        {
            var columns = Columns(schema, scope, qualifiers, request.Dialect, range);
            if (scope.Clause == ClauseKind.On && qualifiers.Count == 0)
            {
                columns.InsertRange(0, OnClauseRows.Build(request, schema, scope, start, range));
            }

            return columns;
        }

        return [];
    }

    /// <summary>Recognizes <c>INSERT INTO [schema.]table (a, b, |</c> and returns the target the column list belongs to.</summary>
    private static (string? Schema, string Table)? InsertTarget(List<(TokenKind Kind, string Text)> tokens, int end)
    {
        var i = end - 1;
        while (i >= 1 && tokens[i].Text == "," && tokens[i - 1].Kind is TokenKind.Identifier or TokenKind.QuotedIdentifier or TokenKind.Keyword)
        {
            i -= 2;
        }

        if (i < 2 || tokens[i].Text != "(" || tokens[i - 1].Kind is not (TokenKind.Identifier or TokenKind.QuotedIdentifier))
        {
            return null;
        }

        var name = Unquote(tokens[i - 1]);
        var j = i - 2;
        string? owner = null;
        if (j >= 1 && tokens[j].Text == "." && tokens[j - 1].Kind is TokenKind.Identifier or TokenKind.QuotedIdentifier)
        {
            owner = Unquote(tokens[j - 1]);
            j -= 2;
        }

        return j >= 0 && tokens[j].Text.Equals("INTO", StringComparison.OrdinalIgnoreCase) ? (owner, name) : null;
    }

    private static List<CompletionItem> Tables(SchemaSnapshot schema, List<string> qualifiers, DatabaseDialect dialect, TextRange range)
    {
        var items = new List<CompletionItem>();
        if (qualifiers.Count > 0)
        {
            var owner = qualifiers[^1];
            foreach (var t in schema.Tables.Where(t => string.Equals(t.Schema, owner, StringComparison.OrdinalIgnoreCase)))
            {
                items.Add(Table(t, null, dialect, range));
            }

            return items;
        }

        items.AddRange(schema.Tables.Select(t => Table(t, schema, dialect, range)));
        var schemas = schema.Tables.Select(t => t.Schema).OfType<string>().Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        if (schemas.Count > 1)
        {
            items.AddRange(schemas.Select(s => new CompletionItem(s, CompletionKind.Module, IdentifierQuoting.Quote(s, dialect), "schema", "1" + s, range)));
        }

        return items;
    }

    // snapshot is null after "schema.": the schema is already typed, so the bare name is inserted.
    private static CompletionItem Table(TableInfo t, SchemaSnapshot? snapshot, DatabaseDialect dialect, TextRange range)
    {
        var kind = t.Kind.IsViewLike() ? "view" : "table";
        var qualified = snapshot is not null && TableReference.NeedsSchema(t, snapshot);
        return new CompletionItem(
            t.Name,
            CompletionKind.Struct,
            snapshot is null ? IdentifierQuoting.Quote(t.Name, dialect) : TableReference.Insert(t, snapshot, dialect),
            qualified ? $"{kind} ({t.Schema})" : kind,
            "0" + t.Name,
            range,
            t.Name);
    }

    private static List<CompletionItem> Columns(SchemaSnapshot schema, ScopeInfo scope, List<string> qualifiers, DatabaseDialect dialect, TextRange range)
    {
        var sources = scope.Sources.Where(s => Matches(s, qualifiers)).ToList();
        var columns = sources.Select(s => (Source: s, Columns: ColumnsOf(schema, s))).ToList();
        var items = new List<CompletionItem>();

        // Without a typed qualifier a name shared by two sources of the innermost statement must be written qualified, or the server rejects it as ambiguous.
        var local = qualifiers.Count == 0 ? columns.Take(scope.LocalCount).ToList() : [];
        for (var i = 0; i < columns.Count; i++)
        {
            var (source, cols) = columns[i];
            var owner = source.Alias ?? source.Name;
            var canQualify = i < local.Count && owner.Length > 0;
            foreach (var (name, type) in cols)
            {
                var shared = columns.Count(c => c.Columns.Any(x => string.Equals(x.Name, name, StringComparison.OrdinalIgnoreCase))) > 1;
                var detail = shared ? (type is null ? owner : owner + "." + type) : type;
                var insert = IdentifierQuoting.Quote(name, dialect);
                if (canQualify && local.Count(c => c.Columns.Any(x => string.Equals(x.Name, name, StringComparison.OrdinalIgnoreCase))) > 1)
                {
                    insert = IdentifierQuoting.Quote(owner, dialect) + "." + insert;
                }

                items.Add(new CompletionItem(name, CompletionKind.Field, insert, detail, "0" + name, range, name));
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
    internal static bool Matches(VisibleSource source, List<string> qualifiers)
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

    internal static List<(string Name, string? Type)> ColumnsOf(SchemaSnapshot schema, VisibleSource source)
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
