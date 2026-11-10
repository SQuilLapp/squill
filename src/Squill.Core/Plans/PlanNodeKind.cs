namespace Squill.Core.Plans;

/// <summary>Coarse operator category of a plan node; it drives the icon, never the layout.</summary>
public enum PlanNodeKind
{
    /// <summary>Full table scan.</summary>
    TableScan,
    /// <summary>Full or range index scan.</summary>
    IndexScan,
    /// <summary>Index seek or lookup.</summary>
    IndexSeek,
    /// <summary>Join of two inputs.</summary>
    Join,
    /// <summary>Grouping or aggregation.</summary>
    Aggregate,
    /// <summary>Sort.</summary>
    Sort,
    /// <summary>Row limit.</summary>
    Limit,
    /// <summary>Row filter.</summary>
    Filter,
    /// <summary>Materialization of an intermediate result.</summary>
    Materialize,
    /// <summary>Helper node attached to its parent (for example a PostgreSQL hash build).</summary>
    Helper,
    /// <summary>Set union or append.</summary>
    Union,
    /// <summary>Subquery or CTE evaluation.</summary>
    Subquery,
    /// <summary>Parallelism operator (gather, exchange).</summary>
    Parallel,
    /// <summary>Final result or root operator.</summary>
    Result,
    /// <summary>Anything not classified above.</summary>
    Other,
}
