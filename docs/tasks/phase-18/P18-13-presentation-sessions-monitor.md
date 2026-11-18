# P18-13 - Presentation: sessions and locks monitor (risky)

Status: done
Depends on: P18-11, P18-12, P18-02

## Goal
The M12 screen as a workspace tab driven by `IActivityReader`: tiles, filtered table, session detail, blocking chains, cancel / terminate. Recipe 1, 2, 4.

## Design decisions (final)
- `src/Squill.Presentation/Activity/SessionsMonitorViewModel.cs`, tab kind `Sessions et verrous` per connection session (one tab per session, a second open request focuses it). Command on the workspace + palette action + menu item (recipe 2); disabled with a reason when the session has no `IActivityReader` (SQLite, Oracle) or is not connected.
- Refresh: every 2 s by default (`RefreshSeconds` 1, 2, 5, 10 persisted in settings only if `AppSettings` already has a place for per-view settings; otherwise a field of the view-model, not persisted), reading on a worker; a read in flight is never overlapped (skip the tick), a stale result never replaces a newer one, `Pause` / resume command (`P`), `Actualisé toutes les 2 s . il y a 1 s` text; an error stops the timer, shows the message and `Reprendre`. The tab pauses itself when it is not the active tab (no polling in the background) and refreshes at once when shown.
- Tiles: `Connexions actives n / max` with a fill ratio, `Transactions / s` = delta of `TransactionCounter` over the elapsed time with the last 60 points kept for the sparkline (tile omitted when the counter is null), `Requêtes longues (> 30 s)` count and `dont n > 5 min` (thresholds constants; durations of `Active` sessions only), `Verrous en attente` count and `bloqués par pid <id>` (the blocker holding most blocked sessions, danger styling when > 0).
- Filter tabs `Toutes n`, `Actives n`, `Transaction inactive n`, `En attente n`, check `Masquer les processus système` (default on, `IsSystem`), filter box (user, application, query, case-insensitive), sort (default duration descending, click a header to change; keep the selected session across refreshes by `Id`, clear it when it disappears and say so in the detail pane for one refresh).
- Row projection (`SessionRowViewModel`): pid, user, application, client, state (dot kind: active = success, idle in transaction = warning, idle = muted), duration text (`6 min 12 s`, `48 s`, `400 ms`, `-`) with warning / danger color above 30 s / 5 min, wait text, one-line query.
- Detail pane: fields (`Utilisateur`, `Application`, `Client`, `Début transaction`, `Dernier état`, `Verrous détenus`: each shown only when the engine provided it), `Dernière requête` with `Copier`, an explanatory note when the session is `IdleInTransaction` and blocks others (`La transaction est ouverte mais inactive depuis 6 min. Elle bloque 2 sessions...`, built from the data, no invented text), `Annuler la requête` (hidden when `!SupportsCancelQuery`), `Terminer la session...`.
- Blocking panel `Qui bloque qui`: chains built from `Blocking` edges (root blockers with a tree of the sessions they block, `BLOQUE n`), a chain per root, selecting a row selects the session; `BlockingUnavailable` shows `Non disponible avec ces droits`.
- Actions: `Annuler la requête` asks a plain confirmation; `Terminer la session...` asks a confirmation naming pid, user and the open-transaction rollback consequence; on a **Production** connection both use the phase 15 production confirmation contract (read-only monitor, but ending a session is a modifying act on production: typed word for terminate, plain for cancel). Never act on the monitor's own session (the reader excludes it; the view-model also refuses an id equal to `SessionId` of its own connection if the engine exposes it). Failure (`ActivityActionFailedException`) shows the message.
- Status bar data exposed: environment block, `pg_stat_activity . 47 lignes . 6 ms` style text (`<source view> . <n> lignes . <ms> ms`; the source view name per engine comes from the reader: add `string SourceName` to `ActivitySnapshot` if P18-11 did not).
- `UiText` keys `activity.*` (fr + en): strings of M12 (`Sessions et verrous`, `Actualisé toutes les 2 s`, `Pause`, `Masquer les processus système`, `Qui bloque qui`, `1 chaîne de blocage`, `Terminer la session...`, `Production : une confirmation sera demandée.`).

## Out of scope
- Views (P18-14, P18-15), history of snapshots, killing several sessions, query plans of a session.

## Tasks
- [x] View-model, row and detail view-models, chain builder (pure, `BlockingChains.Build`), tiles, timer with an injectable clock and scheduler, catalog keys, workspace command, palette action.
- [x] Tests in `tests/Squill.Presentation.Tests/Activity/` with a fake reader: tiles figures, transactions per second from two samples, filters and counts, selection kept across refreshes, chain building (two blocked behind one, a chain of three, a cycle does not loop), paused tab does not poll, overlapping read skipped, stale read dropped, cancel unsupported hides the command, terminate on Production asks the typed confirmation and does nothing when refused, error stops the timer.

## Acceptance criteria
- Common acceptance (recipe 7), no head touched. Riskiest: polling must never pile up or keep running in a closed or inactive tab (a test proves it).

## Suggested commit message
Add the sessions and locks monitor view-model
