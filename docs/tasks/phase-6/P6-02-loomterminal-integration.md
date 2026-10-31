# P6-02 - Heads: LoomTerminal tabs

Status: done
Depends on: P6-01

## Goal
Render terminal tabs with LoomTerminal on both heads (ADR 0002).

## Design decisions (final)
- Read the LoomTerminal sources first (`D:\LoomTerminal`, property `LoomTerminalPath` in `Directory.Build.local.props`, git-ignored). If the checkout or a usable control/PTY API is missing, STOP and report.
- Optional project reference by MSBuild property as in P5-13, with `SQUILL_LOOMTERMINAL` compile symbol; WinUI backend in the WinUI head, Avalonia backend in the Avalonia head. Each head has a `LoomTerminalHost : ITerminalHost` using LoomTerminal's PTY/ConPTY API and a `TerminalTabView`; a `New terminal` button/menu entry appears only when the symbol is defined.
- No terminal tab for a profile whose launcher returned null: show the reason in a dialog.

## Out of scope
- Changes inside LoomTerminal.

## Tasks
- [x] Implement both hosts and views behind the symbol (one shared `LoomTerminalHost` in `src/Squill.App.Shared`, linked into both heads).
- [x] Run each head with the reference set: both Native AOT publishes start and show the window; opening a shell and typing was not exercised (no UI automation in the session), see the report.

## Acceptance criteria
- `rtk dotnet build Squill.Windows.slnx -c Release -warnaserror` exits 0 with 0 warnings.
- `rtk bypass dotnet test --solution Squill.Windows.slnx -c Release` is green (new tests included, none skipped except Docker-gated ones).
- `rtk dotnet publish src/Squill.App.Avalonia -c Release -r win-x64` succeeds with zero IL2xxx/IL3xxx warnings.
- `rtk dotnet publish src/Squill.App.WinUI -c Release -r win-x64` succeeds with zero IL2xxx/IL3xxx warnings.
- Both heads still build and publish without the property set.
- Report states what was verified on which head (the Avalonia head can only be exercised on this OS).

## Suggested commit message
Add optional LoomTerminal tabs to both heads
