using System.Globalization;
using Squill.Core;
using Squill.Sql.Completion;
using Squill.Sql.Lexing;
using Squill.Sql.Syntax;

namespace Squill.Sql.Analysis;

/// <summary>Describes the table or column under the caret using the loaded schema.</summary>
public static class SqlHoverProvider
{
    /// <summary>Describes the identifier at <see cref="SqlRequest.Offset"/>.</summary>
    /// <param name="request">The script, the pointer offset, the dialect and the schema.</param>
    /// <returns>The description, or <see langword="null"/> when there is no schema or nothing to say.</returns>
    public static HoverInfo? GetHover(SqlRequest request)
    {
        if (request.Schema is not { } schema)
        {
            return null;
        }

        var text = request.Text;
        var offset = Math.Clamp(request.Offset, 0, text.Length);
        var token = SqlTokenizers.For(request.Dialect).Tokenize(text)
            .FirstOrDefault(t => t.Kind is TokenKind.Identifier or TokenKind.QuotedIdentifier && t.Start <= offset && offset < t.End);
        if (token.Length == 0)
        {
            return null;
        }

        var word = token.Text(text);
        if (token.Kind == TokenKind.QuotedIdentifier && word.Length >= 2)
        {
            word = word[1..^1];
        }

        var range = new TextRange(token.Start, token.Length);
        var script = SqlParser.Parse(text, request.Dialect);
        var node = SqlTreeWalker.References(script)
            .Where(r => r.Node.Start <= token.Start && token.End <= r.Node.End)
            .OrderBy(r => r.Node.Length)
            .Select(r => r.Node)
            .FirstOrDefault();
        switch (node)
        {
            case TableRef t when Same(t.Name, word) && schema.FindTable(t.Schema, t.Name) is { } table:
                return Describe(range, table);
            case ColumnRef c when Same(c.Name, word) && token.End == c.End:
                List<string> qualifiers = c.Qualifier is null ? [] : [.. c.Qualifier.Split('.')];
                var hits = SqlScopeResolver.Resolve(script, token.Start).Sources
                    .Where(s => SchemaCompletionProvider.Matches(s, qualifiers) && s.DerivedColumns is null)
                    .Select(s => schema.FindTable(s.Schema, s.Name)?.Columns.FirstOrDefault(x => Same(x.Name, word)))
                    .OfType<ColumnDefinition>()
                    .Distinct()
                    .ToList();
                return hits.Count == 1 ? Describe(range, hits[0]) : null;
            default:
                return null;
        }
    }

    private static bool Same(string a, string b) => string.Equals(a, b, StringComparison.OrdinalIgnoreCase);

    private static HoverInfo Describe(TextRange range, TableInfo t)
    {
        var name = t.Schema is null ? t.Name : t.Schema + "." + t.Name;
        var view = t.Kind.IsViewLike();
        var markdown = $"**{(view ? "view" : "table")}** {name} ({t.Columns.Count} {(t.Columns.Count == 1 ? "column" : "columns")})";
        return new HoverInfo(range, markdown, view ? SqlMessageCodes.HoverView : SqlMessageCodes.HoverTable, [t.Columns.Count.ToString(CultureInfo.InvariantCulture), name]);
    }

    private static HoverInfo Describe(TextRange range, ColumnDefinition c)
    {
        var markdown = $"`{c.Name}`: {c.DataType}, {(c.IsNullable ? "nullable" : "not null")}{(c.IsPrimaryKey ? ", PK" : string.Empty)}";
        var code = (c.IsNullable, c.IsPrimaryKey) switch
        {
            (true, false) => SqlMessageCodes.HoverColumnNullable,
            (false, false) => SqlMessageCodes.HoverColumnNotNull,
            (true, true) => SqlMessageCodes.HoverColumnNullablePk,
            (false, true) => SqlMessageCodes.HoverColumnNotNullPk,
        };
        return new HoverInfo(range, markdown, code, [c.Name, c.DataType]);
    }
}
