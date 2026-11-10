# Phase 13 - shared recipe (execution plan, mockup M06)

Read by every card of phase 13. Mockups: `docs/design/mockups/M06-Plan-clair-Windows.png` and `M06-Plan-sombre-Windows.png` (1440 x 900 logical px at 2x). When this file and the PNG disagree about layout, the PNG wins; numbers drawn in the PNG are illustrative, the rules below decide what is computed.

## 1. What the screen is (final decisions)

- The plan opens in **its own workspace tab** titled `<query tab name> · Plan` (like the ER diagram tabs: own collection in `WorkspaceViewModel`, one plan tab per query tab, running again replaces its content). The tab keeps the environment color of the query tab.
- Toolbar of the query tab gets `Expliquer` (estimated plan, never executes the statement) and `Analyser` (actual plan: **executes the statement**). The M04 mockup draws only `Expliquer`; `Analyser` is added by this phase and is the only way to get real timings.
- Plan tab, top to bottom: toolbar (segmented `Arbre` / `Graphique` / `Texte`, the command that produced the plan, summary figures, warning pills, zoom `-` `100 %` `+`, `Relancer` + `Ctrl+Maj+E`), the graph canvas, then at the bottom a table (columns `Nœud`, `Coût relatif`, `Coût total`, `Lignes est.`, `Lignes réelles`, `Temps`, `Boucles`) and a details panel (264 wide, right) for the selected node. `Arbre` = table only (full height) plus details; `Graphique` = canvas on top of the table; `Texte` = the raw engine output, monospace, selectable.
- Graph: operators flow **left to right**, leaves at the left, root at the right; node card 250 x about 90-150 (icon + operation name 13 semibold, target in mono, relative-cost bar, `<pct> %` left and time right, `est. N · réel M ×factor` line when the estimate is off, warning line in amber); border amber when relative cost >= 30 % or the node has a warning, red when >= 60 %; edge thickness grows with the row count; PostgreSQL `Hash` helper nodes are drawn as a dashed pill on the edge (`Hash 212 k · 18 Mo`), not as a card. Legend bottom right: `Épaisseur = lignes`, `> 30 % ou alerte`, `> 60 %`.
- Status bar text while the plan tab is active: engine message line (`EXPLAIN ANALYZE exécuté dans une transaction annulée` when `RolledBack`), right side `<n> nœuds · <ms> ms`, server version.

## 2. Not drawn in phase 13 (no feature behind it)

- `Comparer...` (plan comparison), live/streaming runtime statistics, index suggestions invented by the app for PostgreSQL or MySQL (the `Suggestion d'index` card of the PNG is drawn **only** for SQL Server, from the engine's own `MissingIndexes`), `Workers` line unless the engine reports it (PostgreSQL does), export of a plan to a file, saving plans, Oracle, MariaDB (its JSON differs; a MariaDB server is not a supported MySQL), cost of a statement inside a stored routine.
- A figure the engine does not give is **omitted** (cell shows an en dash, summary figure hidden), never invented. SQLite gives no cost, no estimate and no timing at all; the bars and percentages are not drawn for it.
- Parallel plans (PostgreSQL `Gather`, SQL Server `Parallelism`) are shown as ordinary nodes; time shown is the engine's inclusive figure and relative percentages on parallel plans are approximate (known limit, written in the ADR).

## 3. Engine matrix

| Engine | Estimated (`Expliquer`) | Actual (`Analyser`) | Source format | Cost | Estimated rows | Timing |
|---|---|---|---|---|---|---|
| SQLite | `EXPLAIN QUERY PLAN <sql>` (columns `id`, `parent`, `notused`, `detail`) | not available (button disabled, reason shown) | text rows | no | no | no |
| PostgreSQL | `EXPLAIN (FORMAT JSON) <sql>` | `BEGIN READ ONLY; EXPLAIN (ANALYZE, BUFFERS, FORMAT JSON) <sql>; ROLLBACK` | JSON | yes (unitless, cumulative) | yes (per execution) | yes (ms, per loop) |
| MySQL 8.4 | `EXPLAIN FORMAT=JSON <sql>` | `EXPLAIN ANALYZE <sql>` (text tree, MySQL 8.0.18+) | JSON / text tree | yes (estimated only) | yes | yes (actual only) |
| SQL Server | `SET SHOWPLAN_XML ON` batch, the statement batch, `SET SHOWPLAN_XML OFF` batch | `SET STATISTICS XML ON` batch, the statement batch (executes), `SET STATISTICS XML OFF` batch, all inside a rolled-back transaction | XML | yes (unitless, cumulative subtree) | yes | yes (ms, per thread) |

Engine-specific risks every provider card must handle explicitly:

1. **SQL Server showplan mode is connection state.** If the `OFF` batch is skipped (cancel, exception) the connection stays in showplan mode and every later user statement returns a plan instead of running. `OFF` runs in `finally` with `CancellationToken.None`; if it fails the connection is disposed and the error says the session must be reopened. A test proves a normal statement runs after an explain, after a failed explain and after a cancelled one.
2. **ANALYZE executes the statement.** Only a read-only statement may be analyzed (guard of P13-03); on top of that PostgreSQL runs in `BEGIN READ ONLY` ... `ROLLBACK` (a volatile function that writes then fails instead of writing), SQL Server in `BEGIN TRANSACTION` ... `ROLLBACK`, MySQL has no wrapper (a `START TRANSACTION` would implicitly commit a transaction the user opened by hand; `EXPLAIN ANALYZE` accepts `SELECT` only anyway). `QueryPlan.RolledBack` is true only when a wrapper was really used and rolled back.
3. **PostgreSQL transaction already open** (the user typed `BEGIN;` earlier on the same session): `BEGIN READ ONLY` then raises a warning with SQLSTATE `25001`; when that warning is seen the provider must NOT send `ROLLBACK` (it would end the user's transaction), and `RolledBack` is false.
4. **Parameters**: the statement runs with the same `QueryParameter`s as a normal run. PostgreSQL `EXPLAIN` with `$n` parameters and SQL Server showplan over an `sp_executesql` call must be proven by a Docker-gated test; if an engine refuses, the provider throws `PlanUnavailableException` with the reason `parameters` and the UI says so (no silent substitution of values).
5. **Parsers never throw on unknown content**: unknown JSON keys / XML attributes go to `PlanNode.Properties`; a document that cannot be understood at all raises `PlanParseException` and the Presentation layer shows the raw text in `Texte` with the message. Parsing uses `System.Text.Json.JsonDocument` / `Utf8JsonReader` (DOM and reader only, no serializer, so no source-generated context is needed and nothing relies on reflection) and `System.Xml.Linq` (`XDocument`); both are AOT-safe and the driver AOT probes must still publish without IL2xxx/IL3xxx.
6. **Samples**: every parser has unit tests over sample outputs captured once from the container image of the phase 11 fixtures (`postgres:17-alpine`, `mysql:8.4`, `mcr.microsoft.com/mssql/server:2022-latest`) and committed under `tests/<project>.Tests/Plans/` (sizes small; one estimated and one actual sample per engine, plus one with a join, one with a sort and one with a subquery). These run everywhere; the Docker-gated tests prove the same statements against a live server.

## 4. Layers and names (final)

| Layer | Names |
|---|---|
| `Squill.Core/Plans` | `PlanMode`, `PlanNodeKind`, `PlanSourceFormat`, `PlanNode`, `PlanProperty`, `PlanWarning`, `PlanSuggestion`, `QueryPlan`, `PlanAnalysis`, `PlanNodeStats`, `PlanSeverity`, `PlanMessageCodes` |
| `Squill.Sql/Plans` | `PlanEligibility` |
| `Squill.Data` | `IPlanReader` (optional interface, implemented by the four connections; Oracle does not implement it), `PlanUnavailableException`, `PlanParseException` |
| `Squill.Data.<Driver>` | `<Driver>PlanReader` (internal, runs the statements) and `<Driver>PlanParser` (internal, pure text to `QueryPlan`) |
| `Squill.Presentation/Plans` | `PlanViewModel`, `PlanRowViewModel`, `PlanDetailsViewModel`, `PlanLayout` (+ `PlanLayoutResult`, `PlanEdgePath`), `PlanTabViewModel`; commands on `QueryTabViewModel`, collection on `WorkspaceViewModel` |
| Heads | `PlanTabView`, `PlanGraphCanvas`, `PlanTable` (WinUI: Win2D like `ErDiagramCanvas`; Avalonia: drawing like its `ErDiagramCanvas`) |

String keys: prefix `plan.` in both catalogs (`UiTextCatalogFr`, `UiTextCatalogEn`), French values taken from the mockup, parity and scanner tests of phase 10 must stay green. Message codes for warnings follow the pattern of `CoreMessageCodes` / `IssueText`.

## 5. Common acceptance (every card)

- `rtk dotnet build Squill.slnx -c Release -warnaserror` 0 warnings (cards touching the WinUI head use `Squill.Windows.slnx`), `rtk bypass dotnet test --solution Squill.slnx -c Release` green (never weaken a test), XML docs as in `CLAUDE.md`, no `NoWarn`/`#pragma`.
- Cards with Docker-gated tests also run `rtk pwsh eng/run-docker-tests.ps1 -Engine <engine>`; when Podman is not available the card stops and reports, it never marks skipped tests as verified. No container left afterwards.
- Everything started is closed (`dotnet build-server shutdown`, executables, containers).
