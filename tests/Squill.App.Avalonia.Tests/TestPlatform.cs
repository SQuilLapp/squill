using Squill.App.Avalonia.Editors;
using Squill.Data;
using Squill.Presentation;
using Squill.Presentation.Editor;
using Squill.Presentation.Query;

namespace Squill.App.Avalonia.Tests;

internal sealed class TestPlatform(ProviderRegistry providers) : IPlatformServices
{
    public IUiDispatcher Dispatcher { get; } = new UiThreadDispatcher();
    public IDialogService Dialogs { get; } = new Dialogs();
    public Func<ISqlEditor> EditorFactory { get; } = () => new AvaloniaSqlEditor();
    public ProviderRegistry Providers { get; } = providers;
}

internal sealed class Dialogs : IDialogService
{
    public ValueTask<bool> ConfirmSshHostKeyAsync(Squill.Platform.Ssh.SshHostKeyRequest request) => ValueTask.FromResult(false);

    public ValueTask<Squill.Presentation.Safety.ProductionConfirmationResult> ConfirmProductionAsync(Squill.Presentation.Safety.ProductionConfirmationRequest request) =>
        ValueTask.FromResult(new Squill.Presentation.Safety.ProductionConfirmationResult(true, false));

    public ValueTask<Squill.Presentation.Safety.TransactionChoice> AskTransactionAsync(Squill.Presentation.Safety.TransactionCloseRequest request) =>
        ValueTask.FromResult(Squill.Presentation.Safety.TransactionChoice.Rollback);

    public ValueTask<bool> ConfirmAsync(string title, string message) => ValueTask.FromResult(true);
    public ValueTask<string?> PromptAsync(string title, string message, string? initial) => ValueTask.FromResult<string?>(null);
    public ValueTask<string?> PickSaveFileAsync(string suggestedName, string extension) => ValueTask.FromResult<string?>(null);
    public ValueTask ShowErrorAsync(string title, string message) => throw new InvalidOperationException(message);
}

/// <summary>Runs inline on the UI thread and marshals a call from any other thread (a timer tick, a continuation) to it, as the real dispatcher does.</summary>
/// <remarks>An inline-everywhere dispatcher let the 30 s timer of a workspace left over from an earlier test update controls from a pool thread.</remarks>
internal sealed class UiThreadDispatcher : IUiDispatcher
{
    // Captured at construction (on the UI thread): reading Dispatcher.UIThread from a pool thread could bind a freshly reset dispatcher to that thread.
    private readonly global::Avalonia.Threading.Dispatcher _ui = global::Avalonia.Threading.Dispatcher.UIThread;

    public bool CheckAccess() => _ui.CheckAccess();

    public void Post(Action action)
    {
        if (CheckAccess())
            action();
        else
            _ui.Post(action);
    }
}
