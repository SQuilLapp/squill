# P15-03 - Core and Data: transaction model and probe contract

Status: done
Depends on: none

## Goal
The vocabulary (mode, state) and the optional connection capability that tells the truth about the server-side transaction. Recipe: section 2 (decisions 2, 3, 4), section 6. Look at `IPlanReader` (`src/Squill.Data/IPlanReader.cs`) and how it is optional (`connection as IPlanReader`).

## Design decisions (final)
- `Squill.Core/Transactions/`: `public enum TransactionMode { AutoCommit, Manual }`, `public enum TransactionState { None, Open, Failed }`, `public readonly record struct TransactionProbeResult(bool? InTransaction, bool Failed)` (`InTransaction` null = the engine cannot tell; `Failed` true = the transaction is aborted / doomed). Static `TransactionProbeResult.Unknown`.
- `Squill.Data/ITransactionProbe.cs`: `public interface ITransactionProbe { ValueTask<TransactionProbeResult> ProbeAsync(CancellationToken cancellationToken); }`. Contract in the XML doc: **never throws for a server or driver error** (returns `Unknown`), only `OperationCanceledException`; cheap (one round trip at most); does not change the transaction; implemented by the four connections of SQLite, PostgreSQL, MySQL, SQL Server; **a connection without it does not support manual transactions** (Oracle bridge).
- No change to `IDatabaseConnection`.

## Out of scope
- Implementations (P15-04 ... P15-07).

## Tasks
- [x] Types, XML docs, a `tests/Squill.Core.Tests` test for `Unknown`.

## Acceptance criteria
- Common acceptance of the recipe, section 9.

## Suggested commit message
Add the transaction mode, state and probe contract
