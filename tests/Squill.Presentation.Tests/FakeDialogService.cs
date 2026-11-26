using Squill.Presentation.Query;
using Squill.Presentation.Safety;

namespace Squill.Presentation.Tests;

/// <summary>Scriptable dialog service that records what view-models ask.</summary>
internal sealed class FakeDialogService : IDialogService
{
    public bool ConfirmResult { get; set; } = true;
    public string? PromptResult { get; set; }

    /// <summary>Gets or sets whether the prompt is cancelled (returns <see langword="null"/>).</summary>
    public bool PromptCancelled { get; set; }
    public string? SavePathResult { get; set; }
    public List<(string Title, string Message)> Errors { get; } = [];

    public List<(string Title, string Message)> Confirms { get; } = [];

    public List<(string Title, string Message)> Prompts { get; } = [];

    public ProductionConfirmationResult ProductionResult { get; set; } = new(true, false);

    public List<ProductionConfirmationRequest> ProductionRequests { get; } = [];

    public ValueTask<ProductionConfirmationResult> ConfirmProductionAsync(ProductionConfirmationRequest request)
    {
        ProductionRequests.Add(request);
        return ValueTask.FromResult(ProductionResult);
    }

    public bool SshHostKeyResult { get; set; }

    public List<Squill.Platform.Ssh.SshHostKeyRequest> SshHostKeyRequests { get; } = [];

    public ValueTask<bool> ConfirmSshHostKeyAsync(Squill.Platform.Ssh.SshHostKeyRequest request)
    {
        SshHostKeyRequests.Add(request);
        return ValueTask.FromResult(SshHostKeyResult);
    }

    public Queue<TransactionChoice> TransactionChoices { get; } = [];

    public List<TransactionCloseRequest> TransactionRequests { get; } = [];

    public ValueTask<TransactionChoice> AskTransactionAsync(TransactionCloseRequest request)
    {
        TransactionRequests.Add(request);
        return ValueTask.FromResult(TransactionChoices.Count > 0 ? TransactionChoices.Dequeue() : TransactionChoice.Cancel);
    }

    public ValueTask<bool> ConfirmAsync(string title, string message)
    {
        Confirms.Add((title, message));
        return ValueTask.FromResult(ConfirmResult);
    }

    public ValueTask<string?> PromptAsync(string title, string message, string? initial) =>
        Record(title, message, initial);

    private ValueTask<string?> Record(string title, string message, string? initial)
    {
        Prompts.Add((title, message));
        return ValueTask.FromResult(PromptCancelled ? null : PromptResult ?? initial);
    }

    public ValueTask<string?> PickSaveFileAsync(string suggestedName, string extension) =>
        ValueTask.FromResult(SavePathResult);

    public ValueTask ShowErrorAsync(string title, string message)
    {
        Errors.Add((title, message));
        return ValueTask.CompletedTask;
    }

    public string? OpenFileResult { get; set; }

    public List<Squill.Presentation.Import.ImportWizardViewModel> Wizards { get; } = [];

    public ValueTask<string?> PickOpenFileAsync(string title, IReadOnlyList<string> extensions) => ValueTask.FromResult(OpenFileResult);

    public ValueTask ShowImportWizardAsync(Squill.Presentation.Import.ImportWizardViewModel wizard)
    {
        Wizards.Add(wizard);
        return ValueTask.CompletedTask;
    }
}
