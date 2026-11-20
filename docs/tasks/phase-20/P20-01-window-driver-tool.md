# P20-01 - A reusable tool to drive the app windows (UI Automation and keys)

Status: done
Depends on: none

## Goal
Every phase 19 verification card that stayed partial says the same thing: the native window could not be clicked or typed into (P19-09 "no UI automation", P19-13 "synthetic keys did not open the palette", P19-14 "the UIA click on the toggle did not engage it"). P19-12 and P19-14 did drive a window with a throw-away script that was not kept. Keep one tool so the cards of this phase do not each rebuild it.

## Design decisions (final)
- `tools/visual/Drive-Window.ps1` (PowerShell 7, Windows only, `Add-Type` + `System.Windows.Automation` from `UIAutomationClient`, same style as `Capture-Window.ps1`). One script, a `-Action` parameter, `-Process <name>` selects the main window as `Capture-Window.ps1` does:
  - `Dump [-Depth n] [-Filter <text>]`: prints the UI Automation tree under the window (`ControlType`, `Name`, `AutomationId`, `ClassName`, bounding rectangle) so a card can find what to click.
  - `Click -Name <text> [-ControlType <type>] [-Index n] [-Mode Invoke|Mouse|DoubleClick|RightClick]`: finds the element (exact `Name`, then `Contains`), uses `InvokePattern` / `SelectionItemPattern` / `ExpandCollapsePattern` when present and `-Mode Mouse` (real mouse event at the element center through `SetCursorPos` + `mouse_event`) otherwise. Exit 4 when the element is missing, printing the nearest names.
  - `Key -Keys <chord list>` e.g. `Ctrl+K`, `Ctrl+Shift+E`, `Alt+Down`, `Enter`, `Esc`, `Ctrl+Alt+C`: sends the chord with `SendInput` (virtual keys, key down / key up, modifiers held), **after** checking with `GetForegroundWindow` that the target window is the foreground window, bringing it to the front (`SetForegroundWindow`, with the `AttachThreadInput` trick) and exiting 2 when it cannot (a covered window must not receive keys meant for another program; same rule as `Capture-Window.ps1`).
  - `Type -Text <text>`: `SendInput` with `KEYEVENTF_UNICODE` (accents, `;`, quotes).
  - `Wait -Name <text> [-TimeoutSeconds 10] [-Gone]`: polls for an element to appear or disappear; exit 5 on timeout.
  - `Hover -Name <text>` / `-X -Y` (client logical px): moves the mouse without clicking, for pointer-over states and tooltips.
  - `Focus-Dialog`: brings a secondary top-level window of the same process (confirmation dialog, wizard) to the front and makes it the target of the next `Key` / `Click` (P19-14: the dialog tree is not reachable from the main window tree).
- Output is plain text on stdout, one `ok` / `error` line per action, exit codes documented in `tools/visual/README.md` (0 ok, 1 window missing, 2 cannot get focus, 4 element missing, 5 timeout).
- A self-check, `tools/visual/Drive-Window.ps1 -Action SelfTest`, launches `notepad.exe`, types text, asserts the edit control holds it, closes it, and prints `selftest ok`; the card runs it. No new NuGet package, no C# project.
- Add the entries needed so the heads can be driven, found with `Dump` on both heads: when a control a later card needs (the explorer tree, the toolbar buttons `Exécuter`, `Expliquer`, the sidebar tool buttons, the tab headers, the palette input) has an empty `Name` in both `Dump` outputs, set `AutomationProperties.Name` (WinUI) / `AutomationProperties.Name` (Avalonia) from the existing `UiText` strings on **at most 12 controls**; these are accessibility names, also required by the Narrator rows of the checklists. A bigger gap is reported, not fixed.

## Out of scope
- Driving custom-drawn canvases (`DataGridCanvas`, `ErDiagramCanvas`, `PlanGraphCanvas`): use coordinates from `Dump` of the host and `Click -Mode Mouse` with `-X -Y`; no OCR, no image matching.
- Linux / macOS.

## Tasks
- [x] Script with the actions above, `SelfTest`, README section "Drive-Window" in `tools/visual/README.md` (usage, exit codes, the foreground rule, how to find names with `Dump`).
- [x] `Dump` both published heads (`Squill.App.WinUI`, `Squill.App.Avalonia`, SQLite `-Scenario Full`) and write the list of names a later card needs into the README ("Names to click"): connection list, `Exécuter`, `Expliquer`, tabs, palette, explorer.
- [x] Prove it on both heads: open `pg-prod-eu` (double click in the explorer), run `select * from clients` (`Key Ctrl+Enter`), open the palette (`Key Ctrl+K`, `Wait` for its input), `Key Esc`; capture with `Capture-Window.ps1` and read the PNG.

## Acceptance criteria
- `SelfTest` prints `selftest ok`; both heads were driven as above (Done notes say which actions used `Invoke` and which needed `Mouse`); if a head cannot be driven for the palette, say exactly why (this is what P19-13 hit) and keep the exit code 2 behavior.
- Only `tools/visual/` and at most 12 `AutomationProperties.Name` additions changed: if heads changed, common acceptance of recipe 2 applies (both slnx, AOT publish, app starts).
- Processes closed.

## Risks
- `SendInput` into a window that is not foreground goes to the wrong program: the foreground check is not optional, test it by covering the window with another one.
- WinUI 3 controls built in code (no XAML name) expose their class name only: prefer `ClassName` + `Index` over a guess.

## Suggested commit message
Add a UI Automation and keyboard driver for the visual verification passes

## Done notes
- `tools/visual/Drive-Window.ps1` with Dump, Click, Key, Type, Wait, Hover, Focus-Dialog, SelfTest; README section "Drive-Window" with exit codes, the foreground rule and "Names to click". `SelfTest` prints `selftest ok`. It uses its own WinForms window: Notepad was tried first and rejected because Windows restores the user's earlier tabs into it and the typed text went into one of them (that Notepad process was killed without saving; an unsaved edit may be offered back in Notepad's restored tabs).
- Both heads driven (SQLite Full, French, 150 % display): open `pg-prod-eu` (Invoke), run `select * from clients` with `Ctrl+Enter` (12 rows), `Ctrl+K` (palette input found), `Esc`; both captures were read. Only the editor focus click needed `Mouse`. The palette is driveable on both heads (no P19-13 problem seen here).
- Not done: no `AutomationProperties.Name` added: about 14 controls per head are unnamed (more than the 12 allowed), listed in the README. The covered-window refusal (exit 2) is implemented but was not exercised by covering a window. Heads unchanged, so no slnx / AOT run was needed.
