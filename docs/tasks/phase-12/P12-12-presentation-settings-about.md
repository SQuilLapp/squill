# P12-12 - Presentation: update settings and About view-models

Status: todo
Depends on: P12-11

## Goal
The settings window can edit the update frequency and channel, and an About dialog can show what is installed, with the same logic in both heads.

## Design decisions (final)
- `SettingsViewModel` gets `UpdateCheckFrequency UpdateFrequency` and `UpdateChannel UpdateChannel` (observable, saved like the other settings, included in `Current` and `LoadAsync`), `IReadOnlyList<FrequencyOption> FrequencyOptions` and `IReadOnlyList<ChannelOption> ChannelOptions` (`record FrequencyOption(UpdateCheckFrequency Value, string Label)`, `record ChannelOption(UpdateChannel Value, string Label)`; the picker offers `Never`, `Startup`, `Daily`, `Weekly` only; a loaded `NotAsked` is shown as `Never` in the picker **without saving** until the user changes it, so merely opening the window never decides for the user). Keys: `settings.update.title`, `.frequency`, `.frequency.never`, `.startup`, `.daily`, `.weekly`, `.channel`, `.channel.stable` ("Stable"), `.channel.beta` ("Beta: early releases, may be unstable" / "Beta : versions anticipées, parfois instables"), `.channelNote` ("Changing the channel applies to the next check." / fr), `.checkNow` reused from `update.button.checkNow`.
- Switching from `Beta` to `Stable` never downgrades (`AllowVersionDowngrade` false, enforced by P12-08's downgrade rule): note key `settings.update.noDowngrade` shown while `UpdateChannel == Stable` and the installed version is a pre-release ("You will get the next stable release; SQuilL does not go back to an older version.").
- `AboutViewModel` (new, `Squill.Presentation.Updates`): `ProductName` ("SQuilL"), `VersionText` (`about.version`: "Version {0}", `AppVersion.Current`), `ChannelText` (`about.channel`: from the installed version's pre-release flag), `InstallKindText` (`about.install.installed` "Installed with the installer" / `about.install.portable` "Not installed (updates are disabled)"), `RuntimeText` (`about.runtime`: ".NET {version}, Native AOT" from `Environment.Version` and `RuntimeFeature.IsDynamicCodeSupported == false`), `DataFolder` (`AppPaths.DataDirectory`, a "Open data folder" request exposed as `OpenDataFolderRequested` event handled by the head), `RepositoryUrl` (`https://github.com/squillapp/squill`) and `LicenseText` (read the repository `LICENSE` file name; if there is none, omit the license line and say so in the report), the embedded `UpdateViewModel Updates` for the check button and status line, `RunKind` of P12-06 (`about.whatsnew` shown when `Updated`: "Updated from {0}" using the stored last version; the notes of the installed version come from `CHANGELOG.md` embedded as a resource of `Squill.Presentation`: add an `EmbeddedResource` link to `../../CHANGELOG.md` with `LogicalName` `Squill.Presentation.CHANGELOG.md` and read it with `typeof(AboutViewModel).Assembly.GetManifestResourceStream(...)`, the only `Assembly` API allowed here (trim-safe, not reflection over members); the section of `AppVersion.Current` is cut out with the same rules as `eng/release-notes.ps1`, falling back to `Unreleased`).
- Every string through `UiText`, both catalogs.

## Out of scope
- Any window or control (P12-13, P12-14, P12-15).

## Tasks
- [ ] Settings members and options, `AboutViewModel`, embedded changelog, catalog keys fr/en.
- [ ] Tests in `tests/Squill.Presentation.Tests/Settings/` and `/Updates/`: option lists in both languages, `NotAsked` shown as `Never` and not saved on load, saved on change, channel saved and reloaded, downgrade note rule, about texts for installed / portable fakes, `RunKind.Updated` shows the line, embedded changelog is found and non-empty, data folder request raised.

## Acceptance criteria
- `rtk dotnet build Squill.Windows.slnx -c Release -warnaserror` 0 warnings; `rtk bypass dotnet test --solution Squill.Windows.slnx -c Release` green; both head publishes zero IL2xxx/IL3xxx warnings (embedded resource included).

## Suggested commit message
Add update settings and the About view-model
