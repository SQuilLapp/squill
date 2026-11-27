# P21-65 - Presentation: test data generator for a table

Status: todo
Depends on: none

## Goal
Fill a table with N rows of plausible test data, respecting types, nullability, unique keys and foreign keys (DBeaver's data generator, DataGrip's "Generate data").

Exists today: the import runner (`ImportRunner`: batched inserts per engine, savepoints inside a user transaction, rejected rows, production confirmation, P18-24), the type catalogue and column types (`docs/column-types.md`), the schema snapshot with FKs.

## Design decisions (final)
- `TestDataGenerator` (pure, seeded `Random` for repeatable output): per column a generator chosen from type and name hints (`email`, `name`, `city`, `phone`, `url`, `created_at`, ...): integers in range, decimals by precision/scale, text within max length (lorem words or name/email lists embedded as small static arrays, English only), dates in the last 5 years, booleans, UUIDs, JSON `{}` objects, binary random bytes; `NULL` ratio for nullable columns (default 10 %).
- Auto-increment / identity / generated columns are skipped; unique and PK columns get unique values (sequence or set check); FK columns take values from the parent's existing keys (read once, capped at 100,000 keys; empty parent -> error before running).
- `TestDataViewModel`: table, row count (1 ... 1,000,000), per-column generator override and null ratio, preview of 10 rows, `Generate` through `ImportRunner` (same batching, transaction, rejects and production confirmation).

## Out of scope
- Heads (P21-66), filling several tables in FK order at once.

## Tasks
- [ ] Tests: repeatable with a seed, lengths and ranges, uniqueness, FK values from parent, identity skipped, preview; Docker-gated insert of 10,000 rows on each server engine.

## Acceptance criteria
- Recipe 2 common acceptance.

## Risks
- `CHECK` constraints are not modelled: rejected rows are reported by the import runner, not prevented.

## Open points (defaults)
- None.

## Suggested commit message
Generate test data for a table through the import runner
