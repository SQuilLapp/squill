# P19-11 - Avalonia: look at M01, M09, M10, M11 and M12

Status: todo
Depends on: P19-09, P19-10

## Goal
The Avalonia versions of the five phase 18 screens were only tested headless (cards P18-04, P18-10, P18-15, P18-21, P18-27: "no capture was taken"). Do the same pass as P19-09 and P19-10 in the Avalonia head, reusing their data, their findings and their fixes (a difference already fixed or proposed for WinUI is not rediscovered: read their Done notes first). Recipe 3 of [debug-polish-recipe.md](debug-polish-recipe.md) applies.

## Design decisions (final)
- Head: `rtk dotnet publish src/Squill.App.Avalonia -c Release -r win-x64`, run the exe (process `Squill.App.Avalonia`), Windows only. Linux and macOS are not available: say so in the Note; nothing is claimed for them.
- Matrix: M01, M09, M10 (SQLite scenario of P19-08) and M11, M12 (PostgreSQL `-Keep` container as in P19-10, same safety rules, no credential in the repository), light and dark, French and English. M10: the Avalonia file picker may be scriptable (it is the app's own `StorageProvider` dialog on Windows, a native dialog in practice): if it cannot be driven, mapping step is `agent: not driven (file picker)`.
- Known Avalonia differences already recorded in the P18 done notes (the `outline-danger` Production button instead of the solid fill, the rail subtitle showing the engine enum name in M10) are re-checked: fix the enum-name subtitle here if it still shows (it is a wording bug: use the localized engine label already used elsewhere), leave the button style as a recorded deviation unless the mockup comparison shows it clearly wrong.
- Results go in the `Result` column of `new-screens.md` as `agent: ...` entries (a second note for Avalonia), boxes unticked. Update the Avalonia cells of `visual-spec.md` section 12 and sections 15-19.

## Out of scope
- Linux/macOS drivers, ticking owner boxes, redesigns.

## Tasks
- [ ] Publish, prepare, drive, capture, read PNGs, compare.
- [ ] Small fixes with headless tests where the pattern exists; proposed cards for bigger ones.
- [ ] Update docs; stop containers and processes.

## Acceptance criteria
- Common acceptance (recipe 4) if code changed (`Squill.slnx`, Avalonia AOT publish, app starts, `LayoutOverlapTests` green); report separates compared / not driven; `podman ps -a` empty.

## Risks
- Avalonia `RenderScaling` differs from WinUI at 125 % / 150 % display scale: capture at the scale `Capture-Window.ps1` reports and say it in the Note.

## Suggested commit message
Compare the Avalonia home, compare, import, privileges and sessions screens with their mockups
