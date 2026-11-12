using Squill.Presentation.Query;
using Squill.Presentation.Safety;

namespace Squill.Presentation;

/// <summary>Modal dialogs that view-models request without knowing the UI framework.</summary>
public interface IDialogService
{
    /// <summary>Asks the user to confirm an action.</summary>
    /// <param name="title">Dialog title.</param>
    /// <param name="message">Question shown to the user.</param>
    /// <returns><see langword="true"/> when the user confirmed.</returns>
    ValueTask<bool> ConfirmAsync(string title, string message);

    /// <summary>Asks the user for a line of text.</summary>
    /// <param name="title">Dialog title.</param>
    /// <param name="message">Prompt shown to the user.</param>
    /// <param name="initial">Initial value of the input, if any.</param>
    /// <returns>The entered text, or <see langword="null"/> when cancelled.</returns>
    ValueTask<string?> PromptAsync(string title, string message, string? initial);

    /// <summary>Asks the user where to save a file.</summary>
    /// <param name="suggestedName">Suggested file name without extension.</param>
    /// <param name="extension">File extension without the leading dot.</param>
    /// <returns>The chosen path, or <see langword="null"/> when cancelled.</returns>
    ValueTask<string?> PickSaveFileAsync(string suggestedName, string extension);

    /// <summary>Shows an error message.</summary>
    /// <param name="title">Dialog title.</param>
    /// <param name="message">Error text.</param>
    ValueTask ShowErrorAsync(string title, string message);

    /// <summary>Asks the user for the values of named statement parameters.</summary>
    /// <param name="names">Parameter names without sigil.</param>
    /// <param name="defaults">Values entered the last time, to offer as defaults; may be empty.</param>
    /// <returns>One value per name, or <see langword="null"/> when cancelled.</returns>
    ValueTask<IReadOnlyList<ParameterValue>?> PromptParametersAsync(
        IReadOnlyList<string> names,
        IReadOnlyDictionary<string, ParameterValue> defaults);

    /// <summary>Asks the production confirmation (mockup M15) before a modifying statement is sent to a production connection.</summary>
    /// <param name="request">The localized content; the head draws it and adds nothing.</param>
    /// <returns>Whether the user confirmed and whether the transaction option was checked.</returns>
    ValueTask<ProductionConfirmationResult> ConfirmProductionAsync(ProductionConfirmationRequest request);

    /// <summary>Asks what to do with an open transaction before the session is closed or changed.</summary>
    /// <param name="request">The localized question.</param>
    /// <returns>The choice; <see cref="TransactionChoice.Cancel"/> when the dialog was dismissed.</returns>
    ValueTask<TransactionChoice> AskTransactionAsync(TransactionCloseRequest request);
}
