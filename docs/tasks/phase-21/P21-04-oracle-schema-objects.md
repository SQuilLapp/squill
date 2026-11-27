# P21-04 - Data.Oracle: indexes, foreign keys, defaults, routines, triggers and view definitions

Status: todo
Depends on: P21-03

## Goal
The Oracle schema snapshot carries what the four other engines carry, so the explorer, ER diagram, completion joins and designer have data.

Exists today: the Oracle schema reader lists schemas, tables, views, columns (`NUMBER(p,s)`, `VARCHAR2(n)`), nullability and primary key over `all_objects`, `all_tab_columns`, `all_constraints`.

## Design decisions (final)
- Read through plain SQL over the bridge (the bridge stays a generic executor): indexes `all_indexes` / `all_ind_columns`; foreign keys `all_constraints` type `R` joined to the referenced key, with `delete_rule` mapped to `ReferentialAction`; defaults `all_tab_columns.data_default` (a `LONG`: read as text, trimmed); identity `all_tab_identity_cols` -> auto-increment (P7-01 model); routines `all_procedures` + `all_source` (functions, procedures, packages listed as routines); triggers `all_triggers`; view definitions `all_views.text`; materialized views `all_mviews`.
- Same filtering of system schemas as today.
- Sequences and synonyms are not in the schema model (not supported anywhere): not read.

## Out of scope
- DDL generation (P21-06).

## Tasks
- [ ] Unit tests with the fake bridge (mapping of each catalogue row).
- [ ] Docker-gated test on a sample Oracle schema (two tables with a FK, an index, an identity, a view, a function, a trigger) added to the verification scenario.

## Acceptance criteria
- Recipe 2 common acceptance; `-Engine oracle` green; the ER diagram opens on the sample schema (checked through the ER view-model test, not by hand).

## Risks
- `LONG` columns cannot be read after other columns in some driver versions: select `data_default` in its own query if needed.

## Open points (defaults)
- None.

## Suggested commit message
Read Oracle indexes, foreign keys, defaults, routines, triggers and view definitions
