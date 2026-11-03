# P11-12 - Presentation: several results and messages per statement

Status: todo
Depends on: P11-11

## Goal
`QueryTabViewModel` shows every result set of a statement and a "Messages" entry, instead of the first set only.

## Design decisions (final)
- Read how `QueryTabViewModel.RunStatementAsync` and `ResultViewModel` model results today (one result per executed statement); extend the model rather than replace it: a statement now yields N `ResultViewModel`s (one per result set, titled `statement title (n/N)` through the catalog with `FormatPlural`-style keys) plus, when `reader.Messages` is not empty, one result of kind `Messages` (new member of `ResultKind`) holding the lines. Editable-source analysis (P8-12) only applies to a statement that returned exactly one set.
- The "rows affected" status line keeps working for non-query statements; history stores the first set's row count as today.
- A failing statement keeps the results already shown (unchanged behaviour; add a test).

## Out of scope
- Rendering in the heads (P11-13, P11-14).

## Tasks
- [ ] View-model changes, catalog keys fr/en, tests with a fake reader that returns two sets and messages, and one that returns messages only.

## Acceptance criteria
- `rtk dotnet build Squill.slnx -c Release -warnaserror` 0 warnings; `rtk bypass dotnet test --solution Squill.slnx -c Release` green (localization guards included).

## Suggested commit message
Show every result set and message of a statement in the query tab
