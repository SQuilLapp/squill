using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Media;
using Avalonia.VisualTree;

namespace Squill.App.Avalonia.Tests;

/// <summary>Finds the layout bugs of a too narrow column: a visible text drawn over another one, or cut without an ellipsis.</summary>
internal static class TextOverlap
{
    public static List<string> Find(Visual root)
    {
        var texts = new List<(Visual Visual, Rect Box, string Text)>();
        var problems = new List<string>();
        Collect(root, root, texts, problems);
        for (var i = 0; i < texts.Count; i++)
        {
            for (var j = i + 1; j < texts.Count; j++)
            {
                var (a, b) = (texts[i], texts[j]);
                var overlap = a.Box.Intersect(b.Box);
                if (overlap.Width > 1 && overlap.Height > 1 && !a.Visual.IsVisualAncestorOf(b.Visual) && !b.Visual.IsVisualAncestorOf(a.Visual))
                    problems.Add($"'{a.Text}' {a.Visual.GetType().Name} {Format(a.Box)} overlaps '{b.Text}' {b.Visual.GetType().Name} {Format(b.Box)}");
            }
        }

        return problems;
    }

    private static void Collect(Visual visual, Visual root, List<(Visual, Rect, string)> texts, List<string> problems)
    {
        if (!visual.IsVisible || visual.Opacity == 0)
            return;
        switch (visual)
        {
            // The text of a box scrolls inside it: the box itself is what can spill over a neighbour.
            case TextBox box:
                Add(box, root, new Rect(box.Bounds.Size), box.Text is { Length: > 0 } t ? t : box.PlaceholderText, texts, problems);
                return;
            case TextBlock block:
                Add(block, root, InkBox(block), block.Text, texts, problems);
                return;
        }

        var children = visual.GetVisualChildren().ToList();
        for (var i = 0; i < children.Count; i++)
        {
            // A later sibling that is opaque and covers this one hides it (the designer drawn over the query view).
            var hidden = children.Skip(i + 1).Any(c => c.IsVisible && IsOpaque(c) && c.Bounds.Contains(children[i].Bounds));
            if (!hidden)
                Collect(children[i], root, texts, problems);
        }
    }

    private static bool IsOpaque(Visual visual) => visual switch
    {
        Panel { Background: { } b } when b.Opacity > 0 && b is not ISolidColorBrush { Color.A: 0 } => true,
        Border { Background: { } b } when b.Opacity > 0 && b is not ISolidColorBrush { Color.A: 0 } => true,
        _ => visual.GetVisualChildren().ToList() is [var only] && only.Bounds.Size == visual.Bounds.Size && IsOpaque(only),
    };

    private static void Add(Visual visual, Visual root, Rect local, string? text, List<(Visual, Rect, string)> texts, List<string> problems)
    {
        if (string.IsNullOrWhiteSpace(text) || visual.TransformToVisual(root) is not { } toRoot)
            return;
        var box = local.TransformToAABB(toRoot).Intersect(new Rect(root.Bounds.Size));
        var scrolls = false;
        for (var parent = visual.GetVisualParent(); parent is not null && parent != root; parent = parent.GetVisualParent())
        {
            if (parent.ClipToBounds && parent.TransformToVisual(root) is { } clip)
                box = box.Intersect(new Rect(parent.Bounds.Size).TransformToAABB(clip));
            scrolls |= parent is ScrollContentPresenter p && p.Extent.Width > p.Viewport.Width + 1;
        }

        if (box.Width <= 1 || box.Height <= 1)
            return;
        texts.Add((visual, box, text));
        // Cut: clipped narrower than its glyphs by a block that neither trims nor wraps. Hidden by a sideways scroll is not cut: the user scrolls to it.
        if (!scrolls && visual is TextBlock { TextWrapping: TextWrapping.NoWrap } block && block.TextTrimming == TextTrimming.None && box.Width + 2 < local.Width)
            problems.Add(FormattableString.Invariant($"'{text}' cut to {box.Width:0} of {local.Width:0} px"));
    }

    // Where the glyphs are drawn: a text without trimming or wrapping is drawn past its bounds when the layout gives it too little room.
    private static Rect InkBox(TextBlock block)
    {
        var width = block.TextLayout.WidthIncludingTrailingWhitespace;
        var inner = block.Bounds.Width - block.Padding.Left - block.Padding.Right;
        var x = block.Padding.Left + block.TextAlignment switch
        {
            TextAlignment.Center => (inner - width) / 2,
            TextAlignment.Right or TextAlignment.End => inner - width,
            _ => 0,
        };
        return new Rect(x, 0, width, block.Bounds.Height);
    }

    private static string Format(Rect r) => FormattableString.Invariant($"[{r.X:0},{r.Y:0} {r.Width:0}x{r.Height:0}]");
}
