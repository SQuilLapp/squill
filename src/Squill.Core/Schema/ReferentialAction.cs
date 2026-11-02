namespace Squill.Core;

/// <summary>Action a foreign key takes when the referenced row is deleted or updated.</summary>
public enum ReferentialAction
{
    /// <summary>Reject the change at the end of the statement.</summary>
    NoAction,

    /// <summary>Reject the change immediately.</summary>
    Restrict,

    /// <summary>Propagate the change to the referencing rows.</summary>
    Cascade,

    /// <summary>Set the referencing columns to NULL.</summary>
    SetNull,

    /// <summary>Set the referencing columns to their default values.</summary>
    SetDefault,
}
