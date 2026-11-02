namespace Squill.Sql.Editing;

/// <summary>An INSERT plus the optional query that reads back the key the database generated.</summary>
/// <param name="Insert">The insert; where the engine supports it, its own result holds the generated key.</param>
/// <param name="KeyQuery">Follow-up query returning the key (MySQL), or null when there is none to run.</param>
public sealed record InsertPlan(DmlStatement Insert, DmlStatement? KeyQuery);
