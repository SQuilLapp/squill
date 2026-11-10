# P14-18 - End-to-end verification: completion through the query tab, and the manual checklist

Status: todo
Depends on: P14-08 ... P14-17

## Goal
Prove the whole path (query tab -> session schema -> providers -> editor adapter) with the real Presentation classes, and give the owner a checklist for the part nobody can automate: the popup in the two heads. Procedure and report format: [phase-11/verification-recipe.md](../phase-11/verification-recipe.md) sections 2 and 3; the report is `docs/verification/completion.md` (the owner ticks it, like [plan.md](../../verification/plan.md)).

## Design decisions (final)
- Automated, `tests/Squill.Presentation.Tests/Completion/CompletionFlowTests.cs` over the SQLite sample schema (no container): a `QueryTabViewModel` with a stub `ISqlEditor` (`StubSqlEditor`), the real session, then `editor.SetCompletionProvider`'s provider called at `select * from customers c join |`: first row is the `orders` relation, applying it to the editor text (replace range + insert) gives text that **runs** through `RunCommand` and returns rows; keyword case from a fake settings store (`Lower` gives `left join`); functions present in a SELECT list; schema refresh after `CREATE TABLE` (the query tab already refreshes; the new table appears in the popup); a request on a script of 100,000 lines sliced to the current statement answers fast (use `LoomSqlProviders`' slicing only if reachable from tests, otherwise skip and say so).
- Manual checklist `docs/verification/completion.md`, both heads (WinUI first, Avalonia smoke), `SQUILL_LANGUAGE=en` and once `fr`, against the SQLite sample and one server engine: type `select * from customers c join ` (popup opens by itself, join row first, Enter inserts table + alias + ON in one go, caret after the condition, Ctrl+Z reverts everything in one step); `left join`, `right join`, `full join`, `cross join` (no ON), `natural join`; a table with two keys (two rows); self join; a table without a key (plain row only); typing `ord` filters join rows by table name; `join | on a.x = b.y` (caret before an existing ON: no condition inserted); typing `on ` by hand gives the same conditions; a table in another schema inserted qualified; a column shared by two tables inserted qualified; docs panel on a table, a column, a join row and a function; keyword case Upper / Lower / Preserve (change it in the settings, wait a few seconds, new popup); `sel` + Tab expands the snippet and Tab moves between stops, Escape leaves; dark and light theme; a 5,000-table connection popup opens without a visible stall (use the stress schema tool of phase 7, `ErStressScenario`, if it can create one); the popup after a space does not accept an item when the space is typed (P14-10 risk). Boxes unchecked, `Result` column blank, WinUI popup details that differ from Avalonia written under "Notes".
- Bugs found: fix in this commit when under ~30 lines with a regression test, else new card `P14-20` onwards (added to the index).

## Out of scope
- Docs and ADR (P14-19).

## Tasks
- [ ] Flow tests, `docs/verification/completion.md`, clean-up (`dotnet build-server shutdown`, executables closed).

## Acceptance criteria
- Recipe section 4. Status `automated done, manual pending` until the owner ticks the checklist.

## Suggested commit message
Verify completion end to end and add the manual checklist
