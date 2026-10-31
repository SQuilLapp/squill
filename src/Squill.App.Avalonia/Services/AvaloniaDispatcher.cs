using Avalonia.Threading;
using Squill.Presentation;

namespace Squill.App.Avalonia.Services;

/// <summary>Runs view-model work on the Avalonia UI thread.</summary>
internal sealed class AvaloniaDispatcher : IUiDispatcher
{
    /// <inheritdoc />
    public bool CheckAccess() => Dispatcher.UIThread.CheckAccess();

    /// <inheritdoc />
    public void Post(Action action)
    {
        ArgumentNullException.ThrowIfNull(action);
        Dispatcher.UIThread.Post(action);
    }
}
