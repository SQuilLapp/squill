# Execution plan verification (P13-21)

Status: **automated done, manual pending.** The owner ticks the boxes below; the `Result` column stays blank until then.

## Automated (done)

`tests/Shared/PlanFlowScenario.cs` drives, per engine, a real `WorkspaceViewModel` with a real `SessionManager`, the real provider and a live connection (the query tab, the `Expliquer` / `Analyser` commands, the plan tab, `PlanLayout`). One test class per driver project (`Plans/PlanFlowTests.cs`): SQLite always runs, PostgreSQL / MySQL / SQL Server are Docker-gated (`rtk pwsh eng/run-docker-tests.ps1 -Engine <engine> -Filter '--filter-class *PlanFlowTests'`).

Covered on all four engines: `ExplainCommand` on the statement under the caret opens one plan tab (`<name> · Plan`) with non-empty rows and a layout without overlapping cards, and writes no history entry; a normal `Run` works after an explain and after a failed explain (unknown table: the server error is shown, no plan tab opens, the previous plan tab is kept); `Relancer` after the editor text was edited re-explains the old statement; parameters are asked (`@who` / `:who`) and then explained; closing the query tab closes its plan tab. SQLite: `Analyser` is disabled with the reason `plan.reason.noanalyze`, the command only sets the status text. PostgreSQL, MySQL, SQL Server: `AnalyzeCommand` on a select opens an actual plan; on an `UPDATE` it is refused and the rows are unchanged; a cancelled analyze (`select` that runs 30 s, cancelled after 0.5 s) leaves the connection usable for a normal `Run` (on SQL Server this proves the showplan state is reset). PostgreSQL: analyze of `select pf_write()` (a volatile function that inserts) fails inside `BEGIN READ ONLY` and leaves no row in `pf_log`.

Not covered by the scenario (and why): SQL Server has no "select that calls a writing function" (a UDF cannot modify tables); `select ... into` is refused by `PlanEligibility` before reaching the engine (unit-tested in `Squill.Sql.Tests`). MySQL has no analyze wrapper, `EXPLAIN ANALYZE` accepts only `select`. A PostgreSQL session with a user transaction already open (`25001` path) is covered by the P13-06 provider tests, not here.

## Not driven

No agent session drove or looked at the WinUI or Avalonia windows, nor compared them with `docs/design/mockups/M06-Plan-clair-Windows.png` / `M06-Plan-sombre-Windows.png`. Everything in the checklist below is open.

## How to prepare

- SQLite: open a new SQLite profile on a copy of a file created with `tests/Shared/SampleSchema/sqlite.sql`.
- PostgreSQL / MySQL / SQL Server: `rtk pwsh eng/run-docker-tests.ps1 -Engine postgres -Keep` (or `mysql`, `sqlserver`) starts the container with `tests/Shared/SampleSchema/<engine>.sql` loaded, prints host, port, user and password and waits for Enter. Create a profile with those values. At the end press Enter in that terminal, then `podman ps -a` must be empty and `podman machine stop`.
- Start the WinUI head with `SQUILL_LANGUAGE=fr` (then once with `en`), then the Avalonia head for a smoke pass. Statement used below ("the join"): `select customers.name, sum(orders.total) as t from orders join customers on customers.id = orders.customer_id where orders.total > 3 group by customers.name order by t desc limit 5` (SQL Server: `select top 5 ...` without `limit`; prefix the tables with `sample.` where the sample schema uses a schema).
- Seed a few rows first (`insert into customers (name, email) values ('a','a@x'),('b','b@x'),('c','c@x'); insert into orders (customer_id, total) select id, 5 from customers`), otherwise every plan reads empty tables.

## Checklist

Columns: WinUI / Avalonia = tick when it behaves as described in that head. Result: `ok`, `fixed in <commit>`, `bug -> <card>`, `not driven (why)`.

### SQLite

| Step | WinUI | Avalonia | Result | Note |
|---|---|---|---|---|
| 1 Type the join, caret in it, click `Expliquer`: a tab `Requête 1 · Plan` opens next to the query tab, with the query tab's environment color | [ ] | [ ] | | |
| 2 The three views `Arbre` / `Graphique` / `Texte`: table only, canvas above table, raw `EXPLAIN QUERY PLAN` rows in monospace (selectable) | [ ] | [ ] | | |
| 3 Cost-free plan: no relative-cost bars, no `%`, no `Coût` figure in the summary, cells show an en dash, nothing invented | [ ] | [ ] | | |
| 4 Click a row, then a card in the graph: selection follows in both, the details panel (right, 264 wide) shows the node | [ ] | [ ] | | |
| 5 Zoom `-` / `100 %` / `+` (and Ctrl+wheel): the graph scales, the table does not | [ ] | [ ] | | |
| 6 Edit the query text, click `Relancer` (and press Ctrl+Maj+E): the plan of the **old** statement is shown again | [ ] | [ ] | | |
| 7 `Analyser` is disabled and its tooltip gives the reason (not available on SQLite) | [ ] | [ ] | | |
| 8 Put the caret in an `insert` statement: `Expliquer` works, `Analyser` is disabled with a reason; put it in a `create table`: both disabled with a reason | [ ] | [ ] | | |
| 9 Status bar on the plan tab: engine message, `<n> nœuds`, server version | [ ] | [ ] | | |

### PostgreSQL

| Step | WinUI | Avalonia | Result | Note |
|---|---|---|---|---|
| 1 `Expliquer` on the join: plan tab, `Arbre` / `Graphique` / `Texte`, summary figures (`Coût`), relative-cost bars and `%`; status line `Plan estimé : l'instruction n'a pas été exécutée` | [ ] | [ ] | | |
| 2 `Hash` helper nodes are a dashed pill on the edge (`Hash 212 k · 18 Mo` style), not a card | [ ] | [ ] | | |
| 3 Selection sync table / graph / details; zoom; `Relancer` after editing the query text | [ ] | [ ] | | |
| 4 `Analyser` on the join: actual rows, `Temps`, `Boucles`, `Durée totale`, buffers; status line ends with `exécuté dans une transaction annulée` | [ ] | [ ] | | |
| 5 A misestimate (`Estimation N fois trop basse/haute`) shows as an amber pill and an `est. N · réel M ×factor` line on the card, if the sample produces one (else `not driven (no misestimate)`) | [ ] | [ ] | | |
| 6 `Analyser` on `update orders set total = 1`: disabled with the reason (writes data); rows unchanged | [ ] | [ ] | | |
| 7 `Analyser` on `select pg_sleep(60)`: cancel with the `Annuler` button of the query tab: status `Annulé`, then `select 1` runs normally | [ ] | [ ] | | |
| 8 Parameters: `select name from customers where name = @who`, `Expliquer`: the parameter dialog opens, then the plan (or the message that parameters are unsupported) | [ ] | [ ] | | |
| 9 Type `begin;` and run it, then `Analyser` on a select: it works, and the `begin` transaction is still open afterwards (`select txid_current_if_assigned()` or `commit;` succeeds without a warning) | [ ] | [ ] | | |
| 10 Production profile (environment `Production`): `Analyser` asks for confirmation first, `Expliquer` does not | [ ] | [ ] | | |
| 11 Run a generated 50-table join (`select 1 from customers c1 join customers c2 on c2.id = c1.id join customers c3 on c3.id = c2.id ...` up to `c50`): `Expliquer` stays usable (scroll, zoom out, selection, no freeze above ~2 s) | [ ] | [ ] | | |
| 12 Light and Dark theme: graph, pills, bars and legend (`Épaisseur = lignes`, `> 30 % ou alerte`, `> 60 %`) readable in both | [ ] | [ ] | | |

### MySQL

| Step | WinUI | Avalonia | Result | Note |
|---|---|---|---|---|
| 1 `Expliquer` on the join: plan with cost and estimated rows, no `Temps`, no `Lignes réelles` (an en dash, nothing invented) | [ ] | [ ] | | |
| 2 `Analyser` on the join: actual rows, loops and times from `EXPLAIN ANALYZE`; no `transaction annulée` in the status line (MySQL has no wrapper) | [ ] | [ ] | | |
| 3 `Analyser` on an `update`: disabled with the reason; rows unchanged | [ ] | [ ] | | |
| 4 Cancel of a long `Analyser` (`select count(*) from information_schema.columns a, information_schema.columns b, information_schema.columns c, information_schema.columns d`): status `Annulé`, a normal run works | [ ] | [ ] | | |
| 5 Selection sync, zoom, `Relancer`, `Texte` view shows the raw tree | [ ] | [ ] | | |
| 6 Production profile confirmation on `Analyser` | [ ] | [ ] | | |
| 7 Light and Dark | [ ] | [ ] | | |

### SQL Server

| Step | WinUI | Avalonia | Result | Note |
|---|---|---|---|---|
| 1 `Expliquer` on the join: plan with cost, estimated rows; `Texte` view shows the showplan XML | [ ] | [ ] | | |
| 2 After `Expliquer`, a normal `select 1` returns a result grid (not a plan): the connection left showplan mode | [ ] | [ ] | | |
| 3 `Analyser` on the join: actual rows, `Temps`, `Boucles`; status line ends with `exécuté dans une transaction annulée` | [ ] | [ ] | | |
| 4 Index suggestion: on a query that scans an unindexed column (`select * from orders where total = 5` after dropping the index on `total` if any) the `Suggestion d'index` card shows the engine's own `CREATE INDEX`; `Copier` puts it on the clipboard (paste it in a query tab); `Ouvrir dans l'éditeur` opens it in a new query tab | [ ] | [ ] | | |
| 5 Spill or misestimate warning on a card if the sample shows one (else `not driven`) | [ ] | [ ] | | |
| 6 A statement that cannot be analyzed (`update`, `select ... into x`): `Analyser` disabled, reason in the tooltip | [ ] | [ ] | | |
| 7 Cancel of a long `Analyser` (`select count_big(*) from sys.all_objects a cross join sys.all_objects b cross join sys.all_objects c cross join sys.all_objects d`), then `select 1` returns a grid | [ ] | [ ] | | |
| 8 Parameters: `select name from customers where name = @who` then `Expliquer`: dialog, then plan or the unsupported-parameters message | [ ] | [ ] | | |
| 9 Production profile confirmation on `Analyser` | [ ] | [ ] | | |
| 10 Light and Dark | [ ] | [ ] | | |

### Language and mockups (once, any engine)

| Step | WinUI | Avalonia | Result | Note |
|---|---|---|---|---|
| 1 Repeat the PostgreSQL steps 1, 4 and 12 with `SQUILL_LANGUAGE=en`: every label of the plan tab is English (no French left, no missing key shown as `plan.*`) | [ ] | [ ] | | |
| 2 Compare the plan tab (`Graphique` view, Light) with `M06-Plan-clair-Windows.png` at 1440 x 900: toolbar order, card size, colors, table columns, details panel width, legend | [ ] | [ ] | | |
| 3 Same in Dark with `M06-Plan-sombre-Windows.png` | [ ] | [ ] | | |
