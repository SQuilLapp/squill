# P18-20 - WinUI head: the security view

Status: done
Depends on: P18-19, P9-10

## Goal
Draw M11 in the WinUI head. Recipe 5, 6.

## Design decisions (final)
- `Views/SecurityView.xaml(.cs)`: left role list (built in code, no object `ItemsSource`), header with badges, the matrix as one Win2D / `Grid`-built control (7 columns, rows 28 high from M11, drawn checkbox glyphs: granted = accent filled, inherited = grey filled, pending grant = accent outline, pending revoke = danger outline with a minus, none = empty; focus ring; tooltip on inherited cells), legend, schema multi-select flyout, right `SQL GÉNÉRÉ` panel with the SQL shown in the read-only SQL highlighter if reusable else plain mono, footer `Copier`, `Annuler`, `Appliquer Ctrl+S`.
- Keys: arrows, `Espace`, `Maj+Espace`, `Ctrl+S`.
- Narrator: the matrix exposes the cell as `<object> <privilege> <state>` (an automation peer or text alternative; if the Win2D control cannot, say so in the report).
- Menu, context and palette entries wired.

## Out of scope
- Avalonia (P18-21).

## Tasks
- [x] View, matrix control, wiring.
- [x] Tests in `tests/Squill.App.WinUI.Tests` where the harness allows (cell hit test, key toggles).

## Acceptance criteria
- Common acceptance (recipe 7) with the WinUI AOT publish; live capture against a PostgreSQL container (`-Keep`) with the P18-17 roles, compared with M11 light and dark; differences listed.

## Suggested commit message
Add the WinUI security view

## Report
- Live capture against a PostgreSQL container with the P18-17 roles and the light/dark comparison with M11 were **not driven**: to check by hand. Known differences from M11: the matrix is a `Grid` of drawn boxes (no Win2D), only the first 400 rows are built, the generated SQL is plain monospace (no syntax colors), the schema selector is a check-box flyout.
- Narrator: each cell is a named element exposing `<schema.object> <PRIVILEGE> <state>`; the focus ring and the arrow / `Espace` / `Maj+Espace` / `Ctrl+S` keys are handled on the matrix. Not tested with a screen reader.
- Added catalog keys `menu.security`, `explorer.security`, `security.col.object`, `security.filterPlaceholder`, `security.showInherited`, `security.legend.*`, `security.pick`, `security.noChanges`, `security.sql`, `security.copy`, `security.discard`, `security.apply`, `security.loading`. WinUI AOT publish: no IL2xxx/IL3xxx from SQuilL code (only the third-party SqlClient and ConfigurationManager assembly warnings), the exe starts.
