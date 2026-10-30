namespace Squill.Presentation;

/// <summary>Marshals work onto the UI thread so view-models stay independent of the UI framework.</summary>
public interface IUiDispatcher
{
    /// <summary>Gets whether the calling thread is the UI thread.</summary>
    /// <returns><see langword="true"/> when the caller can touch UI-bound state directly.</returns>
    bool CheckAccess();

    /// <summary>Queues an action to run on the UI thread.</summary>
    /// <param name="action">The action to run.</param>
    void Post(Action action);
}
