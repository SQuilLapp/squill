# P21-96 - AI assistant (5/5): Avalonia panel and settings, README and privacy notes

Status: todo
Depends on: P21-94, P21-95

## Goal
Same as P21-95 in the Avalonia head; then the README section and a privacy paragraph in `docs/release.md` (what is sent, when, to whom), and an ADR `docs/adr/00NN-ai-assistant.md` (next free number) with the decisions of P21-92 ... P21-96.

Exists today: Avalonia settings window and query tab.

## Design decisions (final)
- Same panel, preview window and settings page as P21-95.
- README "AI assistant (optional)": off by default, providers, what is sent (schema text of up to N tables, no rows unless both switches), the preview before every send, key in the OS keychain.

## Out of scope
- WinUI.

## Tasks
- [ ] Headless tests; publish and drive (recipe 3) against the fake server.
- [ ] README section, `docs/release.md` privacy paragraph, ADR, `CHANGELOG.md` line.

## Acceptance criteria
- Recipe 2 and 3 acceptance (Avalonia); docs written.

## Risks
- None beyond P21-95.

## Open points (defaults)
- None.

## Suggested commit message
Show the AI assistant in the Avalonia head and document its privacy rules
