# P19-07 - Make `Capture-Window.ps1` capture the window, not the desktop

Status: todo
Depends on: none

## Goal
P18-31 recorded: "A WinUI launch at the start of the card captured the desktop instead of the window (the capture script copies the screen; the window was not on top), so no visual match is claimed for any of the five screens." `tools/visual/Capture-Window.ps1` calls `SetForegroundWindow` (which Windows refuses to a background process) and then `Graphics.CopyFromScreen`, so whatever is on top gets captured. Every verification card of this phase depends on the tool telling the truth.

## Design decisions (final)
- After positioning the window, bring it to the front reliably: temporary `HWND_TOPMOST` through `SetWindowPos` then `HWND_NOTOPMOST` after the capture (no `AttachThreadInput` tricks, no key injection), and check `GetForegroundWindow()` / that the window rectangle is not covered (`WindowFromPoint` at the center of the client area belongs to the target's process via `GetWindowThreadProcessId`). If it is still covered, exit 2 with `Window of '<process>' is covered by '<other>'` instead of saving a wrong PNG.
- New parameter `-Method Screen|PrintWindow` (default `Screen`). `PrintWindow` uses `PrintWindow(hwnd, hdc, 2)` (`PW_RENDERFULLCONTENT`) and works when covered. Known risk: a DirectComposition / WinUI 3 window may come back black; detect a fully black bitmap (sample a grid of pixels) and exit 3 with that message.
- The script prints the foreground/cover check result on success (`Saved ... (window on top: yes, method Screen)`), so a log shows how it was captured.
- Keep the DPI, size and exit-1 behaviors unchanged. Update `tools/visual/README.md` (parameter, exit codes, the black-bitmap caveat).

## Out of scope
- UI automation (clicks, keys), video, capturing popups outside the window.

## Tasks
- [ ] Script changes as above.
- [ ] Prove it on the published or `dotnet run` WinUI head with the profile of `New-VisualProfile.ps1`: (1) window behind a PowerShell console: `-Method Screen` now brings it up and the PNG shows SQuilL; (2) a deliberately covering topmost window: exit 2, no PNG written; (3) `-Method PrintWindow` while covered: PNG shows SQuilL, or exit 3 when black. Read each PNG with the Read tool and say in the Done notes what was seen.
- [ ] Same for the Avalonia head (process `Squill.App.Avalonia`).

## Acceptance criteria
- The three scenarios behave as described for both heads; no PNG of the desktop can be produced with exit 0. Processes closed; `dotnet build-server shutdown`.
- If `PrintWindow` is black for WinUI, that is recorded as a known limit in `tools/visual/README.md`, and `Screen` with the topmost fix is the supported method (acceptable result).

## Risks
- `SetWindowPos` topmost may steal focus from the user's own work: say in the README that the window flashes to the front for about a second.
- Another always-on-top app (a screen recorder) can cover the window: the exit 2 check is there for it.

## Suggested commit message
Capture the target window reliably and refuse to save a covered capture
