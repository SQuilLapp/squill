namespace Squill.Core;

/// <summary>Severity of a diagnostic shown in the editor.</summary>
public enum DiagnosticSeverity
{
    /// <summary>The statement is invalid and cannot run.</summary>
    Error,

    /// <summary>The statement runs but is likely wrong.</summary>
    Warning,

    /// <summary>Neutral information about the statement.</summary>
    Information,

    /// <summary>A low-key suggestion, such as a style improvement.</summary>
    Hint,
}
