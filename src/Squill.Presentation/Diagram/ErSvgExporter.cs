using System.Globalization;
using System.Text;

namespace Squill.Presentation.Diagram;

/// <summary>Renders an ER diagram as a standalone SVG document.</summary>
public static class ErSvgExporter
{
    private const double Margin = 16;
    private const double ColumnFontSize = 12;
    private const double HeaderFontSize = 13;
    private const double MarkerFontSize = 9;
    private const double CrowLength = 10;
    private const double CrowSpread = 6;

    // Light palette, values of tokens/squill.tokens.json (light column).
    private const string LightRaised = "#FFFFFF";
    private const string LightHeader = "#F1F2F5";
    private const string LightBorder = "#B8BBC6";
    private const string LightText = "#16171C";
    private const string LightMuted = "#626574";
    private const string LightAccent = "#5B4CD6";

    // Dark palette, values of tokens/squill.tokens.json (dark column).
    private const string DarkRaised = "#1A1B20";
    private const string DarkHeader = "#0F1013";
    private const string DarkBorder = "#454852";
    private const string DarkText = "#E8E9EE";
    private const string DarkMuted = "#878A97";
    private const string DarkAccent = "#9488F5";

    /// <summary>Draws the diagram.</summary>
    /// <param name="model">Entities to draw.</param>
    /// <param name="boxes">Box of every entity, keyed by entity key.</param>
    /// <param name="edges">Routed relations.</param>
    /// <param name="options">Sizes used to lay the boxes out; row and header heights place the texts.</param>
    /// <param name="dark">Whether to use the dark palette.</param>
    /// <returns>The SVG text; the same input always gives the same string.</returns>
    public static string Render(
        ErModel model,
        IReadOnlyDictionary<string, ErBox> boxes,
        IReadOnlyList<ErEdge> edges,
        ErLayoutOptions options,
        bool dark)
    {
        ArgumentNullException.ThrowIfNull(model);
        ArgumentNullException.ThrowIfNull(boxes);
        ArgumentNullException.ThrowIfNull(edges);
        ArgumentNullException.ThrowIfNull(options);
        var (raised, header, border, text, muted, accent) = dark
            ? (DarkRaised, DarkHeader, DarkBorder, DarkText, DarkMuted, DarkAccent)
            : (LightRaised, LightHeader, LightBorder, LightText, LightMuted, LightAccent);

        double minX = 0, minY = 0, maxX = 0, maxY = 0;
        var any = false;
        void Include(double x, double y)
        {
            if (!any)
            {
                (minX, minY, maxX, maxY, any) = (x, y, x, y, true);
            }
            else
            {
                (minX, minY, maxX, maxY) = (Math.Min(minX, x), Math.Min(minY, y), Math.Max(maxX, x), Math.Max(maxY, y));
            }
        }

        foreach (var e in model.Entities)
        {
            if (boxes.TryGetValue(e.Key, out var b))
            {
                Include(b.X, b.Y);
                Include(b.Right, b.Bottom);
            }
        }

        foreach (var edge in edges)
        {
            foreach (var p in edge.Points)
                Include(p.X, p.Y);
        }

        var width = maxX - minX + (2 * Margin);
        var height = maxY - minY + (2 * Margin);
        var sb = new StringBuilder();
        sb.Append("<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"")
            .Append(N(minX - Margin)).Append(' ').Append(N(minY - Margin)).Append(' ').Append(N(width)).Append(' ').Append(N(height))
            .Append("\" width=\"").Append(N(width)).Append("\" height=\"").Append(N(height))
            .Append("\" font-family=\"Segoe UI, Inter, sans-serif\">\n");

        foreach (var edge in edges)
        {
            if (edge.Points.Count < 2)
                continue;
            sb.Append("<polyline fill=\"none\" stroke=\"").Append(accent).Append("\" stroke-width=\"1.5\"");
            if (edge.Relation.IsOptional)
                sb.Append(" stroke-dasharray=\"6 4\"");
            sb.Append(" points=\"").AppendJoin(' ', edge.Points.Select(p => N(p.X) + "," + N(p.Y))).Append("\"/>\n");
            var many = edge.Relation.Cardinality == ErCardinality.ManyToOne;
            Marker(sb, edge.FromAnchor, edge.Points[0], edge.Points[1], many, accent);
            Marker(sb, edge.ToAnchor, edge.Points[^1], edge.Points[^2], false, accent);
        }

        foreach (var e in model.Entities)
        {
            if (!boxes.TryGetValue(e.Key, out var b))
                continue;
            sb.Append("<rect x=\"").Append(N(b.X)).Append("\" y=\"").Append(N(b.Y)).Append("\" width=\"").Append(N(b.Width))
                .Append("\" height=\"").Append(N(b.Height)).Append("\" fill=\"").Append(raised).Append("\" stroke=\"").Append(border).Append("\"/>\n");
            sb.Append("<rect x=\"").Append(N(b.X)).Append("\" y=\"").Append(N(b.Y)).Append("\" width=\"").Append(N(b.Width))
                .Append("\" height=\"").Append(N(options.HeaderHeight)).Append("\" fill=\"").Append(header).Append("\" stroke=\"").Append(border).Append("\"/>\n");
            sb.Append("<text x=\"").Append(N(b.X + options.Padding)).Append("\" y=\"").Append(N(b.Y + (options.HeaderHeight / 2)))
                .Append("\" dominant-baseline=\"central\" font-size=\"").Append(N(HeaderFontSize)).Append("\" font-weight=\"600\" fill=\"").Append(text).Append("\">")
                .Append(Esc(e.Name)).Append("</text>\n");
            for (var i = 0; i < e.Columns.Count; i++)
            {
                var c = e.Columns[i];
                var y = b.Y + options.HeaderHeight + (options.RowHeight * (i + 0.5));
                sb.Append("<text x=\"").Append(N(b.X + options.Padding)).Append("\" y=\"").Append(N(y))
                    .Append("\" dominant-baseline=\"central\" font-size=\"").Append(N(ColumnFontSize)).Append("\" fill=\"").Append(text).Append("\">").Append(Esc(c.Name));
                // shortcut: markers are not part of the measured width, a tight box can overlap the type; widen MinWidth if it shows.
                if (c.IsPrimaryKey)
                    sb.Append("<tspan dx=\"4\" font-size=\"").Append(N(MarkerFontSize)).Append("\" font-weight=\"700\" fill=\"").Append(accent).Append("\">PK</tspan>");
                if (c.IsForeignKey)
                    sb.Append("<tspan dx=\"4\" font-size=\"").Append(N(MarkerFontSize)).Append("\" font-weight=\"700\" fill=\"").Append(muted).Append("\">FK</tspan>");
                sb.Append("</text>\n");
                sb.Append("<text x=\"").Append(N(b.Right - options.Padding)).Append("\" y=\"").Append(N(y))
                    .Append("\" text-anchor=\"end\" dominant-baseline=\"central\" font-size=\"").Append(N(ColumnFontSize)).Append("\" fill=\"").Append(muted).Append("\">")
                    .Append(Esc(c.DataType)).Append("</text>\n");
            }
        }

        return sb.Append("</svg>\n").ToString();
    }

    // Draws a crow's foot (many) or a bar (one) at an anchor, oriented along the first segment leaving it.
    private static void Marker(StringBuilder sb, ErPoint anchor, ErPoint from, ErPoint next, bool many, string color)
    {
        double dx = next.X - from.X, dy = next.Y - from.Y;
        var len = Math.Sqrt((dx * dx) + (dy * dy));
        (dx, dy) = len < 1e-9 ? (1, 0) : (dx / len, dy / len);
        double px = -dy, py = dx;
        sb.Append("<path fill=\"none\" stroke=\"").Append(color).Append("\" stroke-width=\"1.5\" d=\"");
        if (many)
        {
            double tx = anchor.X + (dx * CrowLength), ty = anchor.Y + (dy * CrowLength);
            sb.Append('M').Append(N(tx)).Append(' ').Append(N(ty)).Append('L').Append(N(anchor.X + (px * CrowSpread))).Append(' ').Append(N(anchor.Y + (py * CrowSpread)))
                .Append('M').Append(N(tx)).Append(' ').Append(N(ty)).Append('L').Append(N(anchor.X - (px * CrowSpread))).Append(' ').Append(N(anchor.Y - (py * CrowSpread)));
        }
        else
        {
            double bx = anchor.X + (dx * 8), by = anchor.Y + (dy * 8);
            sb.Append('M').Append(N(bx + (px * CrowSpread))).Append(' ').Append(N(by + (py * CrowSpread)))
                .Append('L').Append(N(bx - (px * CrowSpread))).Append(' ').Append(N(by - (py * CrowSpread)));
        }

        sb.Append("\"/>\n");
    }

    private static string N(double v)
    {
        var s = Math.Round(v, 2).ToString("0.##", CultureInfo.InvariantCulture);
        return s == "-0" ? "0" : s;
    }

    private static string Esc(string s)
    {
        var sb = new StringBuilder(s.Length);
        foreach (var ch in s)
        {
            sb.Append(ch switch
            {
                '&' => "&amp;",
                '<' => "&lt;",
                '>' => "&gt;",
                '"' => "&quot;",
                '\'' => "&apos;",
                < ' ' and not ('\t' or '\n' or '\r') => "",
                _ => ch.ToString(),
            });
        }

        return sb.ToString();
    }
}
