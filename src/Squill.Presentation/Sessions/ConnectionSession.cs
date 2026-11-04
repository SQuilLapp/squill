using Squill.Core;
using Squill.Data;
using Squill.Platform.Ssh;

namespace Squill.Presentation.Sessions;

/// <summary>A live connection to a database, with its optional SSH tunnel and its last schema snapshot.</summary>
public sealed class ConnectionSession : IAsyncDisposable
{
    private readonly ISshTunnel? _tunnel;
    private readonly Dictionary<string, Task<ConnectionSession>> _derived = new(StringComparer.OrdinalIgnoreCase);

    internal ConnectionSession(ConnectionProfile profile, IDatabaseConnection connection, SchemaSnapshot schema, ISshTunnel? tunnel)
    {
        Profile = profile;
        Connection = connection;
        Schema = schema;
        _tunnel = tunnel;
    }

    /// <summary>Gets the session that owns this one when it was opened on another database of the same server; <see langword="null"/> for a session opened from a profile.</summary>
    public ConnectionSession? Parent { get; internal init; }

    /// <summary>Gets the session opened from a profile: <see cref="Parent"/> when there is one, else this session.</summary>
    public ConnectionSession Root => Parent ?? this;

    /// <summary>Gets the names of the server's databases as read when the session opened, empty when they could not be listed.</summary>
    public IReadOnlyList<string> Databases { get; internal init; } = [];

    internal Func<string, CancellationToken, ValueTask<ConnectionSession>>? DatabaseOpener { get; init; }

    /// <summary>Gets the sessions already opened on other databases, in no particular order.</summary>
    public IEnumerable<ConnectionSession> OpenedDatabases =>
        _derived.Values.Where(t => t is { IsCompletedSuccessfully: true }).Select(t => t.Result);

    /// <summary>Gets the profile the session was opened from (with the original host, not the tunnel's).</summary>
    public ConnectionProfile Profile { get; }

    /// <summary>Gets the open database connection.</summary>
    public IDatabaseConnection Connection { get; }

    /// <summary>Gets the schema as of the last read.</summary>
    public SchemaSnapshot Schema { get; private set; }

    /// <summary>Reads the schema again and replaces <see cref="Schema"/>.</summary>
    /// <param name="ct">Cancellation token.</param>
    public async ValueTask RefreshSchemaAsync(CancellationToken ct) => Schema = await Connection.ReadSchemaAsync(ct);

    /// <summary>Gets the session opened on another database of the server, or <see langword="null"/> when it is not open.</summary>
    /// <param name="database">The database name.</param>
    public ConnectionSession? FindDatabase(string database) =>
        _derived.TryGetValue(database, out var t) && t.IsCompletedSuccessfully ? t.Result : null;

    /// <summary>Opens, once, a session on another database of the same server; the session is closed with this one.</summary>
    /// <param name="database">The database name.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The session on that database; this session when it already is on it.</returns>
    /// <exception cref="InvalidOperationException">The session cannot open other databases.</exception>
    public Task<ConnectionSession> OpenDatabaseAsync(string database, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrEmpty(database);
        if (string.Equals(Profile.Database, database, StringComparison.OrdinalIgnoreCase))
            return Task.FromResult(this);
        if (DatabaseOpener is not { } opener)
            throw new InvalidOperationException("This session cannot open other databases.");
        if (_derived.TryGetValue(database, out var existing))
            return existing;
        var task = opener(database, ct).AsTask();
        _derived[database] = task;
        task.ContinueWith(_ => _derived.Remove(database), CancellationToken.None, TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
        return task;
    }

    /// <summary>Disposes the sessions opened on other databases, then the connection, then the tunnel.</summary>
    public async ValueTask DisposeAsync()
    {
        try
        {
            foreach (var task in _derived.Values.ToList())
            {
                try
                {
                    await (await task).DisposeAsync();
                }
                catch (Exception)
                {
                    // A failed open has nothing to dispose; the others must still be closed.
                }
            }
            _derived.Clear();
            await Connection.DisposeAsync();
        }
        finally
        {
            if (_tunnel is not null)
                await _tunnel.DisposeAsync();
        }
    }
}
