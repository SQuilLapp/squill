# P11-14 - Avalonia head: result tabs for several sets and a Messages tab

Status: todo
Depends on: P11-12

## Goal
Same as P11-13 on the Avalonia head.

## Design decisions (final)
- Reuse the Avalonia result tab strip and add a read-only selectable text view for `ResultKind.Messages` (monospace token). Headless test: a `QueryTabViewModel` with a fake provider returning two sets and messages produces three tabs; selecting the last shows the text.

## Out of scope
- WinUI (P11-13).

## Tasks
- [ ] AXAML, headless test, one capture `en`.

## Acceptance criteria
- `rtk dotnet build Squill.slnx -c Release -warnaserror` 0 warnings; `rtk bypass dotnet test --solution Squill.slnx -c Release` green; Avalonia AOT publish with zero IL2xxx/IL3xxx warnings.

## Suggested commit message
Show several result sets and messages in the Avalonia results panel
