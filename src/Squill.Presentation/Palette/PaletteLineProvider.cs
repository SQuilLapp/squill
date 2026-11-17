using Squill.Core;
using Squill.Presentation.Localization;

namespace Squill.Presentation.Palette;

/// <summary>Builds the single row of the <c>:</c> mode, which moves the caret of the active editor to a line.</summary>
/// <param name="host">The workspace, or a fake of it.</param>
public sealed class PaletteLineProvider(IPaletteEditorHost host)
{
    /// <summary>Builds the row for a typed term.</summary>
    /// <param name="term">The text after the <c>:</c> prefix.</param>
    /// <returns>The go-to-line row, or a disabled hint or notice row when the term is not a number or no editor is active.</returns>
    public IReadOnlyList<PaletteItem> Search(string term)
    {
        if (!PaletteQuery.TryLine(term, out var requested))
            return [Row(UiText.Get("palette.line.hint"), "", false, null)];
        var editor = host.ActiveEditor;
        if (editor is null)
            return [Row(UiText.Format("palette.line.goto", requested), UiText.Get("palette.line.noEditor"), false, null)];

        var starts = LineStarts(editor.Text);
        var line = Math.Clamp(requested, 1, starts.Count);
        return [Row(UiText.Format("palette.line.goto", line), UiText.Format("palette.line.total", starts.Count), true, () =>
        {
            editor.CaretOffset = starts[line - 1];
            editor.Selection = new TextRange(starts[line - 1], 0);
        })];
    }

    private static PaletteItem Row(string title, string detail, bool enabled, Action? go) =>
        new(PaletteItemKind.Line, title, detail, null, null, null, "line", [], enabled, 0,
            (_, _) =>
            {
                go?.Invoke();
                return ValueTask.CompletedTask;
            });

    private static List<int> LineStarts(string text)
    {
        List<int> starts = [0];
        for (var i = 0; i < text.Length; i++)
        {
            if (text[i] == '\r' && i + 1 < text.Length && text[i + 1] == '\n')
                i++;
            else if (text[i] is not ('\r' or '\n'))
                continue;
            starts.Add(i + 1);
        }
        return starts;
    }
}
