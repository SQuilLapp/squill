using System.Diagnostics;
using Squill.Core;
using Squill.Core.Transactions;
using Squill.Platform.Storage;
using Squill.Presentation.Localization;
using Squill.Presentation.Sessions;

namespace Squill.Presentation.Safety;

/// <summary>What the user decided about an open transaction.</summary>
public enum TransactionChoice
{
    /// <summary>Commit the transaction, then go on.</summary>
    Commit,
    /// <summary>Roll the transaction back, then go on.</summary>
    Rollback,
    /// <summary>Do nothing and stay where we were.</summary>
    Cancel,
}

/// <summary>The question asked when something would end the session while a transaction is open.</summary>
/// <param name="ConnectionName">The connection the transaction belongs to.</param>
/// <param name="Environment">The environment of the connection.</param>
/// <param name="Database">The database, or the SQLite file name.</param>
/// <param name="State">The transaction state; on <see cref="TransactionState.Failed"/> the heads hide the commit button.</param>
/// <param name="PendingCount">The statements not yet committed.</param>
/// <param name="Statements">The first five journal texts.</param>
/// <param name="Title">The dialog title, already localized.</param>
/// <param name="Message">The message, already localized.</param>
public sealed record TransactionCloseRequest(
    string ConnectionName,
    ConnectionEnvironment Environment,
    string Database,
    TransactionState State,
    int PendingCount,
    IReadOnlyList<string> Statements,
    string Title,
    string Message);

/// <summary>Resolves an open transaction before the caller ends or changes the session.</summary>
internal static class TransactionPrompt
{
    /// <summary>Commits or rolls the transaction back and writes the matching history entry.</summary>
    /// <returns>The error, or <see langword="null"/> on success.</returns>
    internal static async ValueTask<Exception?> FinishAsync(ConnectionSession session, IQueryHistoryStore history, bool commit, CancellationToken ct)
    {
        var clock = Stopwatch.StartNew();
        Exception? error = null;
        try
        {
            if (commit)
                await session.Transaction.CommitAsync(ct);
            else
                await session.Transaction.RollbackAsync(ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            error = ex;
        }

        await history.AppendAsync(new HistoryEntry(DateTimeOffset.UtcNow, session.Profile.Id, commit ? "COMMIT" : "ROLLBACK", error is null, null, clock.Elapsed), CancellationToken.None);
        return error;
    }

    /// <summary>Asks what to do with the open transaction of a session and does it.</summary>
    /// <param name="session">The session about to be closed or changed.</param>
    /// <param name="dialogs">Asks the question and shows errors.</param>
    /// <param name="history">Receives the <c>COMMIT</c> / <c>ROLLBACK</c> entry.</param>
    /// <param name="reasonKey">Catalog key of the dialog title: <c>tx.ask.closeTab</c>, <c>tx.ask.disconnect</c>, <c>tx.ask.switchMode</c> or <c>tx.ask.exit</c>.</param>
    /// <param name="ct">Cancels the commit or rollback.</param>
    /// <returns><see langword="true"/> when the caller may go on: nothing was open, or the transaction is now closed.</returns>
    internal static async ValueTask<bool> ResolveAsync(ConnectionSession session, IDialogService dialogs, IQueryHistoryStore history, string reasonKey, CancellationToken ct)
    {
        var tx = session.Transaction;
        if (tx.State == TransactionState.None)
            return true;
        TransactionChoice choice;
        if (tx.State == TransactionState.Open && tx.PendingCount == 0 && tx.Mode == TransactionMode.Manual)
        {
            choice = TransactionChoice.Rollback; // nothing written yet: nothing to lose
        }
        else
        {
            var profile = session.Profile;
            var database = profile.Dialect == DatabaseDialect.Sqlite ? ProductionGuard.RequiredText(profile) : profile.Database ?? "";
            var message = tx.State == TransactionState.Failed
                ? UiText.Get("tx.ask.failed")
                : UiText.FormatPlural("tx.ask.message", tx.PendingCount, profile.Name, database);
            choice = await dialogs.AskTransactionAsync(new TransactionCloseRequest(
                profile.Name, profile.Environment, database, tx.State, tx.PendingCount,
                [.. tx.Journal.Take(5).Select(p => p.Text)], UiText.Get(reasonKey), message));
        }

        if (choice == TransactionChoice.Cancel)
            return false;
        if (await FinishAsync(session, history, choice == TransactionChoice.Commit, ct) is { } error)
        {
            await dialogs.ShowErrorAsync(UiText.Get(choice == TransactionChoice.Commit ? "tx.ask.commitFailed" : "tx.ask.rollbackFailed"), error.Message);
            return false;
        }

        return true;
    }
}
