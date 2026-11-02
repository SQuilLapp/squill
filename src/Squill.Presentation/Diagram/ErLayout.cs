namespace Squill.Presentation.Diagram;

/// <summary>A point of the diagram plane.</summary>
/// <param name="X">Horizontal coordinate.</param>
/// <param name="Y">Vertical coordinate.</param>
public readonly record struct ErPoint(double X, double Y);

/// <summary>The rectangle of an entity box.</summary>
/// <param name="X">Left edge.</param>
/// <param name="Y">Top edge.</param>
/// <param name="Width">Box width.</param>
/// <param name="Height">Box height.</param>
public readonly record struct ErBox(double X, double Y, double Width, double Height)
{
    /// <summary>Right edge.</summary>
    public double Right => X + Width;

    /// <summary>Bottom edge.</summary>
    public double Bottom => Y + Height;

    /// <summary>Tells whether a point lies inside the box, edges included.</summary>
    /// <param name="p">The point.</param>
    /// <returns>True when inside.</returns>
    public bool Contains(ErPoint p) => p.X >= X && p.X <= Right && p.Y >= Y && p.Y <= Bottom;
}

/// <summary>Sizes and spacing used to lay out the entity boxes.</summary>
/// <param name="MeasureText">Width of a text in the diagram font; supplied by the head's text engine.</param>
/// <param name="HeaderHeight">Height of the entity title bar.</param>
/// <param name="RowHeight">Height of one column row.</param>
/// <param name="Padding">Horizontal padding inside a box.</param>
/// <param name="MinWidth">Smallest box width.</param>
/// <param name="HorizontalGap">Gap between layers.</param>
/// <param name="VerticalGap">Gap between boxes of a layer.</param>
/// <param name="MaxRowWidth">Reserved row width limit; components are never wrapped.</param>
public sealed record ErLayoutOptions(
    Func<string, double> MeasureText,
    double HeaderHeight = 28,
    double RowHeight = 20,
    double Padding = 12,
    double MinWidth = 160,
    double HorizontalGap = 96,
    double VerticalGap = 32,
    double MaxRowWidth = 2400);

/// <summary>Deterministic placement of the entity boxes of an <see cref="ErModel"/>.</summary>
public static class ErLayout
{
    /// <summary>Computes the box of every entity.</summary>
    /// <param name="model">The model to lay out.</param>
    /// <param name="options">Sizes, spacing and the text measuring hook.</param>
    /// <returns>Entity key to box, keys compared case-insensitively; the drawing's top-left is (0, 0).</returns>
    public static IReadOnlyDictionary<string, ErBox> Compute(ErModel model, ErLayoutOptions options)
    {
        ArgumentNullException.ThrowIfNull(model);
        ArgumentNullException.ThrowIfNull(options);
        var cmp = StringComparer.OrdinalIgnoreCase;
        Dictionary<string, ErBox> result = new(cmp);

        var sizes = new Dictionary<string, (double W, double H)>(cmp);
        foreach (var e in model.Entities)
        {
            var text = options.MeasureText(e.Name);
            foreach (var c in e.Columns)
                text = Math.Max(text, options.MeasureText(c.Name + "  " + c.DataType));
            var w = Math.Ceiling(Math.Max(options.MinWidth, (2 * options.Padding) + text));
            sizes[e.Key] = (w, options.HeaderHeight + (options.RowHeight * e.Columns.Count));
        }

        var keys = sizes.Keys.Order(cmp).ToList();
        var parents = keys.ToDictionary(k => k, _ => new SortedSet<string>(cmp), cmp);
        var children = keys.ToDictionary(k => k, _ => new SortedSet<string>(cmp), cmp);
        foreach (var r in model.Relations)
        {
            if (cmp.Equals(r.FromKey, r.ToKey) || !sizes.ContainsKey(r.FromKey) || !sizes.ContainsKey(r.ToKey))
                continue;
            parents[r.FromKey].Add(r.ToKey);
            children[r.ToKey].Add(r.FromKey);
        }

        // Connected components, discovered in key order.
        HashSet<string> seen = new(cmp);
        List<List<string>> components = [];
        List<string> isolated = [];
        foreach (var k in keys)
        {
            if (!seen.Add(k))
                continue;
            List<string> comp = [k];
            Queue<string> queue = new([k]);
            while (queue.Count > 0)
            {
                var cur = queue.Dequeue();
                foreach (var n in parents[cur].Concat(children[cur]))
                {
                    if (seen.Add(n))
                    {
                        comp.Add(n);
                        queue.Enqueue(n);
                    }
                }
            }

            if (comp.Count == 1)
                isolated.Add(k);
            else
                components.Add([.. comp.Order(cmp)]);
        }

        var top = 0.0;
        foreach (var comp in components)
            top += PlaceComponent(comp, parents, children, sizes, options, top, result) + (2 * options.VerticalGap);

        if (isolated.Count > 0)
        {
            var cols = (int)Math.Ceiling(Math.Sqrt(isolated.Count));
            var cellW = isolated.Max(k => sizes[k].W) + options.HorizontalGap;
            var cellH = isolated.Max(k => sizes[k].H) + options.VerticalGap;
            for (var i = 0; i < isolated.Count; i++)
            {
                var (w, h) = sizes[isolated[i]];
                result[isolated[i]] = new ErBox((i % cols) * cellW, top + ((i / cols) * cellH), w, h);
            }
        }

        return result;
    }

    // Places one component starting at y = top and returns its height.
    private static double PlaceComponent(
        List<string> comp,
        Dictionary<string, SortedSet<string>> parents,
        Dictionary<string, SortedSet<string>> children,
        Dictionary<string, (double W, double H)> sizes,
        ErLayoutOptions options,
        double top,
        Dictionary<string, ErBox> result)
    {
        var cmp = StringComparer.OrdinalIgnoreCase;
        var kept = BreakCycles(comp, parents);

        Dictionary<string, int> layerOf = new(cmp);
        int LayerOf(string k)
        {
            if (layerOf.TryGetValue(k, out var l))
                return l;
            l = kept[k].Count == 0 ? 0 : kept[k].Max(LayerOf) + 1;
            layerOf[k] = l;
            return l;
        }

        foreach (var k in comp)
            LayerOf(k);

        var layers = Enumerable.Range(0, layerOf.Values.Max() + 1)
            .Select(i => comp.Where(k => layerOf[k] == i).ToList())
            .ToList();

        // Barycenter sweeps: down, up, down, up. Only the adjacent layer is consulted.
        for (var sweep = 0; sweep < 4; sweep++)
        {
            var down = sweep % 2 == 0;
            for (var step = 1; step < layers.Count; step++)
            {
                var i = down ? step : layers.Count - 1 - step;
                var adj = layers[down ? i - 1 : i + 1];
                var pos = adj.Select((k, p) => (k, p)).ToDictionary(t => t.k, t => t.p, cmp);
                var layer = layers[i];
                var bary = new Dictionary<string, double>(cmp);
                foreach (var k in layer)
                {
                    var ns = parents[k].Concat(children[k]).Where(pos.ContainsKey).ToList();
                    if (ns.Count > 0)
                        bary[k] = ns.Average(n => pos[n]);
                }

                var slots = Enumerable.Range(0, layer.Count).Where(s => bary.ContainsKey(layer[s])).ToList();
                var sorted = slots.Select(s => layer[s])
                    .OrderBy(k => bary[k]).ThenBy(k => layer.IndexOf(k)).ToList();
                for (var s = 0; s < slots.Count; s++)
                    layer[slots[s]] = sorted[s];
            }
        }

        double Height(List<string> layer) => layer.Sum(k => sizes[k].H) + (options.VerticalGap * (layer.Count - 1));
        var tallest = layers.Max(Height);
        var x = 0.0;
        foreach (var layer in layers)
        {
            var y = top + ((tallest - Height(layer)) / 2);
            foreach (var k in layer)
            {
                var (w, h) = sizes[k];
                result[k] = new ErBox(x, y, w, h);
                y += h + options.VerticalGap;
            }

            x += layer.Max(k => sizes[k].W) + options.HorizontalGap;
        }

        return tallest;
    }

    // Parent edges of the component with DFS back edges removed, visiting entities in key order.
    private static Dictionary<string, List<string>> BreakCycles(List<string> comp, Dictionary<string, SortedSet<string>> parents)
    {
        var cmp = StringComparer.OrdinalIgnoreCase;
        Dictionary<string, List<string>> kept = comp.ToDictionary(k => k, _ => new List<string>(), cmp);
        Dictionary<string, int> state = new(cmp); // 1 = on stack, 2 = done
        void Visit(string k)
        {
            state[k] = 1;
            foreach (var p in parents[k])
            {
                if (state.TryGetValue(p, out var s) && s == 1)
                    continue;
                kept[k].Add(p);
                if (!state.ContainsKey(p))
                    Visit(p);
            }

            state[k] = 2;
        }

        foreach (var k in comp)
        {
            if (!state.ContainsKey(k))
                Visit(k);
        }

        return kept;
    }
}
