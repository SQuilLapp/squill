using Microsoft.UI.Dispatching;
using Squill.Presentation;

namespace Squill.App.WinUI.Services;

/// <summary>Runs view-model work on the WinUI UI thread.</summary>
internal sealed class WinUiDispatcher(DispatcherQueue queue) : IUiDispatcher
{
    /// <inheritdoc />
    public bool CheckAccess() => queue.HasThreadAccess;

    /// <inheritdoc />
    public void Post(Action action)
    {
        ArgumentNullException.ThrowIfNull(action);
        queue.TryEnqueue(() => action());
    }
}
