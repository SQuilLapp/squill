using Squill.Presentation.Query;

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

    public ValueTask<bool> ConfirmAsync(string title, string message) => ValueTask.FromResult(ConfirmResult);

    public ValueTask<string?> PromptAsync(string title, string message, string? initial) =>
        ValueTask.FromResult(PromptResult ?? initial);

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
