# P21-31 - WinUI: the script report result and the error policy menu

Status: todo
Depends on: P21-30

## Goal
Show the `Script` result of P21-30 and the policy choice in the WinUI head.

Exists today: `QueryTabView.cs` toolbar (`_run`, `_script`, ...), result tabs, the auto-refresh code-built menu (`AutoRefreshMenu`, P20-38) as the menu pattern.

## Design decisions (final)
- `_script` becomes a split button: click runs, the chevron opens a menu `On error: Stop / Continue / Ask` (radio items, rebuilt on `Opening`).
- The `Script` result tab: a list (code-built rows, status glyph, mono text, right-aligned rows and time), selection drives `SelectStatement`; failed rows in the error token colour.
- The `Ask` question: a `ContentDialog` with the three answers; `Enter` = `Stop`.
- Settings window: the default policy row.

## Out of scope
- Avalonia (P21-32).

## Tasks
- [ ] Text-check tests; publish and drive (recipe 3): a script with an error in statement 3 under each policy, click a row, error squiggle at the position.

## Acceptance criteria
- Recipe 2 and 3 acceptance (WinUI).

## Risks
- Split button inside the toolbar layout (`LayoutToolbar` widths).

## Open points (defaults)
- None.

## Suggested commit message
Show the script run report and the error policy in the WinUI head
