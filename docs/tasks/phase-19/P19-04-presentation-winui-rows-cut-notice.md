# P19-04 - Say when the sessions and privileges lists are cut (Presentation and WinUI)

Status: done
Depends on: none

## Goal
Because the heads draw rows as elements without virtualization, they stop at 400 rows: `const int MaxRows = 400` in `src/Squill.App.WinUI/Views/SessionsMonitorView.cs:36` (`vm.Rows.Take(MaxRows)`) and `src/Squill.App.WinUI/Views/SecurityView.cs:30` (`Math.Min(vm.Rows.Count, MaxRows)`); the same number lives in the two Avalonia files (P19-05). Nothing tells the user: a server with 900 sessions or a database with 1,200 tables shows a list that looks complete. The `shortcut:` comments say "the filter box narrows the rest", but the user is never told there is a rest.

## Design decisions (final)
- The limit moves to Presentation so both heads and the notice share one number: `public const int MaxShownRows = 400;` on `SessionsMonitorViewModel` and on `SecurityViewModel` (`src/Squill.Presentation/Security/SecurityViewModel.cs`), plus `public int HiddenRowCount` (`Math.Max(0, Rows.Count - MaxShownRows)`, `Rows` being the filtered rows the head draws), raised through `PropertyChanged` whenever `Rows` changes (follow how `Rows` change is already notified; on the dispatcher rules of recipe and P19-02).
- Text through `UiText` (fr + en): `activity.rowsCut` and `security.rowsCut`, using `FormatPlural` where the catalog pattern asks for it (read an existing plural key): fr `... sessions non affichées : affinez avec le filtre`, en `... sessions not shown: narrow with the filter` (security: `objets`, `objects`, and "choose fewer schemas"). One line under the list, `fg-3`/muted token, hidden when `HiddenRowCount` is 0.
- WinUI: the notice is a `TextBlock` built in code like the neighbouring elements (the `ItemsSource` rule of ADR 0015: no managed list as `ItemsSource`). The head's private `MaxRows` constants are deleted and replaced by the Presentation constant; hit testing and keyboard movement of the privilege matrix keep using the same bound.
- No change to what is drawn up to 400 rows, no virtualization (that is a feature: the `shortcut:` comments stay, reworded to mention the notice).

## Done notes
- `MaxShownRows` and `HiddenRowCount` / `RowsCutText` added to both view models (`activity.rowsCut`, `security.rowsCut`, `.one`/`.other`, fr + en); `HiddenRowCount` is raised once per rows change. WinUI heads use the constant and show the notice. No head test pattern exists for these views.
- Build (`Squill.Windows.slnx`, 0 warnings), tests (4274 passed) and WinUI AOT publish (no SQuilL IL2xxx/IL3xxx) done; the published app was not started.

## To check by hand
- PostgreSQL `-Keep` container with 500 `pg_sleep` sessions (`rtk pwsh eng/run-docker-tests.ps1 -Engine postgres -Keep`) shows the line under the sessions list; a filter leaving 10 rows hides it.

## Out of scope
- Avalonia (P19-05), virtualizing the lists, paging.

## Tasks
- [x] Tests in `tests/Squill.Presentation.Tests` first: 399, 400 and 401 rows give `HiddenRowCount` 0, 0, 1; narrowing the filter changes it and raises the notification; the catalog keys exist in both languages (parity test).
- [x] Implement the constants and the property; WinUI notice in `SessionsMonitorView.cs` and `SecurityView.cs`; remove the head constants.
- [x] `tests/Squill.App.WinUI.Tests`: add a test only where the head already has a pattern for these views; otherwise list under "To check by hand": a PostgreSQL `-Keep` container with 500 `pg_sleep` sessions (`rtk pwsh eng/run-docker-tests.ps1 -Engine postgres -Keep`) shows the line, a filter that leaves 10 rows hides it.

## Acceptance criteria
- Common acceptance (recipe 4) with `Squill.Windows.slnx` and the WinUI AOT publish.
- Avalonia keeps building: it must not reference a removed Presentation member (P19-05 follows).

## Risks
- `Rows` may be a collection mutated in bulk: raise `HiddenRowCount` once per refresh, not per row (a notification storm on a 5,000 row refresh).
- The matrix hit test uses the row count: keep `Math.Min(vm.Rows.Count, MaxShownRows)` at all three places (draw, hit test, keyboard move).

## Suggested commit message
Tell the user when the sessions and privileges lists show only the first rows
