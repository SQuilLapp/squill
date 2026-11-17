# P18-03 - WinUI head: the home page

Status: done
Depends on: P18-02

## Goal
Draw M01 in the WinUI head from `HomeViewModel`. Recipe 3, 5, 6.

## Design decisions (final)
- `Views/HomePage.xaml(.cs)` in the main area slot, shown when `Workspace.Home.IsHomeVisible`; rail, explorer and properties panel collapsed while it shows.
- Layout from `M01-Accueil-*` (read numbers by cropping, `visual-spec.md` section 1): header (brand glyph, `SQuilL` wordmark in Inter Tight, `Version x.y.z` from `AppVersion.Current`), search box right (opens the palette on click or focus, `Ctrl+K` chip), left column `CONNEXIONS RÉCENTES` + `Gérer les connexions`, a two-column grid of cards (3 px top border in the environment color, engine badge, name, pill, shortcut chip, mono subtitle, footer with last opened text); right column `DÉMARRER` (icon tile + title + subtitle + chip) and the `ASTUCE` card (`TipSegments` rendered with keyboard chips, `Astuce suivante`, `Masquer`), then the checkbox. Cards and tiles are built in code or with a `StackPanel` / `WrapGrid` of `UserControl`s, **not** `ItemsSource` with objects (recipe 5).
- `Ctrl+1` ... `Ctrl+6` accelerators active only while the page is visible. Empty state (no recent connection): one line and the `Nouvelle connexion` tile, no empty grid.
- Status bar left segment `Aucune connexion active` with the grey dot (exists or add through the workspace status text).
- Both themes through token brushes only.

## Out of scope
- Projects, drivers count, Avalonia (P18-04).

## Tasks
- [x] `HomePage`, card and tile controls, accelerators, show / hide of the rail and panels, XAML-free key handling kept in the view-model.
- [x] Tests in `tests/Squill.App.WinUI.Tests` where the harness allows (card count, visibility binding, no `ItemsSource` of objects: assert by grep in the test as P17-19 did for the column menu if a pattern exists).

## Acceptance criteria
- Common acceptance (recipe 7) with the WinUI AOT publish.
- Captures light and dark compared with M01 (recipe 6); differences listed in the card report.

## Suggested commit message
Add the WinUI home page
