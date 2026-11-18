# P18-04 - Avalonia head: the home page

Status: done
Depends on: P18-03

## Goal
The same M01 page in the Avalonia head, same layout and numbers as P18-03. Recipe 3, 6.

## Design decisions (final)
- `Views/HomePage.axaml(.cs)` hosted like the WinUI one; `ItemsControl` over the card view-models is fine here (Avalonia has no AOT trap), with `DataTemplate`s. Keyboard chips from `TipSegments`.
- Same visibility rule and accelerators (`KeyBinding`s on the window, active only while visible).
- Reuse the strings and view-model; no logic in the view.

## Out of scope
- Anything the WinUI card left out.

## Tasks
- [x] `HomePage`, templates, accelerators, hiding rail / explorer / panel.
- [x] Headless tests in `tests/Squill.App.Avalonia.Tests` (card count, empty state, accelerator opens the first card, light and dark render different pixels).

## Acceptance criteria
- Common acceptance (recipe 7) with the Avalonia AOT publish; compare with M01 as far as a capture allows and list the rest.

## Suggested commit message
Add the Avalonia home page
