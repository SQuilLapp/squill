using Squill.Core;
using Squill.Sql.Lexing;

namespace Squill.Sql.Completion;

/// <summary>Proposes statement templates (<c>sel</c>, <c>ins</c>, <c>upd</c>, <c>del</c>, <c>cte</c>, <c>crt</c>) where a statement may start.</summary>
public sealed class SnippetCompletionProvider : ISqlCompletionProvider
{
    /// <inheritdoc />
    public IReadOnlyList<CompletionItem> GetCompletions(SqlRequest request)
    {
        var text = request.Text;
        var offset = Math.Clamp(request.Offset, 0, text.Length);
        var start = offset;
        while (start > 0 && (char.IsLetterOrDigit(text[start - 1]) || text[start - 1] is '_' or '$'))
        {
            start--;
        }

        var before = text[..start];
        var tokens = SqlTokenizers.For(request.Dialect).Tokenize(before);
        var last = tokens.LastOrDefault(t => t.Kind is not (TokenKind.Whitespace or TokenKind.LineComment or TokenKind.BlockComment or TokenKind.EndOfFile));
        if (last.Kind != default && last.Text(before) != ";")
        {
            return [];
        }

        string K(string word) => KeywordCasing.Apply(word, request, start, offset - start);
        var range = new TextRange(start, offset - start);
        var id = request.Dialect == DatabaseDialect.Sqlite ? "INTEGER" : "INT";
        var templates = new (string Abbreviation, string Detail, string Template)[]
        {
            ("sel", "SELECT ... FROM ... WHERE ...", $"{K("SELECT")} ${{1:*}}\n{K("FROM")} ${{2:table}}\n{K("WHERE")} ${{3:condition}}$0"),
            ("ins", "INSERT INTO ... VALUES ...", $"{K("INSERT INTO")} ${{1:table}} (${{2:columns}})\n{K("VALUES")} (${{3:values}})$0"),
            ("upd", "UPDATE ... SET ... WHERE ...", $"{K("UPDATE")} ${{1:table}}\n{K("SET")} ${{2:column}} = ${{3:value}}\n{K("WHERE")} ${{4:condition}}$0"),
            ("del", "DELETE FROM ... WHERE ...", $"{K("DELETE FROM")} ${{1:table}}\n{K("WHERE")} ${{2:condition}}$0"),
            ("cte", "WITH ... AS (...) SELECT ...", $"{K("WITH")} ${{1:name}} {K("AS")} (\n    ${{2:SELECT 1}}\n)\n{K("SELECT")} * {K("FROM")} ${{1:name}}$0"),
            ("crt", "CREATE TABLE ... (...)", $"{K("CREATE TABLE")} ${{1:name}} (\n    ${{2:id}} ${{3:{id}}} {K("PRIMARY KEY")}$0\n)"),
        };
        return [.. templates.Select(t => new CompletionItem(
            t.Abbreviation, CompletionKind.Snippet, t.Template, t.Detail, "09" + t.Abbreviation, range, t.Abbreviation,
            "```sql\n" + SnippetText.ToPlain(t.Template) + "\n```", IsSnippet: true))];
    }
}
