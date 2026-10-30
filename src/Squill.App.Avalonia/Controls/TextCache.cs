using System.Globalization;
using Avalonia;
using Avalonia.Media;

namespace Squill.App.Avalonia.Controls;

/// <summary>Least-recently-used cache of <see cref="FormattedText"/> keyed by text and brush.</summary>
internal sealed class TextCache(int capacity)
{
    private readonly Dictionary<(string Text, IBrush Brush), LinkedListNode<(string Text, IBrush Brush, FormattedText Value)>> _map = [];
    private readonly LinkedList<(string Text, IBrush Brush, FormattedText Value)> _order = new();

    /// <summary>Gets the number of cached entries.</summary>
    public int Count => _map.Count;

    /// <summary>Drops every entry (typeface or size changed).</summary>
    public void Clear()
    {
        _map.Clear();
        _order.Clear();
    }

    /// <summary>Gets the formatted text for a string, creating it on a miss.</summary>
    public FormattedText Get(string text, IBrush brush, Typeface typeface, double size)
    {
        if (_map.TryGetValue((text, brush), out var node))
        {
            _order.Remove(node);
            _order.AddFirst(node);
            return node.Value.Value;
        }

        var formatted = new FormattedText(text, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, typeface, size, brush);
        _map[(text, brush)] = _order.AddFirst((text, brush, formatted));
        if (_map.Count > capacity)
        {
            var last = _order.Last!;
            _order.RemoveLast();
            _map.Remove((last.Value.Text, last.Value.Brush));
        }

        return formatted;
    }
}
