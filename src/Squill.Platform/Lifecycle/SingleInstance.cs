using System.IO.Pipes;

namespace Squill.Platform.Lifecycle;

/// <summary>Lets one process per user run the application: a second start asks the first to show its window, then exits.</summary>
public sealed class SingleInstance : IDisposable
{
    private readonly Mutex _mutex;
    private readonly CancellationTokenSource _stop = new();

    private SingleInstance(Mutex mutex, string name, Action onActivate)
    {
        _mutex = mutex;
        _ = Task.Run(() => ListenAsync(name, onActivate, _stop.Token));
    }

    /// <summary>Gets the name that identifies the application of the current user.</summary>
    public static string DefaultName => "SQuilL-" + new string([.. Environment.UserName.Where(char.IsLetterOrDigit)]);

    /// <summary>Takes the right to run, or asks the running instance to show itself.</summary>
    /// <param name="name">Identifies the application, for example <see cref="DefaultName"/>.</param>
    /// <param name="onActivate">Called, on a background thread, when another start asked this instance to show its window.</param>
    /// <returns>The handle to keep for the whole run, or <see langword="null"/> when another instance runs and was told to show itself: the caller must exit.</returns>
    public static SingleInstance? TryAcquire(string name, Action onActivate)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        ArgumentNullException.ThrowIfNull(onActivate);
        var mutex = new Mutex(true, name, out var first);
        if (first)
            return new SingleInstance(mutex, name, onActivate);
        mutex.Dispose();
        try
        {
            using var client = new NamedPipeClientStream(".", name, PipeDirection.Out);
            client.Connect(2000);
            client.WriteByte(1);
        }
        catch (Exception ex) when (ex is TimeoutException or IOException)
        {
            // The first instance is still starting: it shows its window anyway.
        }

        return null;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _stop.Cancel();
        _mutex.Dispose();
    }

    private static async Task ListenAsync(string name, Action onActivate, CancellationToken stop)
    {
        while (!stop.IsCancellationRequested)
        {
            try
            {
                await using var server = new NamedPipeServerStream(name, PipeDirection.In, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous);
                await server.WaitForConnectionAsync(stop);
                onActivate();
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (IOException)
            {
                // A client that left before it was read: wait for the next one.
            }
        }
    }
}
