# P10-06 - WinUI head: language choice in the settings window

Status: todo
Depends on: P10-04

## Goal
Let the user pick System default / Français / English in the WinUI settings window.

## Design decisions (final)
- Add one row "Langue" / "Language" as the first row of the general section of the WinUI settings window (P9-24), using the same row template and ComboBox style as the theme row. `ItemsSource` = `SettingsViewModel.LanguageOptions`, `DisplayMemberPath` = `Label`, `SelectedValuePath` = `Value`, `SelectedValue` bound two-way to `Language` with the same binding style (compiled `x:Bind` or code-behind) the neighbouring rows use; follow what the theme row does for AOT.
- Row label through `loc:Loc.Key="settings.language"`; the restart note is a `TextBlock` with `loc:Loc.Key="settings.restartNeeded"` in the secondary-text style, `Visibility` bound to `RestartRequired` through the visibility converter already used by the window. No restart button.
- Language names are shown in their own language in both UIs.
- `visual-spec.md` is already amended by P10-05; do not edit it again (if P10-05 is not done, add the same line as described there).
- Headless test only for the options order if the existing WinUI test project can reach it; otherwise verify through the published exe and say so.

## Out of scope
- Live switching, other rows, Avalonia.

## Tasks
- [ ] XAML row and note.
- [ ] Capture the settings window with `SQUILL_LANGUAGE=en` and `fr` (tools of P9-02).

## Acceptance criteria
- `rtk bypass dotnet build Squill.Windows.slnx -c Release -warnaserror` exits 0 with 0 warnings; `rtk bypass dotnet test --solution Squill.Windows.slnx -c Release` is green.
- `rtk dotnet publish src/Squill.App.WinUI -c Release -r win-x64` succeeds with zero IL2xxx/IL3xxx warnings.
- Choosing English, closing and reopening the exe (without the env override) shows the English UI.
- No process left running.

## Suggested commit message
WinUI: language choice in the settings window
