using Squill.Core;
using Squill.Data;

namespace Squill.Presentation.Sessions;

/// <summary>Runs single statements on a connection and brackets them in transactions; shared by the designer and the grid commit.</summary>
internal static class SqlRunner
{
    private const int DrainBatchSize = 1000;

    /// <summary>Executes one statement and drains its result.</summary>
    /// <param name="connection">The connection to run on.</param>
    /// <param name="sql">The statement text.</param>
    /// <param name="parameters">The statement parameters.</param>
    /// <param name="ct">Cancels the execution.</param>
    /// <returns>The rows affected, or <c>-1</c> when the engine does not report it.</returns>
    public static async ValueTask<long> ExecuteAsync(IDatabaseConnection connection, string sql, IReadOnlyList<QueryParameter> parameters, CancellationToken ct)
    {
        var reader = await connection.ExecuteAsync(sql, parameters, ct);
        try
        {
            do
            {
                while (await reader.ReadBatchAsync(DrainBatchSize, ct) is not null)
                {
                }
            }
            while (await reader.NextResultAsync(ct));

            return reader.RecordsAffected;
        }
        finally
        {
            await reader.DisposeAsync();
        }
    }

    /// <summary>Executes one statement, keeps its first row and drains the rest.</summary>
    /// <param name="connection">The connection to run on.</param>
    /// <param name="sql">The statement text.</param>
    /// <param name="parameters">The statement parameters.</param>
    /// <param name="ct">Cancels the execution.</param>
    /// <returns>The first row, or <c>null</c> when the statement returned none.</returns>
    public static async ValueTask<object?[]?> QueryFirstRowAsync(IDatabaseConnection connection, string sql, IReadOnlyList<QueryParameter> parameters, CancellationToken ct)
    {
        var reader = await connection.ExecuteAsync(sql, parameters, ct);
        try
        {
            object?[]? first = null;
            do
            {
                while (await reader.ReadBatchAsync(DrainBatchSize, ct) is { } batch)
                {
                    if (first is null && batch.Length > 0)
                        first = batch[0];
                }
            }
            while (await reader.NextResultAsync(ct));

            return first;
        }
        finally
        {
            await reader.DisposeAsync();
        }
    }

    /// <summary>Starts a transaction.</summary>
    /// <param name="c">The connection to run on.</param>
    /// <param name="d">The dialect that picks the statement text.</param>
    /// <param name="ct">Cancels the execution.</param>
    public static async ValueTask BeginAsync(IDatabaseConnection c, DatabaseDialect d, CancellationToken ct) =>
        await ExecuteAsync(c, d == DatabaseDialect.SqlServer ? "BEGIN TRANSACTION" : "BEGIN", [], ct);

    /// <summary>Commits the open transaction.</summary>
    /// <param name="c">The connection to run on.</param>
    /// <param name="d">The dialect that picks the statement text.</param>
    /// <param name="ct">Cancels the execution.</param>
    public static async ValueTask CommitAsync(IDatabaseConnection c, DatabaseDialect d, CancellationToken ct) =>
        await ExecuteAsync(c, d == DatabaseDialect.SqlServer ? "COMMIT TRANSACTION" : "COMMIT", [], ct);

    /// <summary>Rolls the open transaction back; a failing rollback is ignored so the original error stays the one reported.</summary>
    /// <param name="c">The connection to run on.</param>
    /// <param name="d">The dialect that picks the statement text.</param>
    /// <param name="ct">Cancels the execution.</param>
    public static async ValueTask RollbackAsync(IDatabaseConnection c, DatabaseDialect d, CancellationToken ct)
    {
        try
        {
            await ExecuteAsync(c, d == DatabaseDialect.SqlServer ? "ROLLBACK TRANSACTION" : "ROLLBACK", [], ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Best effort: the failure that caused the rollback is reported instead.
        }
    }
}
