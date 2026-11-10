namespace Squill.Core.Plans;

/// <summary>How prominently a plan node is flagged on the screen.</summary>
public enum PlanSeverity
{
    /// <summary>Nothing to flag.</summary>
    Normal,
    /// <summary>Relative cost of 30 % or more, or the node has a warning.</summary>
    Alert,
    /// <summary>Relative cost of 60 % or more.</summary>
    Critical,
}
