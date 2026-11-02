# P9-23 - Avalonia head: settings window

Status: todo
Depends on: P9-05, P9-03, P9-02

## Goal
The settings window matches the structure and look of M14 for the settings that exist.

## Design decisions (final)
- Read `docs/design/visual-spec.md` sections 3 and 9 (final).
- Window `Paramètres — SQuilL`, back arrow closes it. Left nav 260 wide with the pages that have settings: `Apparence` (theme: three preview cards `Système`/`Clair`/`Sombre`), `Éditeur` (font size, formatter keyword case in its own card `Formatage SQL`), `Grille de résultats` (max rows), `Avancé` (data folder). Settings rows follow the card/row pattern of section 9 with title and description (descriptions are written by this card in French, short, factual, stored in the catalog).
- Binding stays on `SettingsViewModel` (auto-saving as today).

## Out of scope
- Accent color, window material, density, UI font, ligatures, animations, shortcuts, drivers, profile header, search box behavior (no settings behind them; the nav search box is not drawn).

## Tasks
- [ ] Rework `SettingsWindow.axaml`.
- [ ] Headless tests: theme card sets `Theme`, nav switches page, values round-trip through the view-model.

## Acceptance criteria
- `rtk bypass dotnet build Squill.slnx -c Release -warnaserror` exits 0 with 0 warnings.
- `rtk bypass dotnet test --solution Squill.slnx -c Release` is green (new tests included, none skipped except Docker-gated ones).
- `rtk dotnet publish src/Squill.App.Avalonia -c Release -r win-x64` succeeds with zero IL2xxx/IL3xxx warnings.
- Visual check (`docs/design/visual-spec.md` section 11): the running app was captured in light and dark with `tools/visual/Capture-Window.ps1`, compared with `M14-Parametres-clair-Windows.png`, `M14-Parametres-sombre-Windows.png` through `tools/visual/Compare-Mockup.ps1`, and every visible difference was fixed or listed in the card report with its reason. If a state could not be reached or driven, the report says which and what the owner must check by hand.
- No process left running (app, `dotnet build-server`, AOT executable).

## Suggested commit message
Avalonia: settings window
