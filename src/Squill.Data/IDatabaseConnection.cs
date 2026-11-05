using Squill.Core;

namespace Squill.Data;

/// <summary>An open database connection; one call executes exactly one statement.</summary>
public interface IDatabaseConnection : IAsyncDisposable
{
    /// <summary>The server version string reported by the engine.</summary>
    string ServerVersion { get; }

    /// <summary>Executes a single statement and returns its result stream.</summary>
    ValueTask<IResultReader> ExecuteAsync(string sql, IReadOnlyList<QueryParameter> parameters, CancellationToken cancellationToken);

    /// <summary>Reads the tables and views of the connected database.</summary>
    ValueTask<SchemaSnapshot> ReadSchemaAsync(CancellationToken cancellationToken);

    /// <summary>Lists the user databases of the server, system ones excluded; SQLite returns its single file name.</summary>
    ValueTask<IReadOnlyList<string>> ListDatabasesAsync(CancellationToken cancellationToken);
}
