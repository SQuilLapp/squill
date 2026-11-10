namespace Squill.Core.Plans;

/// <summary>An engine-neutral query execution plan.</summary>
/// <param name="Dialect">Engine that produced the plan.</param>
/// <param name="Mode">Estimated or actual.</param>
/// <param name="Statement">The explained SQL statement.</param>
/// <param name="Root">Root operator.</param>
/// <param name="Warnings">Plan-level warnings.</param>
/// <param name="Suggestions">Engine-reported index suggestions.</param>
/// <param name="PlanningTimeMs">Planning time in milliseconds, if reported.</param>
/// <param name="ExecutionTimeMs">Execution time in milliseconds, if reported.</param>
/// <param name="BuffersReadBytes">Bytes read from buffers or disk, if reported.</param>
/// <param name="RolledBack">True only when a wrapper transaction was used and rolled back.</param>
/// <param name="SourceFormat">Format of <paramref name="SourceText"/>.</param>
/// <param name="SourceText">Raw engine output, shown in the text view.</param>
public sealed record QueryPlan(
    DatabaseDialect Dialect,
    PlanMode Mode,
    string Statement,
    PlanNode Root,
    IReadOnlyList<PlanWarning> Warnings,
    IReadOnlyList<PlanSuggestion> Suggestions,
    double? PlanningTimeMs,
    double? ExecutionTimeMs,
    long? BuffersReadBytes,
    bool RolledBack,
    PlanSourceFormat SourceFormat,
    string SourceText)
{
    /// <summary>Enumerates every node in pre-order (a node before its children).</summary>
    /// <returns>All nodes of the plan.</returns>
    public IEnumerable<PlanNode> Flatten()
    {
        var stack = new Stack<PlanNode>();
        stack.Push(Root);
        while (stack.Count > 0)
        {
            var node = stack.Pop();
            yield return node;
            for (var i = node.Children.Count - 1; i >= 0; i--)
            {
                stack.Push(node.Children[i]);
            }
        }
    }

    /// <summary>Finds a node by its <see cref="PlanNode.Id"/>.</summary>
    /// <param name="id">Node id.</param>
    /// <returns>The node, or <see langword="null"/> when no node has that id.</returns>
    public PlanNode? Find(int id) => Flatten().FirstOrDefault(n => n.Id == id);
}
