using Squill.App.Avalonia.Editors;
using Squill.Data;
using Squill.Presentation;
using Squill.Presentation.Editor;
using Squill.Presentation.Query;

namespace Squill.App.Avalonia.Tests;

internal sealed class TestPlatform(ProviderRegistry providers) : IPlatformServices
{
    public IUiDispatcher Dispatcher { get; } = new ImmediateDispatcher();
    public IDialogService Dialogs { get; } = new Dialogs();
    public Func<ISqlEditor> EditorFactory { get; } = () => new AvaloniaSqlEditor();
    public ProviderRegistry Providers { get; } = providers;
}

internal sealed class Dialogs : IDialogService
{
    public ValueTask<bool> ConfirmAsync(string title, string message) => ValueTask.FromResult(true);
    public ValueTask<string?> PromptAsync(string title, string message, string? initial) => ValueTask.FromResult<string?>(null);
    public ValueTask<string?> PickSaveFileAsync(string suggestedName, string extension) => ValueTask.FromResult<string?>(null);
    public ValueTask ShowErrorAsync(string title, string message) => throw new InvalidOperationException(message);
    public ValueTask<IReadOnlyList<ParameterValue>?> PromptParametersAsync(
        IReadOnlyList<string> names,
        IReadOnlyDictionary<string, ParameterValue> defaults) => ValueTask.FromResult<IReadOnlyList<ParameterValue>?>(null);
}
