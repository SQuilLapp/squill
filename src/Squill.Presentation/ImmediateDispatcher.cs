namespace Squill.Presentation;

/// <summary>Dispatcher that runs every action inline on the calling thread, for tests and headless use.</summary>
public sealed class ImmediateDispatcher : IUiDispatcher
{
    /// <inheritdoc/>
    public bool CheckAccess() => true;

    /// <inheritdoc/>
    public void Post(Action action)
    {
        ArgumentNullException.ThrowIfNull(action);
        action();
    }
}
