namespace Squill.Core;

/// <summary>Kind of a stored routine.</summary>
public enum RoutineKind
{
    /// <summary>A function that returns a value.</summary>
    Function,

    /// <summary>A procedure invoked with <c>CALL</c> or <c>EXEC</c>.</summary>
    Procedure,
}
