# Transactions and production confirmation verification (P15-19)

Status: **automated done, manual pending.** The owner ticks the boxes below; the `Result` column stays blank until then.

## Automated (done)

`tests/Shared/TransactionFlowScenario.cs` drives, per engine, a real `WorkspaceViewModel` with a real `SessionManager`, the real provider, a live connection and a **second connection** that plays "another client" (it is what proves a commit or a rollback reached the server). Dialogs are a scripted fake `IDialogService`. One test class per driver project (`TransactionFlowTests.cs`): SQLite always runs, PostgreSQL / MySQL / SQL Server are Docker-gated (`rtk pwsh eng/run-docker-tests.ps1 -Engine <engine> -Filter '--filter-class *TransactionFlowTests'`).

Covered on all four engines unless stated:

- `Manual` mode: an `INSERT` opens a transaction (pending count 1), `Valider` makes the row visible to the second connection and writes a `COMMIT` history entry, `Annuler` removes it and writes `ROLLBACK`; the mode stays `Manual`.
- A `SELECT` in `Manual` mode opens nothing (`State` stays `None`, both buttons disabled).
- A script of three statements whose second fails: PostgreSQL goes `Failed` (count 1, the third never runs, the next statement is refused with `tx.failed`, `Valider` disabled, `Annuler` enabled); the others stay `Open` with count 1 and accept further statements; `Annuler` clears everything.
- `BEGIN` / `BEGIN TRANSACTION` typed in `AutoCommit` mode opens a transaction (mode unchanged, count 0 then 1), typed `ROLLBACK` and `COMMIT` close it.
- DDL inside an open transaction: MySQL asks (`tx.implicitCommit.*`); declined, nothing is sent and the table is absent; accepted, the earlier insert is committed and the state is `None`. SQLite, PostgreSQL, SQL Server: the `CREATE TABLE` runs in the transaction (count 2) and `Annuler` removes it.
- A statement that cannot run in a transaction is refused with `tx.cannotRun` and never sent (PostgreSQL `VACUUM`, SQL Server `CREATE DATABASE`, SQLite `VACUUM`; MySQL has none, it commits implicitly instead).
- A grid edit committed through the query tab's result editor inside an open transaction, then `Annuler`: the earlier insert and the grid edit are both gone (count 2 before). The table designer apply (`CREATE TABLE`) the same way (not on MySQL, where it is the implicit-commit case above).
- `Analyser` inside an open transaction leaves it open with its count (PostgreSQL, MySQL, SQL Server; SQLite has no `Analyser`).
- Two tabs on one session share the transaction: tab B's `INSERT` is rolled back by tab A's `Annuler`, the journal names tab B.
- Closing the last tab, switching to `AutoCommit`, `PrepareExitAsync` and `Déconnecter` with an open transaction ask; `Retour` keeps everything (tab, mode, state), `Annuler` rolls back (second connection sees nothing), `Valider` commits; closing the session itself (`SessionManager.CloseAsync`) rolls back.
- Production profile: `DELETE FROM t` without `WHERE` asks (kind `Run`) with the required word (database name; SQLite file name) and, except on SQLite, a planner figure; declined sends nothing and the three rows are still there; accepted with the transaction option switches the session to `Manual` and the delete waits (`Open`, count 1); `Valider` asks again (kind `Commit`), declined keeps the transaction, accepted commits; a `SELECT` never asks. Staging profile: the same `DELETE` runs without any dialog.

Not covered by the scenario (and why): the grid commit and the designer apply **on a Production profile** are covered by the Presentation tests of P15-13 with fake connections; the destructive-statement matrix of `ProductionGuard` and the figure rules of `ImpactEstimator` by P15-11 / P15-12 (`ImpactEstimateScenario` on the engines); MySQL MyISAM tables (not transactional) are not detected. The 3 s cap of the impact estimate, a lost connection (SSH tunnel drop, server restart) and the idle duration shown by the status bar are not simulated.

## Not driven

No agent session drove or looked at the WinUI or Avalonia windows for this phase, nor compared them with `docs/design/mockups/M03-Workspace-clair-Windows.png`, `M04-Editeur-clair-Windows.png`, `M15-Confirmation-clair-Windows.png` and `M15-Confirmation-sombre-Windows.png`. Earlier cards report what was not checked: Avalonia screens were not compared with the mockups by eye; AltGr chords, the dark theme and the `Failed` variants were not checked in WinUI. Everything in the checklist below is open.

## How to prepare

- SQLite: open a new SQLite profile on a copy of a file created with `tests/Shared/SampleSchema/sqlite.sql`.
- PostgreSQL / MySQL / SQL Server: `rtk pwsh eng/run-docker-tests.ps1 -Engine postgres -Keep` (or `mysql`, `sqlserver`) starts the container with `tests/Shared/SampleSchema/<engine>.sql` loaded, prints host, port, user and password and waits for Enter. Create a profile with those values. At the end press Enter in that terminal, then `podman ps -a` must show nothing from this run and `podman machine stop`.
- Make a **second profile on the same database with the environment `Production`** (and a third with `Staging`) for the production sections. SQLite: the same file, three profiles.
- Start the WinUI head with `SQUILL_LANGUAGE=fr` (then once with `en`), then the Avalonia head for a smoke pass. Do each section once in the light theme and once in the dark theme.
- Scratch table used below, run once in auto-commit: `create table tx_demo (id integer primary key, name varchar(50))` (SQL Server and the others accept it as is). Statements named "insert" are `insert into tx_demo (id, name) values (<next id>, 'x')`.
- "Pill" = the chip `N instruction(s) non validée(s)` in the query toolbar; "status text" = the transaction segment of the status bar.

## Checklist

Columns: WinUI / Avalonia = tick when it behaves as described in that head. Result: `ok`, `fixed in <commit>`, `bug -> <card>`, `not driven (why)`.

### SQLite

| Step | WinUI | Avalonia | Result | Note |
|---|---|---|---|---|
| 1 Open the query tab: the toolbar shows `Auto-commit | Manuel` with `Auto-commit` selected, no pill, no `Valider` / `Annuler`; the status bar has no transaction text | [ ] | [ ] | | |
| 2 Click `Manuel`: status text `Transaction manuelle`, still no pill (nothing open) | [ ] | [ ] | | |
| 3 Run an insert: pill `1 instruction non validée`, `Valider` and `Annuler` appear (tooltips name `Ctrl+Alt+C` / `Ctrl+Alt+Z`), status text `Transaction ouverte depuis moins d'une minute · 1 instruction` | [ ] | [ ] | | |
| 4 Click `Annuler`: pill and buttons disappear, status `Transaction manuelle`, `select * from tx_demo` shows no row, the history panel has a `ROLLBACK` line | [ ] | [ ] | | |
| 5 Insert again, press `Ctrl+Alt+C`: committed, row visible, history has `COMMIT`. Insert again, press `Ctrl+Alt+Z`: rolled back. On an AZERTY / AltGr layout check the shortcut does not type a character in the editor (risk 10) | [ ] | [ ] | | |
| 6 Run `select * from tx_demo` in `Manuel` with nothing open: no pill, no buttons, status stays `Transaction manuelle` | [ ] | [ ] | | |
| 7 Run an insert, then `vacuum`: the result shows the refusal (`Cette instruction (VACUUM) ne peut pas s'exécuter dans une transaction`), the pill still says 1, `Annuler` | [ ] | [ ] | | |
| 8 Run `create table tx_ddl (id integer primary key)` in `Manuel`, then `Annuler`: the table is gone from the explorer after a refresh | [ ] | [ ] | | |
| 9 Switch to `Auto-commit`, type `begin`, run: pill `0 instruction non validée`, buttons appear. Insert, type `rollback`, run: all gone | [ ] | [ ] | | |
| 10 Open a result of `select * from tx_demo` (editable), insert a row from the grid with the `Manuel` mode and an earlier insert open, commit the grid: the pill counts both (2), `Annuler` removes both | [ ] | [ ] | | |
| 11 With a transaction open, close the query tab: the dialog `Fermer le dernier onglet de la connexion ?` lists the connection and the statement; `Retour` keeps the tab, `Annuler les modifications` rolls back and closes | [ ] | [ ] | | |
| 12 With a transaction open, click `Auto-commit`: `Repasser en auto-commit ?` with `Valider` / `Annuler les modifications` / `Retour` | [ ] | [ ] | | |
| 13 With a transaction open, `Déconnecter`: `Se déconnecter ?`; then close the window: `Quitter SQuilL ?`. `Retour` leaves the application running with the transaction open; `Valider` commits and exits | [ ] | [ ] | | |
| 14 Open two query tabs on the connection, open a transaction in tab A: tab B shows the same pill and buttons; tab B's insert is rolled back by tab A's `Annuler` | [ ] | [ ] | | |
| 15 The `Production` profile: `delete from tx_demo` (no `WHERE`) opens the production dialog (see "Production dialog" below), **without** a figure (SQLite gives no estimate) and the confirm button says `Exécuter`; the typed word is the file name | [ ] | [ ] | | |
| 16 The `Staging` profile: the same delete never asks anything | [ ] | [ ] | | |

### PostgreSQL

| Step | WinUI | Avalonia | Result | Note |
|---|---|---|---|---|
| 1 Steps 1 to 6 of SQLite (toggle, insert, pill and status text, `Annuler`, `Valider` with `Ctrl+Alt+C`, `SELECT` leaves nothing open) | [ ] | [ ] | | |
| 2 In `Manuel`, run the script `insert into tx_demo (id, name) values (1, 'a'); insert into tx_demo (id, name) values (1, 'dup'); insert into tx_demo (id, name) values (3, 'c')`: the second fails, the pill shows the `Failed` variant (`Transaction en échec : annulez-la` in the status bar, red pill), the third did not run, `Valider` is disabled | [ ] | [ ] | | |
| 3 In that state run `select 1`: refused with `La transaction est en échec : annulez-la avant de continuer.`; the plan buttons are refused too. `Annuler` returns to `Transaction manuelle` and the table is empty | [ ] | [ ] | | |
| 4 Insert, run `vacuum`: refused with the `tx.cannotRun` message, the transaction is not `Failed` | [ ] | [ ] | | |
| 5 Insert, `Analyser` a select: the plan tab opens, the pill still says 1 and `Annuler` still works | [ ] | [ ] | | |
| 6 Run `create table tx_ddl (id int)` in `Manuel`, `Annuler`: the table is gone (DDL is transactional) | [ ] | [ ] | | |
| 7 Steps 9 to 14 of SQLite (typed `begin`, grid commit, close tab, switch mode, disconnect and exit, two tabs) | [ ] | [ ] | | |
| 8 Production profile, `delete from tx_demo`: the dialog shows the figure (`Elle touchera environ N lignes (estimation du planificateur)`), the confirm button reads `Supprimer N lignes`; see "Production dialog" | [ ] | [ ] | | |
| 9 In another client (`psql`) `select count(*) from tx_demo` while a transaction is open: the uncommitted row is not visible; after `Valider` it is | [ ] | [ ] | | |

### MySQL

| Step | WinUI | Avalonia | Result | Note |
|---|---|---|---|---|
| 1 Steps 1 to 6 of SQLite | [ ] | [ ] | | |
| 2 Insert, then `create table tx_ddl (id int primary key)`: the dialog `Valider la transaction en cours ?` (`Cette modification de schéma valide implicitement la transaction ouverte sur ce serveur. Valider et appliquer ?`) appears **before** anything is sent | [ ] | [ ] | | |
| 3 Decline: the status bar says the run was cancelled, the pill still says 1, the table does not exist. Run it again and accept: the table exists, the earlier insert is committed (check from another client), the pill and buttons are gone, status `Transaction manuelle` | [ ] | [ ] | | |
| 4 Production profile, `drop table tx_ddl`: the production dialog has **no** transaction checkbox (DDL cannot be rolled back); the typed word is the database name | [ ] | [ ] | | |
| 5 Insert, `Analyser` a select: the plan opens, the transaction is still open | [ ] | [ ] | | |
| 6 Steps 9 to 14 of SQLite (the typed statement is `begin`) | [ ] | [ ] | | |
| 7 Production profile, `delete from tx_demo`: the figure is present (`environ N lignes`) | [ ] | [ ] | | |

### SQL Server

| Step | WinUI | Avalonia | Result | Note |
|---|---|---|---|---|
| 1 Steps 1 to 6 of SQLite | [ ] | [ ] | | |
| 2 Insert, then `create database tx_nope`: refused with the `tx.cannotRun` message, no database created | [ ] | [ ] | | |
| 3 In `Manuel`, run `insert ...; insert <duplicate id>; insert <new id>` : the second fails, the transaction stays open (not `Failed`), the third did not run (the script stops); the pill counts only the successful statements; a further insert works | [ ] | [ ] | | |
| 4 `create table tx_ddl (id int primary key)` in `Manuel` then `Annuler`: the table is gone | [ ] | [ ] | | |
| 5 Insert, `Analyser` a select: the plan opens and the transaction (and its pill) is intact; `Annuler` still removes the insert | [ ] | [ ] | | |
| 6 Typed statement `begin transaction` in `Auto-commit` mode opens a transaction; typed `rollback` closes it; steps 10 to 14 of SQLite | [ ] | [ ] | | |
| 7 Production profile, `delete from tx_demo`: the figure is present | [ ] | [ ] | | |

### Production dialog (run on PostgreSQL, then smoke on the others; compare with M15 light and dark)

Use the `Production` profile: header chip, tab marker and chrome are red. `delete from tx_demo` with no `WHERE`.

| Step | WinUI | Avalonia | Result | Note |
|---|---|---|---|---|
| 1 Header: red band `PRODUCTION · <connection> / <database>` with a warning icon; title `Supprimer toutes les lignes de tx_demo ?` | [ ] | [ ] | | |
| 2 Message: the fact `n'a pas de clause WHERE` is bold, the figure (not SQLite) is in the danger color and worded as an estimate; in `Manuel` mode the sentence `La transaction manuelle est active : vous pourrez encore annuler avant de valider.` is present | [ ] | [ ] | | |
| 3 The statement sits in a mono box with a red left border; a script of 7 statements lists five then `+ 2 autres` | [ ] | [ ] | | |
| 4 The typed field has the database name as placeholder (`Saisissez <nom> pour confirmer`); the red confirm button (`Supprimer N lignes`, or `Exécuter` without a figure) is **disabled** until the typed text matches, ignoring case and surrounding spaces; `Annuler` is always enabled | [ ] | [ ] | | |
| 5 Press `Entrée` in the dialog: it cancels (nothing is sent; with the confirm button enabled too: `Entrée` never confirms). The hint `Entrée = Annuler` is shown | [ ] | [ ] | | |
| 6 Checkbox `Exécuter dans une transaction et ne pas valider automatiquement`: checked by default in `Auto-commit`; in `Manuel` it is checked and **disabled**. Confirm with it checked in `Auto-commit`: the toolbar switches to `Manuel` and the pill appears | [ ] | [ ] | | |
| 7 Confirm with the box unchecked in `Auto-commit`: the delete is committed immediately, no pill | [ ] | [ ] | | |
| 8 `Valider` on the `Production` connection asks again (title `Valider la transaction en production ?`, journal listed, no typed field) | [ ] | [ ] | | |
| 9 An `update tx_demo set name = 'z' where id = 1` on Production asks without the typed field (confirm button enabled); a `select` never asks; `Expliquer` never asks; `Analyser` keeps its own plain confirmation | [ ] | [ ] | | |
| 10 Grid commit and designer apply on Production ask (`Écrire N modifications en production ?`, `Appliquer N changements de schéma en production ?`); this is a behavior change of this phase | [ ] | [ ] | | |
| 11 The `Staging` profile never asks | [ ] | [ ] | | |
| 12 `SQUILL_LANGUAGE=en`: every text above is English; dark theme: the dialog colors match M15 dark | [ ] | [ ] | | |

### Window and presentation (once, WinUI first, Avalonia smoke)

| Step | WinUI | Avalonia | Result | Note |
|---|---|---|---|---|
| 1 Compare the toolbar group with M04 (`Auto-commit | Manuel`, pill, `Valider Ctrl+Alt+C`, `Annuler Ctrl+Alt+Z`) and the status bar with M03 / M04 (`Transaction ouverte depuis 4 min · 2 instructions`) | [ ] | [ ] | | |
| 2 Narrow the window to about 800 px: the toolbar group stays usable (nothing cut off, nothing overlapping the run button) | [ ] | [ ] | | |
| 3 Leave a transaction open for a minute: the duration in the status bar moves on from `moins d'une minute` to `1 min` without any action | [ ] | [ ] | | |
| 4 Dark theme: pill, buttons and the `Failed` variant (PostgreSQL) keep their contrast | [ ] | [ ] | | |
| 5 `SQUILL_LANGUAGE=en`: toggle, pill (`1 uncommitted statement`), status text and the three close questions are English | [ ] | [ ] | | |
| 6 Kill the connection server-side while a transaction is open (`select pg_terminate_backend(...)` from another client): `Valider` shows the error; the app does not crash and does not reconnect by itself (known limit) | [ ] | [ ] | | |

## Known limits (so nothing here surprises you)

- The transaction belongs to the **session** (the connection), not to the tab: every tab of a connection mirrors it, and a statement run in tab B while tab A has a transaction open runs inside it. The journal records the tab title.
- `Manual` mode opens the transaction lazily, right before the first statement that is not read-only; a `SELECT` alone never opens one.
- MySQL commits implicitly on DDL (the app asks first) and MyISAM tables are not transactional (not detected).
- `SET autocommit`, `SET IMPLICIT_TRANSACTIONS` and similar statements are allowed and only picked up by the probe; the app does not model them.
- A connection lost mid-transaction (SSH tunnel drop, server restart) is not reconnected; the server has rolled back, `Valider` fails with the error.
- A long transaction on a production server holds locks (PostgreSQL: blocks vacuum); the status bar shows the duration, there is no timeout or automatic rollback.
- The impact figure is a planner estimate, capped at 3 s; it is absent on SQLite, for `INSERT ... VALUES`, for a refused or timed-out plan, and never blocks the run.
- Oracle keeps auto-commit (the toggle is disabled with the tooltip `Ce moteur ne gère pas les transactions manuelles.`). Two tabs running statements at the same time on one connection stays a known limit of the app.
- No isolation-level choice, no read-only transaction, no named savepoints in the UI, no `Staging` confirmation, no per-profile default mode.
