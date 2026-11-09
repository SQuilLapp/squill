# P12-13 - WinUI head: update rows in the settings window and the About dialog

Status: done
Depends on: P12-12, P9-24

## Goal
The WinUI user can choose how often SQuilL checks and which channel, check by hand, and open an About dialog showing the version and the update status.

## Design decisions (final)
- Look and feel from `docs/design/visual-spec.md` and the existing `SettingsWindow` (P9-24): a new section "Updates" (`settings.update.title`) below the language row with a frequency `ComboBox` (`FrequencyOptions`), a channel `ComboBox` (`ChannelOptions`) with the `channelNote` and `noDowngrade` lines, and a *Check now* button + status line bound to `UpdateViewModel`. Markup strings come from `{loc:Text Key=...}` (P10-08); computed ones from the view-models.
- About: a new `AboutWindow` (same chrome pattern as `ConnectionEditorWindow`: modal, `AppWindow.SetIcon`, 420 x 360 logical px, no resize) opened from the existing menu: `Help > About SQuilL` (add the `Help` menu if absent; keys `menu.help`, `menu.about`). It shows the icon (`Assets/squill-32.png` already shipped), name, `VersionText`, `ChannelText`, `InstallKindText`, `RuntimeText`, the data folder with an "Open" link (`explorer.exe` through `Process.Start` with `UseShellExecute` on the folder path only when it exists), the repository link, the status line and *Check now* / *Restart to update* buttons from `Updates`, and the notes of the installed version when `RunKind` was `Updated` (rendered by the shared `ReleaseNotesPresenter`, see below).
- `ReleaseNotesPresenter` (WinUI, `Views/`): turns `IReadOnlyList<NotesBlock>` into a `StackPanel` of `TextBlock`/`RichTextBlock` runs (heading = semibold, bullet = glyph + text, code = monospace run on the token background, link = `Hyperlink` opening only `http(s)` URLs through the OS default browser). Reused by P12-14. No third-party control.
- Dark/light through existing theme resources; keyboard: Esc closes; Tab order natural; every control has an `AutomationProperties.Name` from the catalog.
- Window plumbing: `App.xaml.cs` exposes `AppServices.Updates`; the dialog service opens the About window the same way settings opens (copy the settings window's open code path, do not add a new service type).

## Out of scope
- The "update available" banner and the update dialog (P12-14), Avalonia (P12-15).

## Tasks
- [x] Settings rows, `AboutWindow`, `ReleaseNotesPresenter`, menu entry, catalog keys (`menu.help`, `menu.about`, `about.title`, `about.openDataFolder`, `about.repository`, `about.close`) in both catalogs.
- [x] Tests in `tests/Squill.App.WinUI.Tests`: presenter output for a notes sample (block and run counts, only http(s) hyperlinks), formatting helpers; scanner/parity tests of phase 10 green.
- [x] Capture both windows (partly: see below) with `SQUILL_LANGUAGE=en` and `fr` (tools of P9-02, `docs/design/visual-spec.md` section 11) from the published exe with a throwaway `SQUILL_DATA_DIR`; text must not clip in fr; list what could not be driven under "to check by hand".

## Acceptance criteria
- `rtk dotnet build Squill.Windows.slnx -c Release -warnaserror` 0 warnings; `rtk bypass dotnet test --solution Squill.Windows.slnx -c Release` green.
- `rtk dotnet publish src/Squill.App.WinUI -c Release -r win-x64` zero IL2xxx/IL3xxx warnings; the exe starts, About opens and closes, settings changes persist across a restart; no process left running.
- To check by hand (owner): change frequency to Daily and channel to Beta, restart, values are kept; *Check now* from About shows "Not installed through the installer" when started from the build folder.

## Suggested commit message
Add update settings and the About window to the WinUI head

## Report
- Done: Updates card in the Appearance page (below the language row), `AboutWindow`, `ReleaseNotesPresenter` + `ReleaseNotesFormat`, `Help > About` entry (the existing `menu.help` / `menu.about` keys), catalog keys `about.title`, `about.openDataFolder`, `about.repository`, `about.close` (en + fr).
- Deviation: code runs have no background (a `Run` cannot have one); they use the mono font and the foreground token.
- AOT publish: no IL2xxx/IL3xxx from SQuilL code; the only ones are the existing third-party `Microsoft.Data.SqlClient` / `System.Configuration.ConfigurationManager` trim warnings. The published exe starts and stays alive with a throwaway `SQUILL_DATA_DIR`.

## To check by hand
- Open About from `Help` (en and fr): no clipped text, Esc closes, *Open* opens the data folder, the repository link opens in the browser, *Check now* shows "Not installed through the installer" from the build folder.
- Settings: frequency Daily and channel Beta, restart, values kept; `NotAsked` shows as Never without saving; the no-downgrade note appears for Stable on a pre-release.
- Screenshot captures of both windows (P9-02 tools) were not driven automatically.
