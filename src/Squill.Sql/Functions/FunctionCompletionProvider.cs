using Squill.Core;
using Squill.Sql.Analysis;
using Squill.Sql.Completion;
using Squill.Sql.Syntax;

namespace Squill.Sql.Functions;

/// <summary>Proposes the built-in functions where an expression is expected.</summary>
public sealed class FunctionCompletionProvider : ISqlCompletionProvider
{
    /// <inheritdoc />
    public IReadOnlyList<CompletionItem> GetCompletions(SqlRequest request)
    {
        var text = request.Text;
        var offset = Math.Clamp(request.Offset, 0, text.Length);
        var scope = SqlScopeResolver.Resolve(SqlParser.Parse(text, request.Dialect), offset);
        if (scope.Clause is not (ClauseKind.Select or ClauseKind.Where or ClauseKind.On or ClauseKind.Having
            or ClauseKind.OrderBy or ClauseKind.Set))
        {
            return [];
        }

        var start = offset;
        while (start > 0 && (char.IsLetterOrDigit(text[start - 1]) || text[start - 1] is '_' or '$'))
        {
            start--;
        }

        var range = new TextRange(start, offset - start);
        return [.. FunctionCatalog.For(request.Dialect)
            .Select(f => new CompletionItem(f.Name, CompletionKind.Function, f.Name, f.Display, null, range))];
    }
}
