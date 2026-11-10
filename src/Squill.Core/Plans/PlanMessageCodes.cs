namespace Squill.Core.Plans;

/// <summary>Stable identifiers of the plan warnings and of the reasons a plan is unavailable; the UI maps each to a translated text.</summary>
public static class PlanMessageCodes
{
    /// <summary>Actual rows differ from the estimate by a factor of 10 or more; args: factor, direction (<c>under</c> or <c>over</c>).</summary>
    public const string Misestimate = "plan.misestimate";

    /// <summary>A large table is scanned entirely with no limit above it.</summary>
    public const string FullScan = "plan.fullscan";

    /// <summary>A sort spilled to disk.</summary>
    public const string SortSpill = "plan.sortspill";

    /// <summary>A hash operation spilled to disk.</summary>
    public const string HashSpill = "plan.hashspill";

    /// <summary>The engine reports a missing index.</summary>
    public const string MissingIndex = "plan.missingindex";

    /// <summary>An implicit conversion prevents an index seek.</summary>
    public const string ImplicitConversion = "plan.implicitconversion";

    /// <summary>A table is scanned without any index.</summary>
    public const string NoIndex = "plan.noindex";

    /// <summary>Statistics are missing for a table or column.</summary>
    public const string NoStats = "plan.nostats";

    /// <summary>A join has no join predicate.</summary>
    public const string NoJoinPredicate = "plan.nojoinpredicate";

    /// <summary>The statement is empty.</summary>
    public const string ReasonEmpty = "plan.reason.empty";

    /// <summary>The statement already is an EXPLAIN statement.</summary>
    public const string ReasonAlreadyExplain = "plan.reason.alreadyexplain";

    /// <summary>The statement kind cannot be explained.</summary>
    public const string ReasonNotExplainable = "plan.reason.notexplainable";

    /// <summary>The statement writes data and therefore cannot be analyzed (executed).</summary>
    public const string ReasonWrites = "plan.reason.writes";

    /// <summary>The engine offers no actual plan.</summary>
    public const string ReasonNoAnalyze = "plan.reason.noanalyze";
}
