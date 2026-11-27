# P21-43 - Core: row comparer for data compare (merge by primary key)

Status: todo
Depends on: none

## Goal
First piece of data compare: compare the rows of two tables (same dialect) by primary key and classify them, streaming, without loading both tables in memory.

Exists today: structure compare only (`SchemaComparer`, `SchemaComparison`, P18-05) - ADR 0015 lists data compare as not built.

## Design decisions (final)
- `Squill.Core/Data/Compare/RowComparer` (pure): input two async row sequences **ordered by the same key** (`IAsyncEnumerable<object?[]>`), key column indexes, compared column indexes, a value comparer; output a stream of `RowDifference` (`OnlyInSource`, `OnlyInTarget`, `Changed` with the changed column indexes) and a summary (counts, identical count).
- Merge join on the key; key comparison uses an ordinal comparer for strings **only if both sides sort the same way**: the caller passes the ordering; P21-45 orders both queries by the key with a binary collation where the engine allows it (`COLLATE "C"`, `COLLATE Latin1_General_BIN2`, `BINARY`), else compares in memory after a full read with a cap.
- Value equality: numbers by value (`1` = `1.0` decimal), date-times by ticks, strings ordinal, byte arrays by content, `NULL` = `NULL`.
- A limit on reported differences (default 10,000) stops the comparison with a `Truncated` flag.

## Out of scope
- Sync script (P21-44), queries and UI (P21-45).

## Tasks
- [ ] Tests: identical, inserts, deletes, changes, composite keys, NULL handling, numeric equality across types, truncated, unsorted input detected (throws a clear exception).

## Acceptance criteria
- Recipe 2 common acceptance.

## Risks
- Collation-dependent ordering is the main trap: the unsorted-input check must catch it rather than produce wrong results.

## Open points (defaults)
- None.

## Suggested commit message
Add a streaming row comparer for data compare
