# P21-95 - AI assistant (4/5): WinUI panel and settings

Status: todo
Depends on: P21-94

## Goal
Show the assistant in the WinUI head: a panel under or beside the editor, the payload preview dialog, and the settings page.

Exists today: the parameters panel under the editor (P18-30) as the docking pattern; `SettingsWindow.xaml`; password boxes in the connection editor.

## Design decisions (final)
- Toolbar button `AI` (hidden when the feature is disabled in Settings), panel with prompt, action buttons, reply (mono SQL blocks with their buttons); the preview as a `ContentDialog` with the exact payload in a read-only scrollable mono box, size and tokens, `Send` (not the default button: `Enter` never sends, phase 15 rule) and `Cancel`.
- Settings page `AI assistant`: enable switch (off), provider, base URL, model, API key (password box, stored in `ISecretStore`), `Test` (sends a one-word prompt after its own preview), the privacy switches with explanations.

## Out of scope
- Avalonia (P21-96).

## Tasks
- [ ] Text-check tests; publish and drive (recipe 3) against a local fake server (a small `HttpListener` test host in `tools/` or Ollama if installed); never with a real paid key in the drive.

## Acceptance criteria
- Recipe 2 and 3 acceptance (WinUI).

## Risks
- None specific.

## Open points (defaults)
- None.

## Suggested commit message
Show the AI assistant panel, preview and settings in the WinUI head
