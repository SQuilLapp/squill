namespace Squill.Presentation.Plans;

/// <summary>Which representation of the plan the screen shows.</summary>
public enum PlanViewMode
{
    /// <summary>The table only, full height, with the details panel.</summary>
    Tree,

    /// <summary>The graph canvas above the table.</summary>
    Graph,

    /// <summary>The raw engine output.</summary>
    Text,
}
