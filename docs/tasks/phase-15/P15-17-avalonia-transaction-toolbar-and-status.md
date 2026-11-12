# P15-17 - Avalonia head: transaction toolbar group and status bar segment

Status: todo
Depends on: P15-15

## Goal
Same as [P15-15](P15-15-winui-transaction-toolbar-and-status.md) for the Avalonia head: the toggle, the pill, `Valider` / `Annuler` and the status bar segment, on Windows, Linux and macOS. Read P15-15 first: all its design decisions apply unchanged (labels, visibility rules, `Failed` styling, shortcuts `Ctrl+Alt+C` / `Ctrl+Alt+Z` with the same clash check, status texts, tokens only, narrow-window collapsing). Look at `Views/QueryTabView.axaml(.cs)` (where P13-18 added `Expliquer` / `Analyser`), `Views/StatusBar.axaml(.cs)`, `Styles/`.

## Design decisions (final)
- Avalonia specifics: bind with compiled bindings (`x:DataType`), tokens as `SquillXxxBrush` resources, the segmented toggle uses the segmented style of the plan tab (`Arbre | Graphique | Texte`, P13-18) so both look the same, icons from the existing icon set. On macOS the chord is `Ctrl+Alt+C` as well (not `Cmd`); on keyboard layouts where it types a character, the same stop-and-propose rule applies (recipe risk 10).
- Headless tests where the pattern exists (`tests/Squill.App.Avalonia.Tests`): with a fake session and `QueryTabViewModel`, the toggle is disabled when `IsSupported` is false, `Valider` / `Annuler` visibility follows `Mode` / `State`, the pill text for 0 / 1 / 3, red pill on `Failed`, the status segment text and visibility.

## Out of scope
- Dialogs and closing (P15-18).

## Tasks
- [ ] Toolbar group, status segment, shortcuts, headless tests; capture against M04 (light and dark) where the headless run can write it, otherwise listed as "to check by hand".

## Acceptance criteria
- `rtk dotnet build Squill.slnx -c Release -warnaserror` 0 warnings; tests green; Avalonia AOT publish with zero IL2xxx/IL3xxx.

## Suggested commit message
Add the transaction toolbar group and status segment to the Avalonia head
