# P10-05 - Avalonia head: language choice in the settings window

Status: todo
Depends on: P10-04

## Goal
Let the user pick System default / Français / English in the Avalonia settings window.

## Design decisions (final)
- Add one row "Langue" / "Language" (`settings.language`) as the first row of the general section of `SettingsWindow.axaml`, built with the same row template, label style and ComboBox style as the existing theme row (P9-23). `ItemsSource` = `SettingsViewModel.LanguageOptions`, `DisplayMemberBinding` = `Label`, selected value bound two-way to `Language` (`SelectedValueBinding` = `Value`).
- Under the row, a `TextBlock` with `{loc:Text settings.restartNeeded}` in the existing secondary-text style, `IsVisible` bound to `RestartRequired`. No restart button, no auto-restart.
- The two language names are shown in their own language in both UIs (catalog values from P10-04).
- Every other string of the window is left to P10-16. The language row is not in the mockup M14: add one line to `docs/design/visual-spec.md` section 10 ("Language row added by phase 10") and to the settings entry of section 12.
- No code-behind logic; headless tests only for what binds (the options list order: System, Français, English).

## Out of scope
- Live switching, other settings rows, WinUI.

## Tasks
- [ ] XAML row and note, `visual-spec.md` lines.
- [ ] Test in `tests/Squill.App.Avalonia.Tests` that the window's ComboBox lists the three options and that selecting one sets `SettingsViewModel.Language` and `RestartRequired` (headless, in the style of the existing head tests).
- [ ] Capture the settings window with `SQUILL_LANGUAGE=en` and `fr` (tools of P9-02) and check the row alignment.

## Acceptance criteria
- `rtk bypass dotnet build Squill.slnx -c Release -warnaserror` exits 0 with 0 warnings; `rtk bypass dotnet test --solution Squill.slnx -c Release` is green.
- `rtk dotnet publish src/Squill.App.Avalonia -c Release -r win-x64` succeeds with zero IL2xxx/IL3xxx warnings.
- Choosing English, closing and reopening the app (without the env override) shows the English UI; choosing System default on a French OS shows French.
- No process left running.

## Suggested commit message
Avalonia: language choice in the settings window
