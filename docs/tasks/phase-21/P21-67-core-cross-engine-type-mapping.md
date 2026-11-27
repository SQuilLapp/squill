# P21-67 - Core: neutral column type model and the SQLite to PostgreSQL mapping

Status: todo
Depends on: none

## Goal
First card of the cross-engine schema compare. **Do not duplicate the analysis**: it is in "Cross-engine schema compare: what it would need" (phase 20 section of [the task index](../README.md)) and `docs/ideas.md`. This phase builds its recommended **first phase only: one pair, SQLite to PostgreSQL, one way, tables, columns, primary keys, foreign keys and indexes**. This card covers points 1 and 2 of that list (neutral types, identifier rules) for that pair.

Exists today: `DataTypeCatalog` per dialect, `docs/column-types.md`, `SqlQuoting` / P19-01 reserved-word quoting, `SchemaComparer` (same dialect).

## Design decisions (final)
- `Squill.Core/Schema/CrossEngine/NeutralType` (record: family `Integer`, `Decimal`, `Float`, `Text`, `Binary`, `Boolean`, `Date`, `Time`, `DateTime`, `DateTimeOffset`, `Json`, `Uuid`, `Unknown`; length, precision, scale, auto-increment flag).
- `ITypeMapper` with `ToNeutral(dialect, typeText)` and `FromNeutral(dialect, NeutralType) -> (typeText, Loss?)`; implementations for SQLite (affinity rules: `INTEGER PRIMARY KEY` = 64-bit identity, declared type names honoured when known: `VARCHAR(n)`, `BOOLEAN`, `DATETIME`, `JSON`; anything else by affinity) and PostgreSQL (from the existing catalogue).
- `Loss`: a typed warning (`LengthUnknown`, `PrecisionUnknown`, `AffinityGuess`, `NoEquivalent`) with a message code (P10-09), never a silent change.
- Identifier mapping for the pair: SQLite names kept as is, quoted in PostgreSQL when not lower-case-safe (P19-01 rules); SQLite has no schema -> target schema chosen by the user (default `public`).

## Out of scope
- The other ordered pairs, defaults/checks/generated columns normalization beyond "copied as text with a warning", comparer (P21-68), script (P21-69).

## Tasks
- [ ] Table-driven tests for every SQLite declared type in the verification scenario and `docs/column-types.md`, and for PostgreSQL types back to neutral.

## Acceptance criteria
- Recipe 2 common acceptance.

## Risks
- SQLite accepts any type text: the mapping must never throw; unknown -> `Text` with `AffinityGuess`.

## Open points (defaults)
- Taken from the index section, point 8: one-way (default), lossy changes need an explicit accept in the mapping review (default yes), first pair SQLite to PostgreSQL (default).

## Suggested commit message
Add a neutral column type model and the SQLite to PostgreSQL mapping
