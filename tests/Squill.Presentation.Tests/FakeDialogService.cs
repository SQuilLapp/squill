using Squill.Presentation.Query;
using Squill.Presentation.Safety;

namespace Squill.Presentation.Tests;

/// <summary>Scriptable dialog service that records what view-models ask.</summary>
internal sealed class FakeDialogService : IDialogService
{
    public bool ConfirmResult { get; set; } = true;
    public string? PromptResult { get; set; }
    public string? SavePathResult { get; set; }
    public Func<IReadOnlyList<string>, IReadOnlyDictionary<string, ParameterValue>, IReadOnlyList<ParameterValue>?>? ParametersResult { get; set; }
    public List<IReadOnlyDictionary<string, ParameterValue>> ParameterPrompts { get; } = [];
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
        return ValueTask.FromResult(PromptResult ?? initial);
    }

    public ValueTask<string?> PickSaveFileAsync(string suggestedName, string extension) =>
        ValueTask.FromResult(SavePathResult);

    public ValueTask ShowErrorAsync(string title, string message)
    {
        Errors.Add((title, message));
        return ValueTask.CompletedTask;
    }

    public ValueTask<IReadOnlyList<ParameterValue>?> PromptParametersAsync(
        IReadOnlyList<string> names,
        IReadOnlyDictionary<string, ParameterValue> defaults)
    {
        ParameterPrompts.Add(new Dictionary<string, ParameterValue>(defaults));
        return ValueTask.FromResult(ParametersResult?.Invoke(names, defaults));
    }
}
