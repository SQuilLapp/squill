using System.Globalization;
using Squill.Core.Plans;

namespace Squill.Presentation.Plans;

/// <summary>The rectangle of an operator card of the plan graph.</summary>
/// <param name="Id">The <see cref="PlanNode.Id"/> of the node.</param>
/// <param name="Row">Index of the node in the rows given to the layout.</param>
/// <param name="X">Left edge.</param>
/// <param name="Y">Top edge.</param>
/// <param name="Width">Card width.</param>
/// <param name="Height">Card height, grown by the extra lines (estimate error, warning, spill).</param>
/// <param name="ExtraLines">Number of extra lines the card shows, 0 to 2.</param>
public sealed record PlanNodeBox(int Id, int Row, double X, double Y, double Width, double Height, int ExtraLines);

/// <summary>A helper node drawn as a pill on the edge between its child and its parent.</summary>
/// <param name="Id">The <see cref="PlanNode.Id"/> of the helper node.</param>
/// <param name="Text">Name, abbreviated row count and memory when the engine gives it.</param>
/// <param name="X">Left edge.</param>
/// <param name="Y">Top edge.</param>
/// <param name="Width">Pill width.</param>
/// <param name="Height">Pill height.</param>
public sealed record PlanPill(int Id, string Text, double X, double Y, double Width, double Height);

/// <summary>A cubic Bezier from the right-middle of a child card to the left-middle of its parent card.</summary>
/// <param name="FromId">Id of the child node.</param>
/// <param name="ToId">Id of the parent node.</param>
/// <param name="X0">Start x.</param>
/// <param name="Y0">Start y.</param>
/// <param name="C1X">First control point x.</param>
/// <param name="C1Y">First control point y.</param>
/// <param name="C2X">Second control point x.</param>
/// <param name="C2Y">Second control point y.</param>
/// <param name="X1">End x.</param>
/// <param name="Y1">End y.</param>
/// <param name="Thickness">Stroke width, growing with the row count.</param>
/// <param name="Severity">Severity of the child node, which picks the color.</param>
public sealed record PlanEdgePath(
    int FromId, int ToId, double X0, double Y0, double C1X, double C1Y, double C2X, double C2Y, double X1, double Y1, double Thickness, PlanSeverity Severity);

/// <summary>The geometry of the plan graph at zoom 1.</summary>
/// <param name="Nodes">The operator cards.</param>
/// <param name="Pills">The helper pills.</param>
/// <param name="Edges">The edges.</param>
/// <param name="Width">Total width including margins.</param>
/// <param name="Height">Total height including margins.</param>
public sealed record PlanLayoutResult(
    IReadOnlyList<PlanNodeBox> Nodes, IReadOnlyList<PlanPill> Pills, IReadOnlyList<PlanEdgePath> Edges, double Width, double Height);

/// <summary>Places the operators of a plan from left (leaves) to right (root); pure geometry, no UI types.</summary>
public static class PlanLayout
{
    private const double CardWidth = 250;
    private const double CardHeight = 90;
    private const double ExtraLine = 20;
    private const double ColumnGap = 70;
    private const double SiblingGap = 30;
    private const double Margin = 24;
    private const double PillHeight = 22;
    private const double PillPadding = 24;

    /// <summary>Computes the layout of a plan in O(n).</summary>
    /// <param name="rows">The plan rows in pre-order, as <see cref="PlanViewModel.Rows"/> gives them.</param>
    /// <param name="measureText">Width of a text in the pill font, supplied by the head.</param>
    /// <returns>Cards, pills, edges and the size of the drawing.</returns>
    public static PlanLayoutResult Compute(IReadOnlyList<PlanRowViewModel> rows, Func<string, double> measureText)
    {
        ArgumentNullException.ThrowIfNull(rows);
        ArgumentNullException.ThrowIfNull(measureText);
        var n = rows.Count;
        if (n == 0)
        {
            return new PlanLayoutResult([], [], [], 0, 0);
        }

        // Parent row of each row, from the depths of the pre-order list.
        var parent = new int[n];
        var stack = new List<int>();
        for (var i = 0; i < n; i++)
        {
            var d = Math.Min(rows[i].Depth, stack.Count);
            stack.RemoveRange(d, stack.Count - d);
            parent[i] = d == 0 ? -1 : stack[d - 1];
            stack.Add(i);
        }

        var childCount = new int[n];
        for (var i = 1; i < n; i++)
        {
            if (parent[i] >= 0)
            {
                childCount[parent[i]]++;
            }
        }

        // A helper with exactly one child and a parent becomes a pill; anything else is a card (defensive).
        var isPill = new bool[n];
        for (var i = 0; i < n; i++)
        {
            isPill[i] = rows[i].IsHelper && childCount[i] == 1 && parent[i] >= 0;
        }

        var effParent = new int[n];
        var cardOf = new int[n];
        var cards = new List<int>();
        var column = new int[n];
        var maxColumn = 0;
        for (var i = 0; i < n; i++)
        {
            cardOf[i] = -1;
            if (isPill[i])
            {
                continue;
            }

            var p = parent[i];
            while (p >= 0 && isPill[p])
            {
                p = parent[p];
            }

            effParent[i] = p;
            column[i] = p < 0 ? 0 : column[p] + 1;
            maxColumn = Math.Max(maxColumn, column[i]);
            cardOf[i] = cards.Count;
            cards.Add(i);
        }

        var m = cards.Count;
        var height = new double[m];
        var extra = new int[m];
        var kids = new List<int>?[m];
        for (var c = 0; c < m; c++)
        {
            extra[c] = ExtraLines(rows[cards[c]]);
            height[c] = CardHeight + ExtraLine * extra[c];
            var p = effParent[cards[c]];
            if (p >= 0)
            {
                (kids[cardOf[p]] ??= []).Add(c);
            }
        }

        // Bottom-up (reverse pre-order): block height of each subtree and the node's center inside it.
        var block = new double[m];
        var center = new double[m];
        var shift = new double[m];
        for (var c = m - 1; c >= 0; c--)
        {
            var list = kids[c];
            if (list is null)
            {
                block[c] = height[c];
                center[c] = height[c] / 2;
                continue;
            }

            var offset = 0.0;
            var firstCenter = 0.0;
            var lastCenter = 0.0;
            for (var j = 0; j < list.Count; j++)
            {
                var k = list[j];
                var kc = offset + center[k];
                firstCenter = j == 0 ? kc : firstCenter;
                lastCenter = kc;
                offset += block[k] + (j < list.Count - 1 ? SiblingGap : 0);
            }

            var mid = (firstCenter + lastCenter) / 2;
            var s = Math.Max(0, height[c] / 2 - mid);
            shift[c] = s;
            center[c] = mid + s;
            block[c] = Math.Max(offset + s, mid + s + height[c] / 2);
        }

        // Top-down: absolute block tops.
        var top = new double[m];
        top[0] = Margin;
        var y = new double[m];
        for (var c = 0; c < m; c++)
        {
            y[c] = top[c] + center[c] - height[c] / 2;
            var list = kids[c];
            if (list is null)
            {
                continue;
            }

            var offset = top[c] + shift[c];
            foreach (var k in list)
            {
                top[k] = offset;
                offset += block[k] + SiblingGap;
            }
        }

        double maxRows = 0;
        for (var i = 0; i < n; i++)
        {
            maxRows = Math.Max(maxRows, RowCount(rows[i]) ?? 0);
        }

        var boxes = new List<PlanNodeBox>(m);
        var x = new double[m];
        for (var c = 0; c < m; c++)
        {
            x[c] = Margin + (maxColumn - column[cards[c]]) * (CardWidth + ColumnGap);
            boxes.Add(new PlanNodeBox(rows[cards[c]].Node.Id, cards[c], x[c], y[c], CardWidth, height[c], extra[c]));
        }

        var edges = new List<PlanEdgePath>(m);
        var pills = new List<PlanPill>();
        for (var c = 1; c < m; c++)
        {
            var row = cards[c];
            var pc = cardOf[effParent[row]];
            var x0 = x[c] + CardWidth;
            var y0 = y[c] + height[c] / 2;
            var x1 = x[pc];
            var y1 = y[pc] + height[pc] / 2;
            var cx = (x0 + x1) / 2;
            var thickness = 1.5;
            if (RowCount(rows[row]) is { } r && maxRows > 0)
            {
                thickness = Math.Clamp(1.5 + 6 * Math.Log10(1 + r) / Math.Log10(1 + maxRows), 1.5, 7.5);
            }

            edges.Add(new PlanEdgePath(rows[row].Node.Id, rows[effParent[row]].Node.Id, x0, y0, cx, y0, cx, y1, x1, y1, thickness, rows[row].Severity));

            // Helpers between this card and its effective parent sit on the edge midpoint; several are stacked.
            var k = 0;
            for (var h = parent[row]; h >= 0 && isPill[h]; h = parent[h], k++)
            {
                var text = PillText(rows[h]);
                var w = measureText(text) + PillPadding;
                pills.Add(new PlanPill(rows[h].Node.Id, text, cx - w / 2, (y0 + y1) / 2 - PillHeight / 2 + k * (PillHeight + 4), w, PillHeight));
            }
        }

        pills.Sort((a, b) => a.Id.CompareTo(b.Id));
        return new PlanLayoutResult(boxes, pills, edges, Margin * 2 + (maxColumn + 1) * CardWidth + maxColumn * ColumnGap, Margin * 2 + block[0]);
    }

    private static double? RowCount(PlanRowViewModel row) => row.Node.ActualRows ?? row.Node.EstimatedRows;

    private static int ExtraLines(PlanRowViewModel row)
    {
        var lines = row.MisestimateText.Length > 0 ? 1 : 0;
        var warning = false;
        var spill = false;
        foreach (var w in row.Stats.Warnings)
        {
            if (w.Code is PlanMessageCodes.SortSpill or PlanMessageCodes.HashSpill)
            {
                spill = true;
            }
            else if (w.Code != PlanMessageCodes.Misestimate)
            {
                warning = true;
            }
        }

        return Math.Min(2, lines + (warning ? 1 : 0) + (spill ? 1 : 0));
    }

    private static string PillText(PlanRowViewModel row)
    {
        var text = row.Label;
        if (RowCount(row) is { } r)
        {
            text += " " + Abbreviate(r);
        }

        foreach (var p in row.Node.Properties)
        {
            if (p.Name.Contains("Memory", StringComparison.OrdinalIgnoreCase))
            {
                return text + " · " + p.Value;
            }
        }

        return text;
    }

    private static string Abbreviate(double v) => v switch
    {
        >= 1_000_000 => string.Create(CultureInfo.InvariantCulture, $"{v / 1_000_000:0.#} M"),
        >= 1000 => string.Create(CultureInfo.InvariantCulture, $"{Math.Round(v / 1000):0} k"),
        _ => string.Create(CultureInfo.InvariantCulture, $"{Math.Round(v):0}"),
    };
}
