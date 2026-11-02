# ADR 0004 - Schema designer and ER diagram

- Status: accepted
- Date: 2026-11-02

## Context
Phase 7 adds a table designer (create and alter tables) and an ER diagram for four dialects, on both heads, without an ORM and without reflection (Native AOT).

## Decision
- **Draft versus original.** The designer edits a draft copy of the table next to the original loaded from the schema reader. Each column, index and foreign key keeps an `OriginalName`, so a rename is told apart from a drop plus an add.
- **Abstract operations, hand-written generators.** `TableDiff` compares original and draft and produces abstract `SchemaOperation`s. One hand-written generator per dialect (`Squill.Sql/Ddl`) translates them to SQL. No ORM, no reflection.
- **SQL Server default constraints** have system-generated names, so they are looked up at run time before a column is altered or dropped.
- **SQLite** uses native `ALTER TABLE` when it supports every change. Otherwise the whole edit is one table rebuild: `PRAGMA foreign_keys = OFF`, then create, copy, drop and rename inside a transaction, then `PRAGMA foreign_key_check` and `PRAGMA foreign_keys = ON`. The leading and trailing `PRAGMA`s run outside the transaction.
- **MySQL / MariaDB** DDL is not transactional: a failure leaves the earlier statements applied. The designer shows a warning to the user in that case.
- **ER layout** is layered, deterministic and pure (same input, same boxes). Edges are routed orthogonally and may cross boxes. Box positions are persisted per connection profile.

## Not covered
Views, triggers, check constraints, partitions, moving tables between schemas, comments and collations.

## Consequences
- A new dialect needs one generator and tests; the diff and the view-model do not change.
- SQLite rebuilds copy all rows: large tables take time.
- Reopening condition: users need one of the uncovered object kinds, or edge routing proves unreadable on real schemas.
