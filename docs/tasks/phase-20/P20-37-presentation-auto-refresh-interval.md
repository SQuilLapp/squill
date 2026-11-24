# P20-37 - Presentation: the auto-refresh interval of a query tab (timer, pause rules, Custom...)

Status: todo
Depends on: P20-36

## Goal
Second half of the owner request of P20-36: a query tab re-runs its last run every `5 s`, `10 s`, `30 s`, `1 m`, `5 m` or a custom number of seconds, like DataGrip's clock menu (`Disabled` checked by default, the presets, a separator, `Custom...`). This card is the view-model: the interval, the timer loop on top of `QueryTabViewModel.RefreshAsync` (P20-36), the rules that pause or stop it, the `Custom...` input, the texts the button shows, and the localization keys. The buttons are P20-38 (WinUI) and P20-39 (Avalonia).

Files today: `src/Squill.Presentation/Query/QueryTabViewModel.cs` (`_time` is already injected), `src/Squill.Presentation/Workspace/WorkspaceViewModel.cs` (`ShowTab`, `CloseTabAsync`, the other `Tabs.Remove` site near line 615), precedent `src/Squill.Presentation/Activity/SessionsMonitorViewModel.cs` (`_time.CreateTimer(_ => _dispatcher.Post(Tick), ...)`, `SetActive`, `Tick`, `Idle`, interval kept in memory), `src/Squill.Presentation/IDialogService.cs` (`PromptAsync`, `ShowErrorAsync`), `src/Squill.Presentation/Localization/UiTextCatalogFr.cs` / `UiTextCatalogEn.cs`.

## Design decisions (final)
- **Timer**: the project's pattern, `TimeProvider.CreateTimer` + `IUiDispatcher.Post` (no `PeriodicTimer`, no new abstraction), as `SessionsMonitorViewModel`. **One-shot**, re-armed with `Change(interval, Timeout.InfiniteTimeSpan)` after each refresh ends or each skipped tick: the interval is counted from the end of the previous refresh, so two refreshes never overlap and a slow query never queues ticks.
- **State, per tab, in memory only, default `Disabled`**: like `SessionsMonitorViewModel.RefreshSeconds` (`not persisted`); nothing in `settings.json`, a new tab and a restarted app start `Disabled`. New members on `QueryTabViewModel` (XML doc on each):
  - `public static IReadOnlyList<TimeSpan> AutoRefreshPresets { get; }` = 5 s, 10 s, 30 s, 1 min, 5 min;
  - `public const int AutoRefreshMinSeconds = 1;` / `public const int AutoRefreshMaxSeconds = 3600;` (custom range, whole seconds);
  - `[ObservableProperty] public partial TimeSpan? AutoRefreshInterval { get; private set; }` (`null` = `Disabled`);
  - `public bool IsAutoRefreshCustom` (an interval not in the presets);
  - `public string AutoRefreshLabel` (empty when disabled, else `FormatInterval`: whole minutes `{n} m` / `{n} min`, otherwise `{n} s`), shown next to the clock;
  - `public string AutoRefreshToolTip` (one line `query.autoRefresh.off` or `query.autoRefresh.every`, plus a second line with `AutoRefreshPausedReason` when set, or `query.autoRefresh.unavailable` while `!CanRefresh`);
  - `[ObservableProperty] public partial string AutoRefreshPausedReason { get; private set; } = "";`
  - `public static string FormatInterval(TimeSpan interval)`;
  - `[RelayCommand] private void SetAutoRefresh(TimeSpan? interval)` (`null` disables; a value outside the range is ignored), `[RelayCommand] private Task SetCustomAutoRefreshAsync()`;
  - `public void SetActive(bool active)` (as the monitor), `internal void Tick()`, `internal Task Idle` (the refresh in flight, completed when idle), `internal void StopAutoRefresh()` (disables and disposes the timer).
- **Selecting an interval** while `CanRefresh` arms the timer; it does not refresh at once (the data is fresh from the run). While `!CanRefresh`, `SetAutoRefresh` with a value does nothing (the heads disable the button and say why in the tooltip).
- **Each tick** (on the UI thread): not active, or `AutoRefreshInterval` null -> nothing. Otherwise call `RefreshAsync(token)` (P20-36) and act on the outcome:
  - `Refreshed` -> clear `AutoRefreshPausedReason`, re-arm;
  - `Skipped` -> set `AutoRefreshPausedReason` (`paused.pending` for pending changes, `paused.editing` for a cell or preview edit, `paused.transaction` for a failed transaction, `paused.busy` for another run on the session or this tab running), re-arm; **pending grid edits are never discarded and never prompted for**: the refresh waits until they are committed or discarded;
  - `Failed` -> **stop**: `AutoRefreshInterval = null`, `StatusText = query.autoRefresh.stopped`; the failed statement's error is already shown once as a result (P20-36). Justification: the usual cause is a lost connection or a changed table; retrying every few seconds would replace the error by the same error, keep the session busy and hide the first message, and the sessions monitor already stops on its first failed read (`Error` stops its timer until resumed). The user picks an interval again to resume.
  - `Cancelled` (the user pressed `Stop` during an auto-refresh) -> disable as well (the user stopped it on purpose).
- **Transaction**: an open transaction in manual mode does **not** pause the refresh: a read-only statement adds nothing to the transaction (`EnsureOpenAsync` returns for read-only statements) and shows what `Run` would show at that moment (inside the transaction). A failed transaction pauses it (a run is refused there). Say this in the tooltip only through `paused.transaction`.
- **Tab visibility**: `WorkspaceViewModel.ShowTab` calls `SetActive(ReferenceEquals(t, tab))` on every query tab, next to the monitors. A hidden tab stops its timer; shown again, it re-arms a full interval (no burst of refreshes while switching tabs). The window being minimised is not detected (no signal exists in Presentation).
- **Stops for good**: on tab close (`CloseTabAsync` and the other `Tabs.Remove` site call `StopAutoRefresh`, next to `CancelPendingDiagnostics`), when a new manual run ends with `CanRefresh == false` (another table or a DML: `AutoRefreshInterval = null`), and on `Failed` / `Cancelled` as above. A new refreshable run keeps the interval and re-arms it.
- **`Custom...`**: `PromptAsync(query.autoRefresh.customTitle, query.autoRefresh.customPrompt, current seconds or "")`; `null` (cancel) changes nothing; the text is trimmed and parsed with `int.TryParse(..., NumberStyles.None, CultureInfo.InvariantCulture)`, accepted from 1 to 3600 seconds; anything else -> `ShowErrorAsync(customTitle, customInvalid)` and the interval is unchanged. A value equal to a preset selects that preset.
- **Keys** (both catalogs, parity and used-keys tests): `query.autoRefresh.menu` `Auto-refresh` / `Actualisation automatique`; `query.autoRefresh.disabled` `Disabled` / `Désactivée`; `query.autoRefresh.custom` `Custom...` / `Personnalisé...`; `query.autoRefresh.customValue` `Custom ({0})...` / `Personnalisé ({0})...`; `query.autoRefresh.seconds` `{0} s` / `{0} s`; `query.autoRefresh.minutes` `{0} m` / `{0} min`; `query.autoRefresh.off` `Auto-refresh: off` / `Actualisation automatique : désactivée`; `query.autoRefresh.every` `Auto-refresh every {0}` / `Actualisation automatique toutes les {0}`; `query.autoRefresh.unavailable` `Available after a read-only query ran without error` / `Disponible après une requête en lecture seule exécutée sans erreur`; `query.autoRefresh.paused.pending` `Paused: pending grid changes` / `En pause : modifications de la grille en attente`; `query.autoRefresh.paused.editing` `Paused: a value is being edited` / `En pause : une valeur est en cours de modification`; `query.autoRefresh.paused.transaction` `Paused: the transaction failed` / `En pause : la transaction a échoué`; `query.autoRefresh.paused.busy` `Paused: the session is busy` / `En pause : la session est occupée`; `query.autoRefresh.stopped` `Auto-refresh stopped: the refresh failed` / `Actualisation automatique arrêtée : l'actualisation a échoué`; `query.autoRefresh.customTitle` `Auto-refresh` / `Actualisation automatique`; `query.autoRefresh.customPrompt` `Seconds between two refreshes (1 to 3600):` / `Secondes entre deux actualisations (1 à 3600) :`; `query.autoRefresh.customInvalid` `Enter a whole number of seconds from 1 to 3600.` / `Saisissez un nombre entier de secondes entre 1 et 3600.`

## Out of scope
- The head buttons and menus (P20-38, P20-39). Saving the interval, a global default, refreshing a hidden tab, detecting a minimised window, retry with back-off, a countdown display.

## Tasks
- [ ] Tests first, `tests/Squill.Presentation.Tests/Query/QueryTabAutoRefreshTests.cs`, with the manual `TimeProvider` of `SessionsMonitorViewModelTests` (move `ManualTime` to a shared `internal` test file if it is a private nested class; no `Task.Delay`, advance the clock and `await vm.Idle`), `ImmediateDispatcher`, the fakes of P20-36:
  - `Default_Disabled`; `SetAutoRefresh_ArmsTimer_NoImmediateRefresh`; `Tick_AfterInterval_Refreshes_AndRearms` (two refreshes after two intervals, none before); `SlowRefresh_NeverOverlaps` (refresh held on a `TaskCompletionSource`, advance three intervals: one refresh in flight, the next interval counted from its end);
  - `PendingChanges_Paused_NoSql_ReasonSet`, then discard -> next tick refreshes and the reason clears; `Editing_Paused`; `FailedTransaction_Paused`; `OpenManualTransaction_StillRefreshes`;
  - `Failed_StopsAndSetsStatus`; `CancelledDuringAutoRefresh_Disables`;
  - `Hidden_NoRefresh_ShownAgain_RearmsFullInterval`; `Closed_StopsTimer` (through `WorkspaceViewModel.CloseTabAsync`); `NewDmlRun_Disables`; `NewSelectRun_KeepsInterval`;
  - `SetAutoRefresh_WhileCannotRefresh_Ignored`;
  - `Custom_Valid_SetsInterval`; `Custom_EqualToPreset_SelectsPreset`; `Custom_Invalid_ShowsErrorKeepsInterval` (theory: `0`, `3601`, `abc`, `1.5`, `-5`); `Custom_Cancelled_NoChange`;
  - `FormatInterval_SecondsAndMinutes` (fr and en through `UiText.Override`: `5 s`, `90 s`, `1 m` / `1 min`, `5 m` / `5 min`); `ToolTip_ShowsIntervalAndPausedReason`.
  - Catalog parity and used-keys tests green with the new keys.
  - See them fail, then implement.
- [ ] Implement the members, the timer, the workspace calls, the keys.
- [ ] XML doc grep on the new members.

## Acceptance criteria
- Recipe 2 common acceptance of [verification-recipe.md](verification-recipe.md) with `Squill.slnx` and `Squill.Windows.slnx`; no head code, no AOT publish.
- With an interval, a shown tab whose last run is refreshable re-runs it every interval, counted from the end of the previous refresh; pending edits, an open editor or a failed transaction pause it with a reason; a failed or cancelled refresh, a non-refreshable run and closing the tab stop it; nothing is saved.

## Risks
- The timer callback runs on a pool thread: everything goes through `_dispatcher.Post`, and a tick that arrives after `StopAutoRefresh` must do nothing (check a disposed flag, as the monitor does).
- `QueryTabViewModel` is not `IDisposable` today; do not make it so (the heads and the workspace would need changes everywhere): `StopAutoRefresh` disposes the timer.

## Suggested commit message
Add a per-tab auto-refresh interval that re-runs the last read-only query
