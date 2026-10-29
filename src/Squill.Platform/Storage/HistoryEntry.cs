namespace Squill.Platform.Storage;

/// <summary>One executed query.</summary>
/// <param name="At">When the execution started.</param>
/// <param name="ProfileId">Connection profile it ran against.</param>
/// <param name="Sql">Executed SQL text.</param>
/// <param name="Succeeded">Whether it completed without error.</param>
/// <param name="RowCount">Rows returned or affected, when known.</param>
/// <param name="Elapsed">Execution duration.</param>
public sealed record HistoryEntry(DateTimeOffset At, Guid ProfileId, string Sql, bool Succeeded, long? RowCount, TimeSpan Elapsed);
