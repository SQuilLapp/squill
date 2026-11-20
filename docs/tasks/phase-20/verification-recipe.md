# Phase 20 recipe - closing the verification left open by phase 19, and multi-table delete

Shared decisions for every P20 card. A card says "recipe N" for a section here. Audit of the repository at `806dc3d` (phases 18 and 19 merged into `main`, PR 42).

## 1. Where the work comes from (final)

| Source | What it says | Cards |
|---|---|---|
| ADR 0016 "Known limits", Done notes of P19-09 ... P19-15 | the window could not be clicked (no UI automation), so M09, M10 and M09 ... M12 on Avalonia, the dark / English WinUI filter and palette, the completion popup, the designer (M07), the ER diagram (M08) and the pointer states were never driven | P20-01, P20-10 ... P20-13, P20-16 ... P20-20 |
| P19-20 Done notes | the light theme fix was found by reading code: no test, no capture; a scan of `src/Squill.App.WinUI` shows brush lookups through `Application.Current.Resources[...]` that still follow the system theme (`SearchBox`, `CommandPalette`, `DataGridView`) | P20-02, P20-15 |
| P19-18 Done notes, ADR 0016 | the PostgreSQL column widths, the sessions table, the singular role members and the dialog theme were never seen; the `Failed` variant, plan rows and the 400-row cut notice were never driven; Podman was not running | P20-03, P20-14, P20-17 |
| P19-13 Done notes | a completion popup with a single entry stayed open after `Ctrl+Enter` (WinUI), not investigated | P20-12 |
| `docs/ideas.md` (2026-11-17) | multi-table delete | P20-04 ... P20-09, P20-21 |

Not planned: cross-engine schema compare (see "Cross-engine schema compare: what it would need" in the README phase 20 section), everything in ADR 0015 "Not built".

## 2. Rules for fix and feature cards

- Write the failing test first, see it fail, then implement (as in phase 19). A card whose premise is false in the code stops with a report (Done notes) and a proposed card fix.
- No `NoWarn`, no `#pragma warning disable`, no `.editorconfig` exemption. CA and CS1591 rules apply; `shortcut:` comments are allowed only with the form in the repository conventions.
- Common acceptance for code cards: `rtk dotnet build Squill.slnx -c Release -warnaserror` 0 warnings (head cards also `Squill.Windows.slnx`), `rtk bypass dotnet test --solution Squill.slnx -c Release` green (the two known timing tests may be re-run alone once), catalog parity and string scanner green, XML doc check by grep. Head code changed: the AOT publish of that head with zero IL2xxx/IL3xxx from SQuilL code (`eng/check-aot-warnings.ps1`), and the app starts. Close everything started (app, containers, `dotnet build-server shutdown`).

## 3. Rules for agent verification cards (P20-10 ... P20-20)

- Procedure of `visual-spec.md` section 11 and recipe 3 of [phase 19](../phase-19/debug-polish-recipe.md): `tools/visual/` (P19-07, P19-08), `SQUILL_NO_UPDATE_CHECK=1`, theme through `<dir>/settings.json`, language through `SQUILL_LANGUAGE`, 1440 x 900 logical px (pass `-Width 2160 -Height 1350` to `Capture-Window.ps1` on a 150 % display, see P19-12).
- Drive the window with `tools/visual/Drive-Window.ps1` (P20-01), not with a scratch script. Always **read the PNG** (Read tool) and the composite of `Compare-Mockup.ps1`; never claim a match from an exit code.
- Per checklist step the agent drove: `Result` column text `agent: ok`, `agent: bug -> <card>` or `agent: not driven (<why>)`. **Never tick a WinUI / Avalonia box**, never edit a step's text; the Note says per screen which head, theme, language and mockup were compared and what differed. Captures are not committed.
- A difference one small edit fixes is fixed in the card (with a test where a pattern exists); a bigger one is reported and proposed as a card `P20-22` onwards (index row `todo` and card file added by the card's commit).
- Update only the `visual-spec.md` section 12 cells for what was really compared, with the card id.
- A step that cannot be driven stays `agent: not driven (<why>)`: that is an acceptable result, a guess is not. Stop rule for time: when the session passes about 50 % of its context, stop, record the rest as `not driven (time)` and say so in the Done notes (status `partial`); never stretch a claim.
- Everything started is closed: the app, containers (`podman ps -a` empty), `dotnet build-server shutdown`, the driver's helper processes.

## 4. Multi-table delete decisions (final)

- Scope: tables only (`TableKind.Table`), several of them, of **one connection session**, selected in the explorer; one action. Views, routines, triggers are not part of it. A mixed selection disables the command.
- One flow for one or many tables: `WorkspaceViewModel` gets `DropTablesCommand`; the existing single `DropTableCommand` becomes a call of it. Today the single drop runs `session.Connection.ExecuteAsync` directly, so it skips the production confirmation, the session transaction (journal, savepoint) and the implicit-commit question; the new flow goes through `ProductionRequestBuilder.ConfirmWriteAsync` (`ConfirmationKind.SchemaChange`), `SqlRunner.BeginScopeAsync` / `StatementScope` and the same implicit-commit question as the designer.
- Dialect: statements come from `DdlGenerators.For(dialect).DropTable(schema, name)` (quoting included). No `CASCADE`, ever: a table outside the selection that references a selected table is reported and **nothing is sent** (P20-04 computes it from the schema already loaded).
- Order: referencing tables are dropped before the tables they reference (topological order over the foreign keys inside the selection; a cycle keeps the selection order and the engine's error is reported).
- Atomicity: PostgreSQL, SQL Server and SQLite run the whole drop in one scope (own transaction, or a savepoint inside the user's open transaction): all or nothing. MySQL (and Oracle, whose DDL commits implicitly; `DdlGenerators.UsesTransactionalDdl` today says only MySQL, see risk in P20-05) cannot: the flow stops at the first failure and tells which tables were dropped and which failed.
- Confirmation: outside Production a plain confirmation lists the table names (one per line, qualified); on Production only the production dialog (statements listed, database name typed, because `DROP` is destructive) is shown, not both.
- Not built: drop of views, `CASCADE`, drag selection across connections, undo, dropping hidden tables.
