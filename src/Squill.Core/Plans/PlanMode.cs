namespace Squill.Core.Plans;

/// <summary>Whether a plan was only estimated or produced by executing the statement.</summary>
public enum PlanMode
{
    /// <summary>The statement was not executed; figures are the optimizer's estimates.</summary>
    Estimated,
    /// <summary>The statement was executed and the plan carries measured figures.</summary>
    Actual,
}
