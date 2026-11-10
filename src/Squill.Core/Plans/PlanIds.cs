namespace Squill.Core.Plans;

/// <summary>Numbers plan nodes so parsers can build nodes with <c>Id = 0</c> and finish with one call.</summary>
public static class PlanIds
{
    /// <summary>Renumbers a tree in pre-order (root = 0); the input is not modified.</summary>
    /// <param name="root">Root of the tree to renumber.</param>
    /// <returns>A rebuilt tree whose ids are 0..n-1 in pre-order.</returns>
    public static PlanNode Assign(PlanNode root)
    {
        ArgumentNullException.ThrowIfNull(root);
        var next = 0;
        return Rebuild(root, ref next);
    }

    private static PlanNode Rebuild(PlanNode node, ref int next)
    {
        var id = next++;
        var children = new PlanNode[node.Children.Count];
        for (var i = 0; i < children.Length; i++)
        {
            children[i] = Rebuild(node.Children[i], ref next);
        }

        return node with { Id = id, Children = children };
    }
}
