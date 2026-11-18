using System.ComponentModel;

namespace Squill.Presentation;

/// <summary>Moves a <c>PropertyChanged</c> subscription from one object to the next in a head field.</summary>
public static class PropertyChangedSwap
{
    /// <summary>Unsubscribes the handler from the current object, stores the new one and subscribes to it.</summary>
    /// <typeparam name="T">The type of the observed object.</typeparam>
    /// <param name="current">The field holding the observed object.</param>
    /// <param name="next">The object to observe from now on, or <see langword="null"/> for none.</param>
    /// <param name="handler">The handler to move.</param>
    /// <returns><see langword="true"/> when the object changed; <see langword="false"/> when it was already the observed one.</returns>
    public static bool Swap<T>(ref T? current, T? next, PropertyChangedEventHandler handler)
        where T : class, INotifyPropertyChanged
    {
        if (ReferenceEquals(current, next))
            return false;
        if (current is not null)
            current.PropertyChanged -= handler;
        current = next;
        if (current is not null)
            current.PropertyChanged += handler;
        return true;
    }
}
