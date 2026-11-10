namespace Squill.Data;

/// <summary>The engine returned a plan document that could not be understood.</summary>
/// <param name="message">What was wrong.</param>
/// <param name="sourceText">The raw engine output, kept so the UI can still show it as text.</param>
/// <param name="inner">The parser failure, if any.</param>
public sealed class PlanParseException(string message, string sourceText, Exception? inner = null) : Exception(message, inner)
{
    /// <summary>Gets the raw engine output.</summary>
    public string SourceText { get; } = sourceText;
}
