# P11-22 - Avalonia head: explorer glyphs and menu for routines, triggers, definitions

Status: done
Depends on: P11-20

## Goal
Same as P11-21 on the Avalonia head.

## Design decisions (final)
- Glyph mapping and context menu in the Avalonia explorer view (find the equivalent of `ExplorerGlyphs` in `Squill.App.Avalonia/Views`), same commands and catalog keys as P11-21. Headless test: build the tree from a snapshot with every new kind and assert each node has a glyph and the menu has the two commands.

## Out of scope
- WinUI (P11-21).

## Tasks
- [x] Glyphs, menu, headless test, one capture `en`.

## Acceptance criteria
- `rtk dotnet build Squill.slnx -c Release -warnaserror` 0 warnings; `rtk bypass dotnet test --solution Squill.slnx -c Release` green; Avalonia AOT publish with zero IL2xxx/IL3xxx warnings.

## Suggested commit message
Draw routines and triggers in the Avalonia explorer with definition actions
