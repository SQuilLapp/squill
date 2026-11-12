using Squill.Sql.Transactions;

namespace Squill.Presentation.Safety;

/// <summary>What the production confirmation protects.</summary>
public enum ConfirmationKind
{
    /// <summary>A run of statements from a query tab.</summary>
    Run,
    /// <summary>A <c>Valider</c> of the session transaction.</summary>
    Commit,
    /// <summary>A grid commit.</summary>
    GridEdit,
    /// <summary>A table designer apply.</summary>
    SchemaChange,
}

/// <summary>One statement listed in the confirmation.</summary>
/// <param name="Text">The statement text, shown as written.</param>
/// <param name="Target">The table written to, or <see langword="null"/>.</param>
/// <param name="Kind">The kind of the statement; <see cref="StatementKind.Empty"/> marks the trailing "+ N others" line, whose <paramref name="Text"/> is already localized.</param>
public sealed record ConfirmedStatement(string Text, string? Target, StatementKind Kind);

/// <summary>Everything a head needs to draw the production confirmation; every text is already localized and formatted.</summary>
/// <param name="Kind">What is confirmed.</param>
/// <param name="ConnectionName">The connection name for the red header.</param>
/// <param name="Database">The database for the red header.</param>
/// <param name="Statements">The statements to list (at most five, then the "+ N others" line).</param>
/// <param name="EstimatedRows">The planner estimate, only so the head can color the figure; already part of <paramref name="Message"/>.</param>
/// <param name="RequiredText">The word the user must type before the confirm button enables, or <see langword="null"/> when nothing is typed.</param>
/// <param name="ShowTransactionOption">Whether to show the "run in a transaction" checkbox.</param>
/// <param name="TransactionOptionLocked">Whether the checkbox is checked and disabled (manual mode).</param>
/// <param name="Title">The dialog title.</param>
/// <param name="Message">The message.</param>
/// <param name="ConfirmLabel">The label of the confirm button.</param>
public sealed record ProductionConfirmationRequest(
    ConfirmationKind Kind,
    string ConnectionName,
    string Database,
    IReadOnlyList<ConfirmedStatement> Statements,
    long? EstimatedRows,
    string? RequiredText,
    bool ShowTransactionOption,
    bool TransactionOptionLocked,
    string Title,
    string Message,
    string ConfirmLabel);

/// <summary>The answer to a production confirmation.</summary>
/// <param name="Confirmed">Whether the user confirmed.</param>
/// <param name="UseTransaction">Whether the "run in a transaction" checkbox was checked.</param>
public sealed record ProductionConfirmationResult(bool Confirmed, bool UseTransaction);
