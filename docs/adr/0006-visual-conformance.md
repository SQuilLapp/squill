# ADR 0006 - Visual conformance with the owner's mockups

- Status: accepted. Superseded in part by [ADR 0007](0007-localization.md) (the French-only decision).
- Date: 2026-11-03

## Context
Phase 9 (cards P9-01 to P9-27) restyled both heads (WinUI 3 and Avalonia) after the owner's mockups M02 to M08 and M14, without adding features. The metrics are in [docs/design/visual-spec.md](../design/visual-spec.md); what is matched per screen is in its section 12.

## Decision
- **French only in phase 9; phase 10 added English (ADR 0007), so the "no language switch and no English catalog" part below no longer holds.** All user-visible text of the screens in scope comes from a string catalog (`UiText`, built from `UiTextCatalog`, fr-FR). Keys are English identifiers, values are the French strings drawn in the mockups. There is no language switch and no English catalog.
- **Fonts.** UI text uses the token `font.ui` (Segoe UI Variable first, Inter bundled as the fallback), code, grid and identifiers use bundled JetBrains Mono, the wordmark uses bundled Inter Tight. The font files are OFL and registered in both heads.
- **Features that do not exist are not drawn.** A mockup element whose feature or data is missing is omitted, never replaced by a placeholder (list in visual-spec section 10).
- **Data additions** are limited to what the screens need: connection profiles gained `Environment` and `Folder` (old files load unchanged), and the explorer is one tree from connection to objects with counts on schema and folder rows (P9-26, P9-27).
- **Fidelity is verified by capture**, not by unit tests: `tools/visual/` ([README](../../tools/visual/README.md)) creates French sample data, captures a window at 1440 x 900 logical px and builds a side-by-side with a mockup (`Compare-Mockup.ps1`). Unit tests cover bindings, formatting helpers and styles (no literal colors), not pixels.

## Known gaps and what was not verified
- The visual comparison with `Compare-Mockup.ps1` was skipped or only partly read on several cards (P9-11 light theme checked on the capture alone, P9-12 light theme and diff image not read in detail, P9-19 compared on a headless render, P9-20 one theme only).
- The light theme was not captured on P9-20 (the WinUI app kept the dark theme), and several other screens were captured in only one theme.
- Hover, pressed and focus states were never driven by hand. Neither were most popups: the menu, flyout, tooltip and dialog popups of the WinUI styles, the parameters prompt, the relation tooltip of the ER diagram, the unsaved dot and hover close button of tabs, and the connected status bar of WinUI.
- The DML and provider tests that need Docker (PostgreSQL, MySQL / MariaDB, SQL Server) have never run.
- WinUI gets `LoomEditor.WinUI` and `LoomTerminal.WinUI` from NuGet; their use (editor frame, read-only DDL preview) was checked only through the published exe. LoomEditor exposes decorations only, so the wavy diagnostic underline, inline diagnostic pill, margin triangle, current-statement background and completion popup are not matched.
- Avalonia still uses AvaloniaEdit as its editor (same unmatched items); its LoomTerminal control comes from the `LoomTerminal.Avalonia` NuGet package.
- WinUI explorer rows are 28 high instead of 24 (the `TreeViewItemPresenter` clips below that height).
- No screenshots were added to the README: none were captured for it.

## Follow-up feature phases (proposals only, nothing decided)
`visual-spec.md` section 10 lists mockup screens that need their own feature work:
- Start page (M01) and command palette (M13).
- Query plan (M06).
- Schema compare (M09) and data transfer (M10).
- Privileges (M11) and sessions (M12).
- Production confirmation dialog (M15) with the auto-commit / manual transaction controls.
- Grid filter bar, column menu and row panel (M05); tags, SSL, proxy and driver tabs (M02); impact estimate (M07); shortcuts, drivers, accent, material and density settings (M14).
