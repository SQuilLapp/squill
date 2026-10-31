using Avalonia.Media;
using AvaloniaEdit.Document;
using AvaloniaEdit.Rendering;
using Squill.Core;
using Squill.Sql.Analysis;
using Squill.Sql.Highlighting;

namespace Squill.App.Avalonia.Editors;

/// <summary>Paints syntax colors and diagnostic underlines on the lines AvaloniaEdit asks for, that is the visible ones.</summary>
internal sealed class SqlColorizer : DocumentColorizingTransformer
{
    private IReadOnlyList<HighlightSpan>? _spans;
    private IReadOnlyList<SqlDiagnostic> _diagnostics = [];

    public DatabaseDialect Dialect { get; set; } = DatabaseDialect.Sqlite;

    /// <summary>Drops the cached classification; it is recomputed on the next painted line.</summary>
    public void Invalidate() => _spans = null;

    public void SetDiagnostics(IReadOnlyList<SqlDiagnostic> diagnostics) => _diagnostics = diagnostics;

    protected override void ColorizeLine(DocumentLine line)
    {
        _spans ??= SqlHighlighter.Classify(CurrentContext.Document.Text, Dialect);
        foreach (var span in _spans)
        {
            var start = Math.Max(span.Start, line.Offset);
            var end = Math.Min(span.Start + span.Length, line.EndOffset);
            if (start < end && Brush(span.Kind) is { } brush)
            {
                ChangeLinePart(start, end, e => e.TextRunProperties.SetForegroundBrush(brush));
            }
        }

        foreach (var d in _diagnostics)
        {
            var start = Math.Max(d.Range.Start, line.Offset);
            var end = Math.Min(d.Range.Start + d.Range.Length, line.EndOffset);
            if (start < end)
            {
                var color = d.Severity == DiagnosticSeverity.Error ? Colors.IndianRed : Colors.Orange;
                ChangeLinePart(start, end, e => e.TextRunProperties.SetTextDecorations(
                [
                    new TextDecoration { Location = TextDecorationLocation.Underline, Stroke = new SolidColorBrush(color), StrokeThickness = 1.5 },
                ]));
            }
        }
    }

    private static IBrush? Brush(HighlightKind kind) => Resource(kind switch
    {
        HighlightKind.Keyword => "SquillSynKeywordBrush",
        HighlightKind.Function => "SquillSynFunctionBrush",
        HighlightKind.StringLiteral or HighlightKind.QuotedIdentifier => "SquillSynStringBrush",
        HighlightKind.Number => "SquillSynNumberBrush",
        HighlightKind.Comment => "SquillSynCommentBrush",
        HighlightKind.Parameter => "SquillSynParamBrush",
        HighlightKind.Operator => "SquillSynOperatorBrush",
        _ => null,
    });

    private static IBrush? Resource(string? key) =>
        key is not null && global::Avalonia.Application.Current is { } app
        && app.TryGetResource(key, app.ActualThemeVariant, out var value) ? value as IBrush : null;
}
