#if SQUILL_LOOMEDITOR
using System.Diagnostics;
using LoomEditor.Core.Languages.Features;
using LoomEditor.Core.Model;
using LoomEditor.Core.Text;
using Squill.Core;
using Squill.Sql.Analysis;
using Squill.Sql.Completion;
using Squill.Sql.Statements;
using LoomCompletionItem = LoomEditor.Core.Languages.Features.CompletionItem;
using LoomPosition = LoomEditor.Core.Text.Position;
using LoomRange = LoomEditor.Core.Text.Range;
using SqlCompletionItem = Squill.Sql.Completion.CompletionItem;
using SqlCompletionKind = Squill.Sql.Completion.CompletionKind;

namespace Squill.App;

/// <summary>The completion and hover providers both heads register on a LoomEditor control, over Squill's SQL providers.</summary>
internal sealed class LoomSqlProviders(Func<DatabaseDialect> dialectOf) : ICompletionItemProvider, IHoverProvider
{
    public ISqlCompletionProvider? Completion { get; set; }

    public Func<SqlRequest, HoverInfo?>? Hover { get; set; }

    public double LastCompletionMs { get; private set; }

    public IReadOnlyList<string>? TriggerCharacters { get; } = (string[])["."];

    public async Task<CompletionList?> ProvideCompletionItemsAsync(TextModel model, LoomPosition position, CompletionContext context, CancellationToken token)
    {
        if (Completion is not { } provider)
        {
            return null;
        }

        var started = Stopwatch.GetTimestamp();
        var text = model.GetValue(EndOfLinePreference.LF);
        var offset = model.GetOffsetAt(position);
        var dialect = dialectOf();
        var items = await Task.Run(
            () =>
            {
                var (statement, shift) = CurrentStatement(text, offset, dialect);
                return provider.GetCompletions(new SqlRequest(statement, offset - shift, dialect, null))
                    .Select(i => i with { ReplaceRange = new TextRange(i.ReplaceRange.Start + shift, i.ReplaceRange.Length) })
                    .ToArray();
            },
            token);
        var mapped = items.Select(i => Map(model, i)).ToArray();
        LastCompletionMs = Stopwatch.GetElapsedTime(started).TotalMilliseconds;
        return new CompletionList(mapped);
    }

    public async Task<Hover?> ProvideHoverAsync(TextModel model, LoomPosition position, CancellationToken token)
    {
        if (Hover is not { } provider)
        {
            return null;
        }

        var text = model.GetValue(EndOfLinePreference.LF);
        var offset = model.GetOffsetAt(position);
        var dialect = dialectOf();
        var info = await Task.Run(
            () =>
            {
                var (statement, shift) = CurrentStatement(text, offset, dialect);
                return provider(new SqlRequest(statement, offset - shift, dialect, null)) is { } h
                    ? h with { Range = new TextRange(h.Range.Start + shift, h.Range.Length) }
                    : null;
            },
            token);
        return info is null
            ? null
            : new Hover((MarkdownString[])[new MarkdownString(info.Markdown)], ToRange(model, info.Range));
    }

    // Providers parse the text they get; handing them only the statement at the caret keeps a 100,000-statement script interactive.
    // The splitter trims trailing blanks, so a caret after "FROM " lies past the end of its statement: that statement is extended to the caret.
    private static (string Text, int Shift) CurrentStatement(string text, int offset, DatabaseDialect dialect)
    {
        SqlStatementSpan? previous = null;
        foreach (var span in SqlStatementSplitter.Split(text, dialect))
        {
            if (span.Start > offset)
            {
                break;
            }

            if (offset <= span.Start + span.Length)
            {
                return (text.Substring(span.Start, span.Length), span.Start);
            }

            previous = span;
        }

        if (previous is { } last && text[last.End - 1] != ';')
        {
            return (text.Substring(last.Start, offset - last.Start), last.Start);
        }

        return ("", offset);
    }

    private static LoomCompletionItem Map(TextModel model, SqlCompletionItem item)
    {
        var range = ToRange(model, item.ReplaceRange);
        var target = new LoomCompletionItem(item.Label, item.InsertText, MapKind(item.Kind))
        {
            Range = new CompletionRange(range, range),
        };
        ApplyFields(target, item);
        return target;
    }

    /// <summary>Copies the fields of <paramref name="source"/> that do not depend on the text model.</summary>
    internal static void ApplyFields(LoomCompletionItem target, SqlCompletionItem source)
    {
        target.Detail = source.Detail;
        target.SortText = source.SortText;
        target.FilterText = source.FilterText;
        target.Documentation = source.Documentation is { } d ? new MarkdownString(d) : null;
        target.InsertTextRules = source.IsSnippet ? InsertTextRules.InsertAsSnippet : InsertTextRules.None;
    }

    private static CompletionItemKind MapKind(SqlCompletionKind kind) => kind switch
    {
        SqlCompletionKind.Method => CompletionItemKind.Method,
        SqlCompletionKind.Function => CompletionItemKind.Function,
        SqlCompletionKind.Field => CompletionItemKind.Field,
        SqlCompletionKind.Class => CompletionItemKind.Class,
        SqlCompletionKind.Module => CompletionItemKind.Module,
        SqlCompletionKind.Keyword => CompletionItemKind.Keyword,
        SqlCompletionKind.Snippet => CompletionItemKind.Snippet,
        SqlCompletionKind.Struct => CompletionItemKind.Struct,
        _ => CompletionItemKind.Text,
    };

    private static LoomRange ToRange(TextModel model, TextRange range) =>
        new(model.GetPositionAt(range.Start), model.GetPositionAt(range.End));
}
#endif
