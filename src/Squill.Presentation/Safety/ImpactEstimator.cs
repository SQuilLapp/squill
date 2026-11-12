using Squill.Core;
using Squill.Core.Plans;
using Squill.Core.Transactions;
using Squill.Data;
using Squill.Presentation.Sessions;
using Squill.Sql.Plans;
using Squill.Sql.Transactions;

namespace Squill.Presentation.Safety;

/// <summary>The number of rows the planner expects a modifying statement to touch.</summary>
/// <param name="Rows">The estimated rows, never negative; an estimate, not a count.</param>
public sealed record ImpactEstimate(long Rows);

/// <summary>Asks the planner, without executing anything, how many rows an <c>UPDATE</c>, <c>DELETE</c> or <c>INSERT ... SELECT</c> will touch.</summary>
public static class ImpactEstimator
{
    private static readonly TimeSpan Cap = TimeSpan.FromSeconds(3);

    /// <summary>Estimates the rows a statement will touch.</summary>
    /// <param name="session">The session the statement would run on.</param>
    /// <param name="statement">One statement.</param>
    /// <param name="parameters">The parameters of a normal run of the statement.</param>
    /// <param name="ct">Cancels the estimate; the cancellation propagates.</param>
    /// <returns>The estimate, or <see langword="null"/> when the engine cannot tell cheaply, refuses, or takes more than 3 seconds.</returns>
    public static ValueTask<ImpactEstimate?> EstimateAsync(ConnectionSession session, string statement, IReadOnlyList<QueryParameter> parameters, CancellationToken ct) =>
        EstimateAsync(session, statement, parameters, Cap, ct);

    internal static async ValueTask<ImpactEstimate?> EstimateAsync(ConnectionSession session, string statement, IReadOnlyList<QueryParameter> parameters, TimeSpan cap, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(statement);
        ArgumentNullException.ThrowIfNull(parameters);
        var dialect = session.Profile.Dialect;
        if (dialect == DatabaseDialect.Sqlite || session.Transaction.State == TransactionState.Failed || !session.PlanSupport.CanExplain)
            return null;
        var traits = StatementClassifier.Classify(statement, dialect);
        // shortcut: any SELECT word counts, even inside a string; the cost is one harmless EXPLAIN.
        var estimable = traits.Kind is StatementKind.Update or StatementKind.Delete
            || (traits.Kind == StatementKind.Insert && statement.Contains("SELECT", StringComparison.OrdinalIgnoreCase));
        if (!estimable || !PlanEligibility.Evaluate(statement, dialect).CanExplain)
            return null;

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(cap);
        try
        {
            var plan = await session.ExplainAsync(statement, parameters, PlanMode.Estimated, cts.Token);
            return FirstEstimate(plan.Root) is { } rows ? new ImpactEstimate(Math.Max(0, (long)Math.Round(rows))) : null;
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception)
        {
            return null;
        }
    }

    private static double? FirstEstimate(PlanNode node)
    {
        if (!IsModifying(node.Operation) && node.EstimatedRows is { } rows)
            return rows;
        foreach (var child in node.Children)
        {
            if (FirstEstimate(child) is { } found)
                return found;
        }

        return null;
    }

    // The wrapper operators end in the verb ("Delete", "Clustered Index Update", "Table Insert"); PostgreSQL names them "ModifyTable".
    private static bool IsModifying(string operation)
    {
        if (operation.Equals("ModifyTable", StringComparison.OrdinalIgnoreCase))
            return true;
        var words = operation.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return words.Length > 0 && words[^1] is var last
            && (last.Equals("Insert", StringComparison.OrdinalIgnoreCase) || last.Equals("Update", StringComparison.OrdinalIgnoreCase)
                || last.Equals("Delete", StringComparison.OrdinalIgnoreCase) || last.Equals("Merge", StringComparison.OrdinalIgnoreCase));
    }
}
