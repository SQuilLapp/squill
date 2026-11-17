using Squill.Core.Activity;

namespace Squill.Presentation.Activity;

/// <summary>One session of a blocking chain with the sessions it blocks.</summary>
/// <param name="SessionId">Id of the session.</param>
/// <param name="Children">The sessions this one blocks directly, in id order.</param>
/// <param name="BlockedCount">How many distinct sessions wait behind this one, directly or not.</param>
public sealed record BlockingChainNode(string SessionId, IReadOnlyList<BlockingChainNode> Children, int BlockedCount);

/// <summary>Turns the flat "who blocks whom" edges into one tree per root blocker.</summary>
public static class BlockingChains
{
    /// <summary>Builds the chains.</summary>
    /// <param name="edges">The blocking relations of a snapshot.</param>
    /// <returns>One tree per session that blocks others without being blocked itself, in id order; a cycle (a deadlock the server has not resolved yet) is broken at its smallest id, and no session appears twice on one path.</returns>
    public static IReadOnlyList<BlockingChainNode> Build(IReadOnlyList<BlockingEdge> edges)
    {
        ArgumentNullException.ThrowIfNull(edges);
        var children = edges
            .Where(e => e.BlockerId != e.BlockedId)
            .GroupBy(e => e.BlockerId)
            .ToDictionary(g => g.Key, g => g.Select(e => e.BlockedId).Distinct().Order(StringComparer.Ordinal).ToList());
        var blocked = children.Values.SelectMany(x => x).ToHashSet();
        var roots = children.Keys.Where(k => !blocked.Contains(k)).Order(StringComparer.Ordinal).ToList();
        var covered = new HashSet<string>();
        List<BlockingChainNode> result = [];
        foreach (var root in roots)
            result.Add(Node(root, children, [], covered));
        // Whatever no root reaches is a cycle: start at its smallest id.
        foreach (var key in children.Keys.Order(StringComparer.Ordinal))
            if (!covered.Contains(key))
                result.Add(Node(key, children, [], covered));
        return result;
    }

    private static BlockingChainNode Node(string id, Dictionary<string, List<string>> children, HashSet<string> path, HashSet<string> covered)
    {
        covered.Add(id);
        path.Add(id);
        List<BlockingChainNode> kids = [];
        if (children.TryGetValue(id, out var next))
            foreach (var child in next.Where(c => !path.Contains(c)))
                kids.Add(Node(child, children, path, covered));
        path.Remove(id);
        var count = new HashSet<string>();
        foreach (var kid in kids)
            Collect(kid, count);
        count.Remove(id);
        return new BlockingChainNode(id, kids, count.Count);
    }

    private static void Collect(BlockingChainNode node, HashSet<string> into)
    {
        into.Add(node.SessionId);
        foreach (var child in node.Children)
            Collect(child, into);
    }
}
