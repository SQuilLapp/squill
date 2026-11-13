using Squill.Core.Plans;

namespace Squill.Data;

/// <summary>
/// Optional capability of a connection: returns the query plan of one statement. It is a separate interface so that providers
/// without plans stay untouched; callers test <c>connection as IPlanReader</c>.
/// </summary>
/// <remarks>
/// The statement is a single statement already vetted by <c>PlanEligibility</c>. The reader never executes it for
/// <see cref="PlanMode.Estimated"/>; for <see cref="PlanMode.Actual"/> it executes it, and <see cref="QueryPlan.RolledBack"/> tells
/// whether a rolled-back transaction wrapped the run. Cancellation interrupts like a normal run and leaves the connection usable.
/// </remarks>
public interface IPlanReader
{
    /// <summary>Gets a value indicating whether <see cref="PlanMode.Actual"/> plans are available on this engine.</summary>
    bool SupportsActualPlan { get; }

    /// <summary>Gets the plan of a statement.</summary>
    /// <param name="sql">The single statement to explain.</param>
    /// <param name="parameters">The parameters of a normal run of the statement.</param>
    /// <param name="mode">Estimated (not executed) or actual (executed).</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The engine-neutral plan.</returns>
    /// <exception cref="PlanUnavailableException">The engine cannot give a plan for this statement or session.</exception>
    /// <exception cref="PlanParseException">The engine output could not be understood.</exception>
    ValueTask<QueryPlan> ExplainAsync(string sql, IReadOnlyList<QueryParameter> parameters, PlanMode mode, CancellationToken cancellationToken);
}
