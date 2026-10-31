namespace Squill.Presentation.Results;

/// <summary>Fixed-capacity least-recently-used cache, for text layouts that are expensive to build and drawn every frame.</summary>
/// <typeparam name="TKey">The key type.</typeparam>
/// <typeparam name="TValue">The cached value type.</typeparam>
/// <param name="capacity">Maximum number of entries kept.</param>
/// <param name="onRemoved">Called for every value that leaves the cache (evicted, replaced or cleared), for example to dispose it.</param>
public sealed class LruCache<TKey, TValue>(int capacity, Action<TValue>? onRemoved = null)
    where TKey : notnull
{
    private readonly Dictionary<TKey, LinkedListNode<(TKey Key, TValue Value)>> _map = [];
    private readonly LinkedList<(TKey Key, TValue Value)> _order = new();

    /// <summary>Gets the number of cached entries.</summary>
    public int Count => _map.Count;

    /// <summary>Looks a key up and marks it most recently used.</summary>
    /// <param name="key">The key.</param>
    /// <param name="value">The cached value when found.</param>
    /// <returns><c>true</c> on a hit.</returns>
    public bool TryGet(TKey key, out TValue value)
    {
        if (_map.TryGetValue(key, out var node))
        {
            _order.Remove(node);
            _order.AddFirst(node);
            value = node.Value.Value;
            return true;
        }

        value = default!;
        return false;
    }

    /// <summary>Adds or replaces an entry, evicting the least recently used one when the cache is full.</summary>
    /// <param name="key">The key.</param>
    /// <param name="value">The value to cache.</param>
    public void Add(TKey key, TValue value)
    {
        if (_map.Remove(key, out var old))
        {
            _order.Remove(old);
            onRemoved?.Invoke(old.Value.Value);
        }

        _map[key] = _order.AddFirst((key, value));
        if (_map.Count > capacity)
        {
            var last = _order.Last!;
            _order.RemoveLast();
            _map.Remove(last.Value.Key);
            onRemoved?.Invoke(last.Value.Value);
        }
    }

    /// <summary>Drops every entry.</summary>
    public void Clear()
    {
        if (onRemoved is not null)
        {
            foreach (var entry in _order)
            {
                onRemoved(entry.Value);
            }
        }

        _map.Clear();
        _order.Clear();
    }
}
