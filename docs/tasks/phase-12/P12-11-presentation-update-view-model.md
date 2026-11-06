# P12-11 - Presentation: update view-model and composition

Status: todo
Depends on: P12-06, P12-08, P12-09, P12-10

## Goal
One view-model gives both heads everything the update UI needs (banner, dialog, progress, release notes, commands) over `UpdateService`, wired in `AppComposition` so heads add no logic.

## Design decisions (final)
- `UpdateViewModel` (sealed, `ViewModelBase`, namespace `Squill.Presentation.Updates`), constructor `(UpdateService service, IUiDispatcher dispatcher, ISettingsStore settings)`. It subscribes to `service.Changed` and marshals to the UI thread with `dispatcher` (`CheckAccess`/`Post`); no timers.
- Observable members: `Status` (`UpdateStatus`), `IsBannerVisible` (true in `Available`, `Downloading`, `Ready`, and `Failed` when an offer exists; false for `NotInstalled`, `UpToDate`, `Idle`, `Checking`, and when the user pressed *Later* for this version in this session), `BannerText` (`update.banner.available` "SQuilL {0} is available" / `...downloading` "Downloading {0}... {1} %" / `...ready` "SQuilL {0} is ready: restart to update" / `...failed` with the failure kind text), `Progress`, `AvailableVersion` (string), `CurrentVersion` (`AppVersion.Current`), `ReleaseNotes` (`IReadOnlyList<NotesBlock>` from `ReleaseNotes.Parse`), `HasReleaseNotes`, `DownloadSizeText` (`update.size`, "12.4 MB", culture formatting through the helper of P10-07), `IsDelta`, `StatusText` (for the about dialog: "Up to date" / "Checking..." / "Update available: {0}" / "Not installed through the installer: updates are disabled" / failure text), `IsBusy`, `CanCheck`, `CanDownload`, `CanRestart`, `LastCheckedText` (localized relative: never / date).
- Commands (`[RelayCommand]`, async ones take no parameter and use an internal `CancellationTokenSource` cancelled on `Dispose`): `CheckNowCommand`, `DownloadCommand`, `RestartToUpdateCommand`, `SkipVersionCommand`, `LaterCommand` (hides the banner until the next start; not persisted), `RetryCommand` (check or download depending on what failed).
- Startup: `UpdateViewModel.StartAsync()` is called once by `AppComposition` consumers (the head's main window after the first frame) and runs `service.CheckIfDueAsync`. It never throws; errors surface as `Failed`. **No call happens when the frequency is `NotAsked`**; instead `ShouldAskFrequency` is true (installed + `NotAsked`), and `ChooseFrequencyAsync(UpdateCheckFrequency)` saves the answer through the settings store (`Daily`, `Weekly`, `Startup` or `Never`) and, when it is not `Never`, runs a first check immediately. The heads show the one-time question (P12-13, P12-14 for WinUI; P12-15 Avalonia).
- Failure texts: keys `update.failure.network`, `.feed`, `.disk`, `.verification`, `.notinstalled`, `.unknown` (never show raw exception text; the raw message goes to the optional details line `FailureDetails`, shown only in the dialog).
- `AppComposition.Create`: builds `UpdateService` with `UpdateEngineFactory.Create("https://github.com/squillapp/squill", Environment.GetEnvironmentVariable("SQUILL_UPDATE_FEED"))`, `JsonUpdateStateStore(paths)`, `AppVersion.Current`; adds `UpdateViewModel Updates` at the end of `AppServices` (default parameter value not possible on a positional record: update the two heads' constructions of `AppServices` if any, and the tests). An optional `IUpdateEngine? updateEngine = null` parameter on `Create` lets tests inject a fake.
- Catalog: all keys above plus `update.button.checkNow`, `.download`, `.restart`, `.later`, `.skip`, `.retry`, `update.ask.title`, `update.ask.body` ("Should SQuilL check for updates on its own? It contacts GitHub; nothing else is sent."), `update.ask.daily`, `.weekly`, `.startup`, `.never` in **both** catalogs (parity tests of P10-17 must stay green).
- Never references Velopack or a UI type.

## Out of scope
- Settings rows (P12-12), About view-model (P12-12), all UI.

## Tasks
- [ ] View-model, composition change, catalog keys fr/en (French with accents like the existing catalog).
- [ ] Tests in `tests/Squill.Presentation.Tests/Updates/` with the `FakeUpdateEngine` (share it: move it to `tests/Shared` or link the file, do not duplicate) and `ImmediateDispatcher`: banner visibility per status, `Later` hides and a new version shows again, `NotAsked` -> `ShouldAskFrequency` and zero engine calls, `ChooseFrequencyAsync` saves and checks, `Never` saves and does not check, check -> Available -> Download -> Ready -> Restart calls `ApplyAndRestart` once, skip persists and silences the next scheduled check, failures map to the right key (fr and en text non-empty), commands disabled while busy, `Dispose` cancels a running check, text in both languages (`UiText` initialised for each).

## Acceptance criteria
- `rtk dotnet build Squill.slnx -c Release -warnaserror` and `Squill.Windows.slnx` 0 warnings; `rtk bypass dotnet test --solution Squill.Windows.slnx -c Release` green (including catalog parity and scanner tests).
- WinUI and Avalonia publishes still zero IL2xxx/IL3xxx warnings.

## Suggested commit message
Add the update view-model and wire the update service in the composition root
