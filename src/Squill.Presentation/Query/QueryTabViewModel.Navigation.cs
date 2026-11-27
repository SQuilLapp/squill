using System.Text;
using CommunityToolkit.Mvvm.Input;
using Squill.Core;
using Squill.Presentation.Localization;
using Squill.Sql.Analysis;
using Squill.Sql.Syntax;

namespace Squill.Presentation.Query;

public sealed partial class QueryTabViewModel
{
    /// <summary>Raised when a name under the caret is a table or view of the schema: the workspace selects it in the explorer, or opens it in the table designer when the flag is set.</summary>
    public event Action<TableInfo, bool>? TableDefinitionRequested;

    private (SqlScript Script, SqlSymbol? Symbol) SymbolUnderCaret()
    {
        var script = SqlParser.Parse(_editor.Text, _session.Profile.Dialect);
        return (script, SqlSymbolResolver.SymbolAt(script, _editor.CaretOffset, _session.Schema));
    }

    /// <summary>Moves the caret to the declaration of the name under the caret, or selects the table or view in the explorer.</summary>
    [RelayCommand]
    private void GoToDefinition() => GoToDefinition(false);

    /// <summary>Like <see cref="GoToDefinitionCommand"/>, but opens a table in the table designer instead of only selecting it.</summary>
    [RelayCommand]
    private void GoToDefinitionInDesigner() => GoToDefinition(true);

    private void GoToDefinition(bool inDesigner)
    {
        var (_, symbol) = SymbolUnderCaret();
        if (symbol?.DefinitionInScript is { } range)
        {
            _editor.CaretOffset = range.Start;
            _editor.Selection = range;
            return;
        }

        var table = symbol is { DefinitionInSchema: true, Kind: SqlSymbolKind.Table or SqlSymbolKind.View or SqlSymbolKind.Column }
            ? _session.Schema.FindTable(symbol.Schema, symbol.Kind == SqlSymbolKind.Column ? symbol.Table ?? "" : symbol.Name)
            : null;
        if (table is null)
        {
            StatusText = UiText.Get("editor.definition.none");
            return;
        }

        TableDefinitionRequested?.Invoke(table, inDesigner);
    }

    /// <summary>Highlights every usage of the name under the caret in the editor and lists them (line, column, text) as a message result.</summary>
    [RelayCommand]
    private void FindUsages()
    {
        var (script, symbol) = SymbolUnderCaret();
        var ranges = symbol is null ? [] : SqlSymbolResolver.Usages(script, symbol, _session.Schema);
        _editor.SetHighlights(ranges);
        if (symbol is null || ranges.Count == 0)
        {
            StatusText = UiText.Get("editor.usages.none");
            return;
        }

        var text = _editor.Text;
        var list = new StringBuilder();
        foreach (var range in ranges)
        {
            var start = Math.Clamp(range.Start, 0, text.Length);
            var lineStart = start == 0 ? 0 : text.LastIndexOf('\n', start - 1) + 1;
            var lineEnd = text.IndexOf('\n', start);
            var line = text[lineStart..(lineEnd < 0 ? text.Length : lineEnd)].TrimEnd('\r');
            var number = text.AsSpan(0, lineStart).Count('\n') + 1;
            list.Append(number).Append(':').Append(start - lineStart + 1).Append("  ").AppendLine(line.Trim());
        }

        StatusText = UiText.FormatPlural("editor.usages.status", ranges.Count, symbol.Name);
        AddResult(ResultViewModel.ForMessage(UiText.Format("editor.usages.title", symbol.Name), list.ToString().TrimEnd(), _dialogs));
    }

    /// <summary>Removes the usage highlights (bound to Esc by the heads).</summary>
    [RelayCommand]
    private void ClearHighlights() => _editor.SetHighlights([]);
}
