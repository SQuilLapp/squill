using Squill.Core;
using Squill.Core.Editing;
using Squill.Data;
using Squill.Presentation.Localization;
using Squill.Presentation.Sessions;
using Squill.Sql.Editing;

namespace Squill.Presentation.Editing;

/// <summary>The outcome of a successful commit.</summary>
/// <param name="GeneratedKeys">Grid row of each inserted row that got a generated key, mapped to that key.</param>
/// <param name="StatementCount">Number of DML statements executed (key read-back queries not counted).</param>
public sealed record CommitResult(IReadOnlyDictionary<long, object?> GeneratedKeys, int StatementCount);

/// <summary>A commit failed and was rolled back.</summary>
public sealed class EditCommitException : Exception
{
    /// <summary>Creates the exception.</summary>
    public EditCommitException()
    {
    }

    /// <summary>Creates the exception with a message.</summary>
    /// <param name="message">What went wrong.</param>
    public EditCommitException(string message)
        : base(message)
    {
    }

    /// <summary>Creates the exception with a message and an inner exception.</summary>
    /// <param name="message">What went wrong.</param>
    /// <param name="inner">The provider exception.</param>
    public EditCommitException(string message, Exception inner)
        : base(message, inner)
    {
    }

    /// <summary>Creates the exception with a message and the failing grid row.</summary>
    /// <param name="message">What went wrong.</param>
    /// <param name="row">Grid row of the failing statement, or <see langword="null"/> when none applies.</param>
    /// <param name="inner">The provider exception.</param>
    public EditCommitException(string message, long? row, Exception? inner = null)
        : base(message, inner) => Row = row;

    /// <summary>Grid row of the statement that failed; <see langword="null"/> for begin and commit failures.</summary>
    public long? Row { get; }
}

/// <summary>Executes a change set in one transaction. Non-transactional MySQL engines such as MyISAM cannot be rolled back.</summary>
/// <param name="connection">The connection to run on.</param>
/// <param name="dialect">The engine behind the connection.</param>
public sealed class EditCommitter(IDatabaseConnection connection, DatabaseDialect dialect)
{
    /// <summary>Runs the edits in the given order inside a transaction.</summary>
    /// <param name="target">The table the edits apply to.</param>
    /// <param name="resultColumns">Columns of the grid result, used to type generated keys.</param>
    /// <param name="edits">The edits in execution order.</param>
    /// <param name="ct">Cancels the commit; the transaction is rolled back.</param>
    /// <returns>The generated keys by grid row and the statement count.</returns>
    /// <exception cref="EditCommitException">A statement failed or did not affect exactly one row; everything was rolled back.</exception>
    public async Task<CommitResult> CommitAsync(EditTarget target, IReadOnlyList<ColumnInfo> resultColumns, IReadOnlyList<PendingEdit> edits, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(resultColumns);
        ArgumentNullException.ThrowIfNull(edits);
        var keys = new Dictionary<long, object?>();
        if (edits.Count == 0)
            return new CommitResult(keys, 0);

        long? row = null;
        try
        {
            await SqlRunner.BeginAsync(connection, dialect, ct);
            foreach (var pending in edits)
            {
                row = pending.Row;
                await RunAsync(pending, target, resultColumns, keys, ct);
            }

            row = null;
            await SqlRunner.CommitAsync(connection, dialect, ct);
            return new CommitResult(keys, edits.Count);
        }
        catch (Exception ex)
        {
            await SqlRunner.RollbackAsync(connection, dialect, CancellationToken.None);
            if (ex is OperationCanceledException or EditCommitException)
                throw;
            throw new EditCommitException(ex.Message, row, ex);
        }
    }

    private async Task RunAsync(PendingEdit pending, EditTarget target, IReadOnlyList<ColumnInfo> resultColumns, Dictionary<long, object?> keys, CancellationToken ct)
    {
        if (pending.Edit is InsertRow insert)
        {
            var plan = DmlGenerator.PlanInsert(insert, target, dialect);
            var generated = GeneratedKeyColumn(insert, target);
            object?[]? keyRow;
            if (plan.KeyQuery is not null)
            {
                await SqlRunner.ExecuteAsync(connection, plan.Insert.Sql, Map(plan.Insert), ct);
                keyRow = await SqlRunner.QueryFirstRowAsync(connection, plan.KeyQuery.Sql, Map(plan.KeyQuery), ct);
            }
            else if (generated is not null && DmlGenerator.ReturnsKeyFromInsert(dialect))
            {
                keyRow = await SqlRunner.QueryFirstRowAsync(connection, plan.Insert.Sql, Map(plan.Insert), ct);
            }
            else
            {
                await SqlRunner.ExecuteAsync(connection, plan.Insert.Sql, Map(plan.Insert), ct);
                return;
            }

            if (keyRow is { Length: > 0 })
                keys[pending.Row] = ConvertKey(keyRow[0], KeyType(generated, target, resultColumns));
            return;
        }

        var statement = DmlGenerator.Generate(pending.Edit, target, dialect);
        var affected = await SqlRunner.ExecuteAsync(connection, statement.Sql, Map(statement), ct);
        if (affected == 0)
            throw new EditCommitException(UiText.Format("results.rowNotFound", pending.Row), pending.Row);
        if (affected > 1)
            throw new EditCommitException(UiText.Format("results.rowMatchedMany", pending.Row), pending.Row);
    }

    private static List<QueryParameter> Map(DmlStatement statement) => [.. statement.Parameters.Select(p => new QueryParameter(p.Name, p.Value))];

    // Mirrors DmlGenerator.PlanInsert: the auto-increment key column the insert leaves to the database.
    private static string? GeneratedKeyColumn(InsertRow insert, EditTarget target) =>
        target.KeyColumns.FirstOrDefault(k =>
            target.Columns.Any(c => c.Definition.IsAutoIncrement && string.Equals(c.Definition.Name, k, StringComparison.Ordinal))
            && !insert.Values.Any(v => string.Equals(v.Column, k, StringComparison.Ordinal)));

    private static Type? KeyType(string? column, EditTarget target, IReadOnlyList<ColumnInfo> resultColumns)
    {
        var ordinal = target.Columns.FirstOrDefault(c => string.Equals(c.Definition.Name, column, StringComparison.Ordinal))?.ResultOrdinal;
        return resultColumns.FirstOrDefault(c => c.Ordinal == ordinal)?.ClrType;
    }

    private static object? ConvertKey(object? value, Type? type)
    {
        if (type is null || value is null || value.GetType() == type)
            return value;
        decimal? number = value switch
        {
            int i => i,
            long l => l,
            short s => s,
            byte b => b,
            decimal m => m,
            ulong u => u,
            _ => null,
        };
        if (number is not { } n)
            return value;
        if (type == typeof(int))
            return (int)n;
        if (type == typeof(long))
            return (long)n;
        if (type == typeof(short))
            return (short)n;
        if (type == typeof(byte))
            return (byte)n;
        if (type == typeof(decimal))
            return n;
        if (type == typeof(ulong))
            return (ulong)n;
        return value;
    }
}
