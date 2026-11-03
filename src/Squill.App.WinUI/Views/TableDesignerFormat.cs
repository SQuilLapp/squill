using Squill.Core.Design;

namespace Squill.App.WinUI.Views;

/// <summary>Choices of the table designer view that do not need a UI thread.</summary>
internal static class TableDesignerFormat
{
    /// <summary>Tells whether an issue is drawn with the danger badge.</summary>
    /// <param name="severity">The severity of the issue.</param>
    /// <returns><see langword="true"/> for an error, <see langword="false"/> for a warning.</returns>
    public static bool IsError(DesignIssueSeverity severity) => severity == DesignIssueSeverity.Error;

    /// <summary>Tells whether an issue is drawn with the warning badge.</summary>
    /// <param name="severity">The severity of the issue.</param>
    /// <returns><see langword="true"/> for anything that is not an error.</returns>
    public static bool IsWarning(DesignIssueSeverity severity) => !IsError(severity);

    /// <summary>Tells whether the draft state line is drawn.</summary>
    /// <param name="draftText">The draft state text of the view-model.</param>
    /// <returns><see langword="true"/> when there is something to say.</returns>
    public static bool HasDraft(string? draftText) => !string.IsNullOrEmpty(draftText);
}
