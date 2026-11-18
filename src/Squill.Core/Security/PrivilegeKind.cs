namespace Squill.Core.Security;

/// <summary>An object privilege shown as a column of the privilege matrix.</summary>
public enum PrivilegeKind
{
    /// <summary>Read rows.</summary>
    Select,

    /// <summary>Insert rows.</summary>
    Insert,

    /// <summary>Update rows.</summary>
    Update,

    /// <summary>Delete rows.</summary>
    Delete,

    /// <summary>Truncate the table.</summary>
    Truncate,

    /// <summary>Create foreign keys referencing the table.</summary>
    References,

    /// <summary>Create triggers on the table.</summary>
    Trigger,
}
