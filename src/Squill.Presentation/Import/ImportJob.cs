using Squill.Core;
using Squill.Presentation.Sessions;

namespace Squill.Presentation.Import;

/// <summary>What the runner does with a row that cannot be imported.</summary>
public enum ImportErrorPolicy
{
    /// <summary>Skip the row, write it to the rejects file and go on.</summary>
    Reject,

    /// <summary>The first bad row stops the import; what was already committed stays.</summary>
    Stop,
}

/// <summary>When the imported rows are committed.</summary>
public enum ImportCommitMode
{
    /// <summary>One transaction (or savepoint inside the user's transaction) per batch.</summary>
    PerBatch,

    /// <summary>All or nothing: one bad row, a cancel or a failure leaves the table untouched.</summary>
    SingleTransaction,
}

/// <summary>How the import runs.</summary>
public sealed record ImportOptions
{
    /// <summary>Gets the rows per statement before the engine's parameter limit clamps it.</summary>
    public int BatchSize { get; init; } = 1000;

    /// <summary>Gets what happens to a bad row; <see cref="ImportCommitMode.SingleTransaction"/> always stops at the first one.</summary>
    public ImportErrorPolicy OnError { get; init; } = ImportErrorPolicy.Reject;

    /// <summary>Gets when rows are committed.</summary>
    public ImportCommitMode CommitMode { get; init; } = ImportCommitMode.PerBatch;

    /// <summary>Gets the rejects file, or <see langword="null"/> for <c>file.rejets.csv</c> next to the source.</summary>
    public string? RejectsPath { get; init; }
}

/// <summary>Everything the runner needs.</summary>
/// <param name="Session">The session whose connection receives the rows (and whose transaction is respected).</param>
/// <param name="Target">The table to fill.</param>
/// <param name="Mapping">The CSV to table column mapping.</param>
/// <param name="Format">How field text becomes typed values.</param>
/// <param name="Csv">How the file is decoded and split.</param>
/// <param name="Path">The CSV file.</param>
/// <param name="Options">Batching, error and commit policy.</param>
public sealed record ImportJob(ConnectionSession Session, TableInfo Target, ImportMapping Mapping, ImportFormat Format, CsvOptions Csv, string Path, ImportOptions Options);

/// <summary>A progress report, sent at most every 250 ms.</summary>
/// <param name="Read">Data rows read so far.</param>
/// <param name="Inserted">Rows inserted so far.</param>
/// <param name="Rejected">Rows rejected so far.</param>
/// <param name="RowsPerSecond">Average rows handled per second.</param>
/// <param name="Remaining">Estimated time left from the file position, or <see langword="null"/> when unknown.</param>
public sealed record ImportProgress(long Read, long Inserted, long Rejected, double RowsPerSecond, TimeSpan? Remaining);

/// <summary>How an import ended.</summary>
public enum ImportOutcome
{
    /// <summary>The whole file was handled.</summary>
    Completed,

    /// <summary>The user cancelled, or declined the production confirmation.</summary>
    Cancelled,

    /// <summary>A failure that is not about one row (connection, missing table, refused mapping).</summary>
    Failed,

    /// <summary>A bad row stopped the import.</summary>
    StoppedOnError,
}

/// <summary>The final figures of an import.</summary>
/// <param name="Read">Data rows read.</param>
/// <param name="Inserted">Rows inserted and kept.</param>
/// <param name="Rejected">Rows rejected.</param>
/// <param name="Elapsed">Wall-clock duration.</param>
/// <param name="RejectsPath">The rejects file, or <see langword="null"/> when no row was rejected.</param>
/// <param name="Completed">How the import ended.</param>
/// <param name="Error">The failure text for <see cref="ImportOutcome.Failed"/> and <see cref="ImportOutcome.StoppedOnError"/>.</param>
public sealed record ImportResult(long Read, long Inserted, long Rejected, TimeSpan Elapsed, string? RejectsPath, ImportOutcome Completed, string? Error = null);
