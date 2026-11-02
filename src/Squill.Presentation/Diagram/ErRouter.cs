namespace Squill.Presentation.Diagram;

/// <summary>A relation routed as an orthogonal polyline.</summary>
/// <param name="Relation">The routed relation.</param>
/// <param name="Points">Polyline vertices from the child end to the parent end.</param>
/// <param name="FromAnchor">First point, on the child box; where the head draws the child-side marker.</param>
/// <param name="ToAnchor">Last point, on the parent box; where the head draws the parent-side marker.</param>
public sealed record ErEdge(ErRelation Relation, IReadOnlyList<ErPoint> Points, ErPoint FromAnchor, ErPoint ToAnchor);

/// <summary>Deterministic orthogonal routing of the relations of an <see cref="ErModel"/>.</summary>
public static class ErRouter
{
    private const double SpreadStep = 4;

    /// <summary>Computes the polyline of every relation.</summary>
    /// <param name="model">The model whose relations are routed.</param>
    /// <param name="boxes">Entity key to box, as produced by <see cref="ErLayout.Compute"/>.</param>
    /// <param name="options">Sizes and spacing, shared with the layout.</param>
    /// <returns>One edge per relation whose two entities both have a box, in the order of <see cref="ErModel.Relations"/>.</returns>
    /// <remarks>Lines may pass behind other boxes: crossings are not avoided.</remarks>
    public static IReadOnlyList<ErEdge> Route(ErModel model, IReadOnlyDictionary<string, ErBox> boxes, ErLayoutOptions options)
    {
        ArgumentNullException.ThrowIfNull(model);
        ArgumentNullException.ThrowIfNull(boxes);
        ArgumentNullException.ThrowIfNull(options);
        var cmp = StringComparer.OrdinalIgnoreCase;
        List<ErEdge> edges = [];
        Dictionary<(string Key, bool Right, double Y), int> ends = [];
        Dictionary<string, int> loops = new(cmp);

        foreach (var r in model.Relations)
        {
            if (!TryGetBox(boxes, r.FromKey, out var child) || !TryGetBox(boxes, r.ToKey, out var parent))
                continue;

            var startY = RowY(model, r.FromKey, r.FromColumns, child, options);
            var endY = RowY(model, r.ToKey, r.ToColumns, parent, options);
            var self = cmp.Equals(r.FromKey, r.ToKey);

            bool startRight;
            bool endRight;
            if (parent.Right < child.X)
            {
                startRight = false;
                endRight = true;
            }
            else if (child.Right < parent.X)
            {
                startRight = true;
                endRight = false;
            }
            else
            {
                startRight = true;
                endRight = true;
            }

            var startKey = (r.FromKey.ToUpperInvariant(), startRight, startY);
            var endKey = (r.ToKey.ToUpperInvariant(), endRight, endY);
            var startIndex = ends.GetValueOrDefault(startKey);
            ends[startKey] = startIndex + 1;
            var endIndex = ends.GetValueOrDefault(endKey);
            ends[endKey] = endIndex + 1;
            var spread = self ? 0 : SpreadStep * Math.Max(startIndex, endIndex);

            var startX = startRight ? child.Right : child.X;
            var endX = endRight ? parent.Right : parent.X;
            double midX;
            if (startRight == endRight)
            {
                midX = Math.Max(child.Right, parent.Right) + (options.HorizontalGap / 2) + spread;
                if (self)
                {
                    loops.TryGetValue(r.FromKey, out var n);
                    loops[r.FromKey] = n + 1;
                    midX += options.RowHeight * n;
                }
            }
            else
            {
                midX = ((startX + endX) / 2) + spread;
            }

            ErPoint start = new(startX, startY);
            ErPoint end = new(endX, endY);
            edges.Add(new ErEdge(r, [start, new(midX, startY), new(midX, endY), end], start, end));
        }

        return edges;
    }

    private static bool TryGetBox(IReadOnlyDictionary<string, ErBox> boxes, string key, out ErBox box)
    {
        if (boxes.TryGetValue(key, out box))
            return true;
        foreach (var (k, v) in boxes)
        {
            if (string.Equals(k, key, StringComparison.OrdinalIgnoreCase))
            {
                box = v;
                return true;
            }
        }

        return false;
    }

    private static double RowY(ErModel model, string key, IReadOnlyList<string> columns, ErBox box, ErLayoutOptions options)
    {
        var entity = model.Find(key);
        var index = -1;
        if (entity is not null && columns.Count > 0)
        {
            for (var i = 0; i < entity.Columns.Count; i++)
            {
                if (string.Equals(entity.Columns[i].Name, columns[0], StringComparison.OrdinalIgnoreCase))
                {
                    index = i;
                    break;
                }
            }
        }

        return index < 0
            ? box.Y + (options.HeaderHeight / 2)
            : box.Y + options.HeaderHeight + (options.RowHeight * (index + 0.5));
    }
}
